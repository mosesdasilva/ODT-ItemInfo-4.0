[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$library = Join-Path $repositoryRoot 'scripts\lib\ReleaseReadinessGate.ps1'
$testRoot = Join-Path $repositoryRoot 'artifacts\tmp\release-readiness-regression'
$inputRoot = Join-Path $testRoot 'input'
$recordPath = Join-Path $testRoot 'readiness.json'

if (Test-Path -LiteralPath $testRoot) {
    Remove-Item -LiteralPath $testRoot -Recurse -Force
}
New-Item -ItemType Directory -Path (Join-Path $inputRoot 'config') -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $inputRoot 'scripts') -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $inputRoot 'tests') -Force | Out-Null
Set-Content -LiteralPath (Join-Path $inputRoot 'ItemInfo.cs') -Value 'code-v1'
Set-Content -LiteralPath (Join-Path $inputRoot 'ODT.csproj') -Value 'project-v1'
Set-Content -LiteralPath (Join-Path $inputRoot 'LICENSE') -Value 'license-v1'
Set-Content -LiteralPath (Join-Path $inputRoot 'config\config.json') -Value '{}'
Set-Content -LiteralPath (Join-Path $inputRoot 'scripts\Release.ps1') -Value 'release-v1'
Set-Content -LiteralPath (Join-Path $inputRoot 'tests\Nested.Tests.csproj') -Value 'nested-project-v1'
Set-Content -LiteralPath (Join-Path $inputRoot 'README.md') -Value 'documentation-v1'

. $library

$before = Get-ReleaseReadinessInputIdentity -RepositoryRoot $inputRoot
Set-Content -LiteralPath (Join-Path $inputRoot 'config\config.json') -Value '{ "changed": true }'
$after = Get-ReleaseReadinessInputIdentity -RepositoryRoot $inputRoot
if ($before.sha256 -eq $after.sha256) {
    throw 'An artifact-affecting configuration change did not invalidate release readiness.'
}

Set-Content -LiteralPath (Join-Path $inputRoot 'config\config.json') -Value '{}'
$restored = Get-ReleaseReadinessInputIdentity -RepositoryRoot $inputRoot
Set-Content -LiteralPath (Join-Path $inputRoot 'README.md') -Value 'documentation-v2'
$documentationOnly = Get-ReleaseReadinessInputIdentity -RepositoryRoot $inputRoot
if ($restored.sha256 -ne $documentationOnly.sha256) {
    throw 'A non-artifact documentation change unexpectedly changed the release-input identity.'
}

$beforeNestedProjectChange = Get-ReleaseReadinessInputIdentity -RepositoryRoot $inputRoot
Set-Content -LiteralPath (Join-Path $inputRoot 'tests\Nested.Tests.csproj') -Value 'nested-project-v2'
$afterNestedProjectChange = Get-ReleaseReadinessInputIdentity -RepositoryRoot $inputRoot
if ($beforeNestedProjectChange.sha256 -eq $afterNestedProjectChange.sha256) {
    throw 'A nested project dependency-version change did not invalidate release readiness.'
}

$candidate = [pscustomobject]@{
    path = 'C:\candidate.zip'
    version = '2.0.14'
    sha256 = 'ABC123'
    fileCount = 5
}
$gates = [ordered]@{
    automatedTests = [ordered]@{ outcome = 'passed'; logPath = 'C:\release.log' }
    archiveValidation = [ordered]@{ outcome = 'passed'; logPath = 'C:\release.log' }
    cleanInstall = [ordered]@{ outcome = 'passed'; clonePath = 'C:\clone' }
    installedByteIdentity = [ordered]@{ outcome = 'passed'; fileCount = 5 }
    serverSmoke = [ordered]@{ outcome = 'passed'; reportPath = 'C:\smoke-report.json' }
}
Write-ReleaseReadinessRecord `
    -Path $recordPath `
    -SourceRevision 'deadbeef' `
    -InputIdentity $restored `
    -ReleaseCandidate $candidate `
    -Gates $gates `
    -LogLocations @('C:\release.log', 'C:\smoke-report.json')

$record = Get-Content -Raw -LiteralPath $recordPath | ConvertFrom-Json
if ($record.outcome -ne 'passed' -or $record.releaseCandidate.fileCount -ne 5 -or $record.gates.serverSmoke.outcome -ne 'passed') {
    throw 'The readiness record did not capture the candidate identity and all successful gates.'
}
if ($record.invalidationRule -notmatch 'restart') {
    throw 'The readiness record did not state that artifact-affecting changes restart readiness.'
}

Remove-Item -LiteralPath $testRoot -Recurse -Force
Write-Host 'PASS: release-readiness invalidation and record contracts satisfied.'
