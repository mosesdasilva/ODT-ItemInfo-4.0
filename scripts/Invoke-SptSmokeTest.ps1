[CmdletBinding()]
param(
    [string] $ClonePath,
    [string] $ReportRoot,
    [ValidateRange(1, 3600)]
    [int] $TimeoutSeconds = 120,
    [ValidateRange(1, 65535)]
    [int] $ServerPort = 6970,
    [string] $BuildExecutable = 'dotnet',
    [string] $BuildArguments,
    [string] $BuildOutputPath,
    [string] $ReleaseCandidateArchive,
    [string] $ServerExecutable,
    [string] $ServerArguments = ''
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if ([string]::IsNullOrWhiteSpace($ClonePath)) {
    $ClonePath = Join-Path $repositoryRoot 'artifacts\spt-test-clone'
}
if ([string]::IsNullOrWhiteSpace($ReportRoot)) {
    $ReportRoot = Join-Path $repositoryRoot 'artifacts\spt-smoke'
}
if ([string]::IsNullOrWhiteSpace($BuildOutputPath)) {
    $BuildOutputPath = Join-Path $repositoryRoot 'bin\Release\ODT-ItemInfo-4.0'
}
if ([string]::IsNullOrWhiteSpace($BuildArguments)) {
    $solution = Join-Path $repositoryRoot 'ODT-ItemInfo-4.0.sln'
    $BuildArguments = "build `"$solution`" --configuration Release --nologo --property:EnableForgeStaging=false"
}

. (Join-Path $PSScriptRoot 'lib\SptSmokeGate.ps1')

$runId = '{0}-{1}' -f ([DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ')), ([Guid]::NewGuid().ToString('N').Substring(0, 8))
$reportDirectory = Join-Path ([IO.Path]::GetFullPath($ReportRoot)) $runId
New-Item -ItemType Directory -Path $reportDirectory -Force | Out-Null
$stdoutPath = Join-Path $reportDirectory 'server.stdout.log'
$stderrPath = Join-Path $reportDirectory 'server.stderr.log'
$reportPath = Join-Path $reportDirectory 'smoke-report.json'
$serverProcessId = $null
$exitCode = 10
$failureKind = 'preflight-failure'
$outcome = 'failed'
$failureMessage = $null
$startedAt = [DateTime]::UtcNow
$stages = [Collections.Generic.List[object]]::new()
$releaseCandidate = $null
$sptVersion = $null
$expectedReleaseEntries = @(Get-ExpectedReleaseEntries -AssemblyName 'ODT-ItemInfo-4.0')

try {
    $ClonePath = Assert-SptTestClone -ClonePath $ClonePath -RepositoryRoot $repositoryRoot
    $stages.Add([pscustomobject]@{ name = 'preflight'; outcome = 'passed' })

    Reset-SptTestClone -ClonePath $ClonePath
    $stages.Add([pscustomobject]@{ name = 'reset'; outcome = 'passed' })

    try {
        Set-SptTestClonePort -ClonePath $ClonePath -ServerPort $ServerPort
        $stages.Add([pscustomobject]@{ name = 'configure-endpoint'; outcome = 'passed'; port = $ServerPort })
    }
    catch {
        $exitCode = 12
        $failureKind = 'endpoint-failure'
        throw
    }

    if ([string]::IsNullOrWhiteSpace($ReleaseCandidateArchive)) {
        try {
            Invoke-SptModBuild `
                -RepositoryRoot $repositoryRoot `
                -BuildExecutable $BuildExecutable `
                -BuildArguments $BuildArguments `
                -BuildOutputPath $BuildOutputPath `
                -ReportDirectory $reportDirectory
            $stages.Add([pscustomobject]@{ name = 'build'; outcome = 'passed' })
        }
        catch {
            $exitCode = 20
            $failureKind = 'build-failure'
            throw
        }

        try {
            Install-SptModOutput `
                -RepositoryRoot $repositoryRoot `
                -BuildOutputPath $BuildOutputPath `
                -ClonePath $ClonePath `
                -ModName 'ODT-ItemInfo-4.0'
            $stages.Add([pscustomobject]@{ name = 'install'; outcome = 'passed'; source = 'build-output' })
        }
        catch {
            $exitCode = 30
            $failureKind = 'install-failure'
            throw
        }
    }
    else {
        try {
            $releaseCandidate = Get-SptReleaseArchiveIdentity -ArchivePath $ReleaseCandidateArchive -ExpectedEntries $expectedReleaseEntries
            Install-SptReleaseCandidate -ArchivePath $releaseCandidate.path -ClonePath $ClonePath -ExpectedEntries $expectedReleaseEntries
            $installedFiles = @(Assert-SptInstalledRelease -ArchivePath $releaseCandidate.path -ClonePath $ClonePath -ExpectedEntries $expectedReleaseEntries)
            $releaseCandidate | Add-Member -NotePropertyName installedFiles -NotePropertyValue $installedFiles
            $stages.Add([pscustomobject]@{ name = 'install'; outcome = 'passed'; source = 'release-candidate-zip' })
            $stages.Add([pscustomobject]@{ name = 'installed-byte-identity'; outcome = 'passed'; fileCount = $installedFiles.Count })
        }
        catch {
            $exitCode = 30
            $failureKind = 'install-failure'
            throw
        }
    }

    $serverWorkingDirectory = $ClonePath
    if ([string]::IsNullOrWhiteSpace($ServerExecutable)) {
        $ServerExecutable = Join-Path $ClonePath 'SPT\SPT.Server.exe'
        $serverWorkingDirectory = Join-Path $ClonePath 'SPT'
        $sptVersion = Assert-SptServerVersion -ServerExecutable $ServerExecutable -ExpectedVersion '4.0.13'
        $stages.Add([pscustomobject]@{ name = 'spt-version'; outcome = 'passed'; version = $sptVersion })
    }
    else {
        $serverCommand = Get-Command $ServerExecutable -CommandType Application -ErrorAction SilentlyContinue
        if ($null -ne $serverCommand) {
            $ServerExecutable = $serverCommand.Source
        }
        elseif (-not [IO.Path]::IsPathRooted($ServerExecutable)) {
            $ServerExecutable = Join-Path $ClonePath $ServerExecutable
        }
    }
    if (-not (Get-Command $ServerExecutable -ErrorAction SilentlyContinue) -and
        -not (Test-Path -LiteralPath $ServerExecutable -PathType Leaf)) {
        $exitCode = 50
        $failureKind = 'start-failure'
        throw "Server executable was not found: $ServerExecutable"
    }

    try {
        $serverProcessId = Start-SptConsoleProcess `
            -ServerExecutable $ServerExecutable `
            -ServerArguments $ServerArguments `
            -WorkingDirectory $serverWorkingDirectory `
            -StandardOutputPath $stdoutPath `
            -StandardErrorPath $stderrPath `
            -LaunchScriptPath (Join-Path $reportDirectory 'launch-server.cmd')
    }
    catch {
        $exitCode = 50
        $failureKind = 'start-failure'
        throw
    }
    $stages.Add([pscustomobject]@{ name = 'start'; outcome = 'passed'; processId = $serverProcessId })

    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    while ($true) {
        Start-Sleep -Milliseconds 200
        $serverOutput = Get-SptServerOutput -ClonePath $ClonePath -StandardOutputPath $stdoutPath -StandardErrorPath $stderrPath

        if ($serverOutput -match '(?i)config(?:uration)?.*(?:not found|missing|invalid|failed)|JsonException|Could not find translations file') {
            $exitCode = 42
            $failureKind = 'configuration-failure'
            throw 'SPT server reported a configuration failure.'
        }
        if ($serverOutput -match '(?i)Could not load file or assembly|dependency.*(?:missing|not found)|MissingMethodException|FileNotFoundException') {
            $exitCode = 41
            $failureKind = 'dependency-failure'
            throw 'SPT server reported a dependency failure.'
        }
        if ($serverOutput -match '(?i)\[Fatal\]|Critical exception, stopping server|Unhandled exception') {
            $exitCode = 40
            $failureKind = 'fatal-server-error'
            throw 'SPT server reported a fatal error.'
        }
        if ($serverOutput -match '(?i)Server has started, happy playing') {
            $stages.Add([pscustomobject]@{ name = 'readiness'; outcome = 'passed' })
            break
        }
        if (-not (Get-Process -Id $serverProcessId -ErrorAction SilentlyContinue)) {
            $exitCode = 43
            $failureKind = 'server-exited-before-ready'
            throw 'SPT server exited before readiness.'
        }
        if ([DateTime]::UtcNow -ge $deadline) {
            $exitCode = 44
            $failureKind = 'timeout'
            throw "SPT server did not become ready within $TimeoutSeconds seconds."
        }
    }

    if ($null -ne $releaseCandidate) {
        $postSmokeIdentity = Get-SptReleaseArchiveIdentity -ArchivePath $releaseCandidate.path -ExpectedEntries $expectedReleaseEntries
        if ($postSmokeIdentity.sha256 -ne $releaseCandidate.sha256) {
            $exitCode = 30
            $failureKind = 'install-failure'
            throw 'Release Candidate Artifact changed during the smoke run; release readiness must restart.'
        }
        $null = @(Assert-SptInstalledRelease -ArchivePath $releaseCandidate.path -ClonePath $ClonePath -ExpectedEntries $expectedReleaseEntries)
        $stages.Add([pscustomobject]@{ name = 'post-smoke-byte-identity'; outcome = 'passed'; fileCount = $releaseCandidate.fileCount })
    }
    $exitCode = 0
    $failureKind = $null
    $outcome = 'passed'
}
catch {
    $failureMessage = $_.Exception.Message
    Write-Error -Message $failureMessage -ErrorAction Continue
}
finally {
    try {
        $stoppedProcessIds = @(Stop-SptServerProcess -RootProcessId $serverProcessId)
        $stages.Add([pscustomobject]@{ name = 'stop'; outcome = 'passed'; processIds = $stoppedProcessIds })
    }
    catch {
        $exitCode = 51
        $failureKind = 'stop-failure'
        $failureMessage = $_.Exception.Message
        $outcome = 'failed'
        $stages.Add([pscustomobject]@{ name = 'stop'; outcome = 'failed'; message = $failureMessage })
        Write-Error -Message $failureMessage -ErrorAction Continue
    }
    Copy-SptLogsToReport -ClonePath $ClonePath -ReportDirectory $reportDirectory

    $report = [ordered]@{
        schemaVersion = 1
        runId = $runId
        outcome = $outcome
        exitCode = $exitCode
        failureKind = $failureKind
        failureMessage = $failureMessage
        clonePath = [IO.Path]::GetFullPath($ClonePath)
        buildOutputPath = if ([string]::IsNullOrWhiteSpace($ReleaseCandidateArchive)) { [IO.Path]::GetFullPath($BuildOutputPath) } else { $null }
        releaseCandidate = $releaseCandidate
        sptVersion = $sptVersion
        reportDirectory = $reportDirectory
        startedAtUtc = $startedAt.ToString('o')
        finishedAtUtc = [DateTime]::UtcNow.ToString('o')
        timeoutSeconds = $TimeoutSeconds
        serverPort = $ServerPort
        serverExecutable = $ServerExecutable
        serverArguments = $ServerArguments
        stages = @($stages)
    }
    $report | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $reportPath -Encoding UTF8
    Write-Output "Smoke report: $reportPath"
}

exit $exitCode
