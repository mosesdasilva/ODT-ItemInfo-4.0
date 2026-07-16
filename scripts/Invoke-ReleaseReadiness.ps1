[CmdletBinding()]
param(
    [string] $ClonePath,
    [string] $ReportRoot,
    [ValidateRange(1, 3600)]
    [int] $TimeoutSeconds = 120,
    [ValidateRange(1, 65535)]
    [int] $ServerPort = 6970
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if ([string]::IsNullOrWhiteSpace($ClonePath)) {
    $ClonePath = Join-Path $repositoryRoot 'artifacts\spt-test-clone'
}
if ([string]::IsNullOrWhiteSpace($ReportRoot)) {
    $ReportRoot = Join-Path $repositoryRoot 'artifacts\release-readiness'
}

. (Join-Path $PSScriptRoot 'lib\SptSmokeGate.ps1')
. (Join-Path $PSScriptRoot 'lib\ReleaseReadinessGate.ps1')

$runId = '{0}-{1}' -f ([DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ')), ([Guid]::NewGuid().ToString('N').Substring(0, 8))
$reportDirectory = Join-Path ([IO.Path]::GetFullPath($ReportRoot)) $runId
$readinessPath = Join-Path $reportDirectory 'readiness.json'
$releaseStdout = Join-Path $reportDirectory 'release.stdout.log'
$releaseStderr = Join-Path $reportDirectory 'release.stderr.log'
$smokeCliStdout = Join-Path $reportDirectory 'smoke-cli.stdout.log'
$smokeCliStderr = Join-Path $reportDirectory 'smoke-cli.stderr.log'
New-Item -ItemType Directory -Path $reportDirectory -Force | Out-Null

try {
    $ClonePath = Assert-SptTestClone -ClonePath $ClonePath -RepositoryRoot $repositoryRoot
    $inputIdentity = Get-ReleaseReadinessInputIdentity -RepositoryRoot $repositoryRoot

    $projectPath = Join-Path $repositoryRoot 'ODT-ItemInfo-4.0.csproj'
    $propertyOutput = & dotnet msbuild $projectPath -getProperty:Version -getProperty:AssemblyName --nologo
    if ($LASTEXITCODE -ne 0) {
        throw "Could not evaluate the release version; dotnet msbuild exited with code $LASTEXITCODE."
    }
    $properties = ($propertyOutput -join [Environment]::NewLine) | ConvertFrom-Json
    $version = [string] $properties.Properties.Version
    if ([string]::IsNullOrWhiteSpace($version)) {
        throw 'The evaluated release version is empty.'
    }

    $releaseScript = Join-Path $PSScriptRoot 'Release.ps1'
    $releaseArguments = '-NoProfile -ExecutionPolicy Bypass -File "{0}"' -f $releaseScript
    $releaseProcess = Invoke-LoggedProcess `
        -Executable 'powershell.exe' `
        -Arguments $releaseArguments `
        -WorkingDirectory $repositoryRoot `
        -StandardOutputPath $releaseStdout `
        -StandardErrorPath $releaseStderr `
        -CaptureOutput `
        -Wait
    if ($releaseProcess.ExitCode -ne 0) {
        throw "The exact release command failed with exit code $($releaseProcess.ExitCode)."
    }

    $archivePath = Join-Path $repositoryRoot "artifacts\releases\ODT-ItemInfo-4.0_$version.zip"
    $expectedEntries = @(Get-ExpectedReleaseEntries -AssemblyName 'ODT-ItemInfo-4.0')
    $candidate = Get-SptReleaseArchiveIdentity -ArchivePath $archivePath -ExpectedEntries $expectedEntries
    if ($candidate.version -ne $version) {
        throw "Release Candidate Artifact version '$($candidate.version)' does not equal evaluated version '$version'."
    }
    $afterReleaseInputs = Get-ReleaseReadinessInputIdentity -RepositoryRoot $repositoryRoot
    if ($afterReleaseInputs.sha256 -ne $inputIdentity.sha256) {
        throw 'Artifact-affecting inputs changed while producing the Release Candidate Artifact; release readiness must restart.'
    }

    $smokeReportRoot = Join-Path $reportDirectory 'smoke'
    $smokeScript = Join-Path $PSScriptRoot 'Invoke-SptSmokeTest.ps1'
    $smokeArguments = '-NoProfile -ExecutionPolicy Bypass -File "{0}" -ClonePath "{1}" -ReportRoot "{2}" -TimeoutSeconds {3} -ServerPort {4} -ReleaseCandidateArchive "{5}"' -f `
        $smokeScript, $ClonePath, $smokeReportRoot, $TimeoutSeconds, $ServerPort, $candidate.path
    $smokeProcess = Invoke-LoggedProcess `
        -Executable 'powershell.exe' `
        -Arguments $smokeArguments `
        -WorkingDirectory $repositoryRoot `
        -StandardOutputPath $smokeCliStdout `
        -StandardErrorPath $smokeCliStderr `
        -CaptureOutput `
        -Wait
    if ($smokeProcess.ExitCode -ne 0) {
        throw "Release Candidate Artifact smoke failed with exit code $($smokeProcess.ExitCode)."
    }
    $smokeReportFile = Get-ChildItem -LiteralPath $smokeReportRoot -Filter 'smoke-report.json' -File -Recurse |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1
    if ($null -eq $smokeReportFile) {
        throw 'Release Candidate Artifact smoke did not retain a machine-readable report.'
    }
    $smokeReport = Get-Content -Raw -LiteralPath $smokeReportFile.FullName | ConvertFrom-Json
    if ($smokeReport.outcome -ne 'passed' -or
        $smokeReport.releaseCandidate.sha256 -ne $candidate.sha256 -or
        $smokeReport.sptVersion -ne '4.0.13') {
        throw 'Smoke report does not prove readiness for the sealed Release Candidate Artifact.'
    }

    $afterSmokeInputs = Get-ReleaseReadinessInputIdentity -RepositoryRoot $repositoryRoot
    if ($afterSmokeInputs.sha256 -ne $inputIdentity.sha256) {
        throw 'Artifact-affecting inputs changed during smoke; release readiness must restart.'
    }
    $postSmokeCandidate = Get-SptReleaseArchiveIdentity -ArchivePath $candidate.path -ExpectedEntries $expectedEntries
    if ($postSmokeCandidate.sha256 -ne $candidate.sha256) {
        throw 'The Release Candidate Artifact changed during readiness; release readiness must restart.'
    }

    $sourceRevision = (& git -C $repositoryRoot rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not record the source revision for release readiness.'
    }
    $gates = [ordered]@{
        automatedTests = [ordered]@{ outcome = 'passed'; command = 'scripts\Release.ps1'; logPath = $releaseStdout }
        archiveValidation = [ordered]@{ outcome = 'passed'; fileCount = $candidate.fileCount; logPath = $releaseStdout }
        cleanInstall = [ordered]@{ outcome = 'passed'; source = $candidate.path; clonePath = $ClonePath }
        installedByteIdentity = [ordered]@{ outcome = 'passed'; fileCount = @($smokeReport.releaseCandidate.installedFiles).Count; reportPath = $smokeReportFile.FullName }
        serverSmoke = [ordered]@{ outcome = 'passed'; sptVersion = '4.0.13'; reportPath = $smokeReportFile.FullName; logDirectory = $smokeReport.reportDirectory }
    }
    Write-ReleaseReadinessRecord `
        -Path $readinessPath `
        -SourceRevision $sourceRevision `
        -InputIdentity $inputIdentity `
        -ReleaseCandidate $candidate `
        -Gates $gates `
        -LogLocations @($releaseStdout, $releaseStderr, $smokeCliStdout, $smokeCliStderr, $smokeReportFile.FullName, [string] $smokeReport.reportDirectory)

    Write-Host "Release readiness passed: $readinessPath"
    Write-Output $readinessPath
}
catch {
    Write-Error -Message $_.Exception.Message -ErrorAction Continue
    Write-Host "Release readiness evidence: $reportDirectory"
    exit 1
}
