[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$smokeScript = Join-Path $repositoryRoot 'scripts\Invoke-SptSmokeTest.ps1'
. (Join-Path $repositoryRoot 'scripts\lib\SptSmokeGate.ps1')
$fakeBuild = Join-Path $PSScriptRoot 'fixtures\FakeBuild.ps1'
$fakeServer = Join-Path $PSScriptRoot 'fixtures\FakeSptServer.ps1'
$testRoot = Join-Path $repositoryRoot 'artifacts\tmp\spt-smoke-regression'
$clonePath = Join-Path $testRoot 'clone'
$sourcePath = Join-Path $testRoot 'source'
$buildOutput = Join-Path $testRoot 'build-output'
$reportRoot = Join-Path $testRoot 'reports'

if (-not $testRoot.StartsWith($repositoryRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to use a test directory outside the repository: $testRoot"
}
if (Test-Path -LiteralPath $testRoot) {
    Remove-Item -LiteralPath $testRoot -Recurse -Force
}

New-Item -ItemType Directory -Path $sourcePath -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $clonePath 'SPT\user\mods\ODT-ItemInfo-4.0') -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $clonePath 'SPT\user\mods\unrelated-stale-mod') -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $clonePath 'SPT\user\logs\spt') -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $clonePath 'SPT\SPT_Data\configs') -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $clonePath 'Logs') -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $clonePath 'BepInEx') -Force | Out-Null
Set-Content -LiteralPath (Join-Path $clonePath 'SPT\user\mods\ODT-ItemInfo-4.0\stale.txt') -Value 'stale mod state'
Set-Content -LiteralPath (Join-Path $clonePath 'SPT\user\mods\unrelated-stale-mod\stale.txt') -Value 'unrelated stale mod state'
Set-Content -LiteralPath (Join-Path $clonePath 'SPT\user\logs\spt\stale.log') -Value 'stale server log'
Set-Content -LiteralPath (Join-Path $clonePath 'Logs\stale.log') -Value 'stale game log'
Set-Content -LiteralPath (Join-Path $clonePath 'BepInEx\LogOutput.log') -Value 'stale client log'
[ordered]@{
    ip = '127.0.0.1'
    port = 6969
    backendIp = '127.0.0.1'
    backendPort = 6969
} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $clonePath 'SPT\SPT_Data\configs\http.json') -Encoding UTF8

$portProbe = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
$portProbe.Start()
$serverPort = ([Net.IPEndPoint] $portProbe.LocalEndpoint).Port
$portProbe.Stop()

[ordered]@{
    schemaVersion = 1
    sourcePath = (Resolve-Path -LiteralPath $sourcePath).Path
    targetPath = (Resolve-Path -LiteralPath $clonePath).Path
    createdAtUtc = [DateTime]::UtcNow.ToString('o')
} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $clonePath '.odt-spt-test-clone.json') -Encoding UTF8

if (-not (Test-Path -LiteralPath $smokeScript -PathType Leaf)) {
    throw "Smoke CLI is missing: $smokeScript"
}

function Invoke-SmokeFixture {
    param(
        [Parameter(Mandatory)]
        [string] $Mode,

        [Parameter(Mandatory)]
        [int] $ExpectedExitCode,

        [AllowNull()]
        [string] $ExpectedFailureKind,

        [int] $TimeoutSeconds = 5
    )

    $buildArguments = "-NoProfile -ExecutionPolicy Bypass -File `"$fakeBuild`" -OutputPath `"$buildOutput`""
    $fakeServerLog = Join-Path $clonePath 'SPT\user\logs\spt\fake-server.log'
    $serverArguments = "-NoProfile -ExecutionPolicy Bypass -File `"$fakeServer`" -Mode $Mode -LogPath `"$fakeServerLog`" -ClonePath `"$clonePath`""
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $smokeScript `
        -ClonePath $clonePath `
        -ReportRoot $reportRoot `
        -ServerPort $serverPort `
        -TimeoutSeconds $TimeoutSeconds `
        -BuildExecutable 'powershell.exe' `
        -BuildArguments $buildArguments `
        -BuildOutputPath $buildOutput `
        -ServerExecutable 'powershell.exe' `
        -ServerArguments $serverArguments
    $actualExitCode = $LASTEXITCODE
    if ($actualExitCode -ne $ExpectedExitCode) {
        throw "$Mode smoke run exited $actualExitCode; expected $ExpectedExitCode."
    }

    $latestReport = Get-ChildItem -LiteralPath $reportRoot -Filter 'smoke-report.json' -Recurse -File |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1
    if ($null -eq $latestReport) {
        throw "$Mode smoke run did not retain a machine-readable report."
    }
    $report = Get-Content -Raw -LiteralPath $latestReport.FullName | ConvertFrom-Json
    $script:LastSmokeReport = $report
    if ($report.exitCode -ne $ExpectedExitCode) {
        throw "$Mode report exit code was $($report.exitCode); expected $ExpectedExitCode."
    }
    $failureKindsMatch = $report.failureKind -eq $ExpectedFailureKind
    if ([string]::IsNullOrEmpty([string] $report.failureKind) -and
        [string]::IsNullOrEmpty([string] $ExpectedFailureKind)) {
        $failureKindsMatch = $true
    }
    if (-not $failureKindsMatch) {
        throw "$Mode report failure kind was '$($report.failureKind)'; expected '$ExpectedFailureKind'."
    }
    if (-not (Test-Path -LiteralPath (Join-Path $latestReport.DirectoryName 'server.stdout.log') -PathType Leaf)) {
        throw "$Mode smoke run did not retain full server output."
    }
}

Invoke-SmokeFixture -Mode Ready -ExpectedExitCode 0 -ExpectedFailureKind $null

$readyReportDirectory = [string] $script:LastSmokeReport.reportDirectory
$startStage = @($script:LastSmokeReport.stages | Where-Object name -eq 'start')[0]
if (Get-Process -Id $startStage.processId -ErrorAction SilentlyContinue) {
    throw 'Smoke gate reported success while its server process was still alive.'
}
if ((Get-Content -Raw -LiteralPath (Join-Path $readyReportDirectory 'server.stdout.log')) -notmatch 'Server has started, happy playing') {
    throw 'Full server console output was not retained.'
}
if (-not (Test-Path -LiteralPath (Join-Path $readyReportDirectory 'game-logs\fake-game.log') -PathType Leaf)) {
    throw 'Current game logs were not retained.'
}
if (-not (Test-Path -LiteralPath (Join-Path $readyReportDirectory 'bepinex\LogOutput.log') -PathType Leaf)) {
    throw 'Current BepInEx logs were not retained.'
}

$httpConfig = Get-Content -Raw -LiteralPath (Join-Path $clonePath 'SPT\SPT_Data\configs\http.json') | ConvertFrom-Json
if ($httpConfig.port -ne $serverPort -or $httpConfig.backendPort -ne $serverPort) {
    throw 'Smoke gate did not assign the isolated clone endpoint.'
}

$installedMod = Join-Path $clonePath 'SPT\user\mods\ODT-ItemInfo-4.0'
if (Test-Path -LiteralPath (Join-Path $installedMod 'stale.txt')) {
    throw 'Prior mod state survived reset.'
}
if (Test-Path -LiteralPath (Join-Path $clonePath 'SPT\user\mods\unrelated-stale-mod')) {
    throw 'An unrelated prior mod survived reset.'
}
if (-not (Test-Path -LiteralPath (Join-Path $installedMod 'ODT-ItemInfo-4.0.dll') -PathType Leaf)) {
    throw 'Current build output was not installed.'
}
if (Test-Path -LiteralPath (Join-Path $clonePath 'SPT\user\logs\spt\stale.log')) {
    throw 'Prior SPT logs survived reset.'
}
if (Test-Path -LiteralPath (Join-Path $clonePath 'Logs\stale.log')) {
    throw 'Prior game logs survived reset.'
}
if ((Get-Content -Raw -LiteralPath (Join-Path $clonePath 'BepInEx\LogOutput.log')).Trim() -ne 'current BepInEx log') {
    throw 'Prior BepInEx log survived reset or current output was lost.'
}

Invoke-SmokeFixture -Mode Fatal -ExpectedExitCode 40 -ExpectedFailureKind 'fatal-server-error'
Invoke-SmokeFixture -Mode Dependency -ExpectedExitCode 41 -ExpectedFailureKind 'dependency-failure'
Invoke-SmokeFixture -Mode Configuration -ExpectedExitCode 42 -ExpectedFailureKind 'configuration-failure'
Invoke-SmokeFixture -Mode Timeout -ExpectedExitCode 44 -ExpectedFailureKind 'timeout' -TimeoutSeconds 1

$outsideRoot = Join-Path ([IO.Path]::GetTempPath()) "odt-smoke-outside-$([Guid]::NewGuid().ToString('N'))"
$outsideSource = Join-Path $outsideRoot 'source'
$outsideClone = Join-Path $outsideRoot 'clone'
New-Item -ItemType Directory -Path $outsideSource -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $outsideClone 'SPT\user\mods') -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $outsideClone 'SPT\user\logs') -Force | Out-Null
[ordered]@{
    schemaVersion = 1
    sourcePath = (Resolve-Path -LiteralPath $outsideSource).Path
    targetPath = (Resolve-Path -LiteralPath $outsideClone).Path
    createdAtUtc = [DateTime]::UtcNow.ToString('o')
} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $outsideClone '.odt-spt-test-clone.json') -Encoding UTF8

& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $smokeScript `
    -ClonePath $outsideClone `
    -ReportRoot $reportRoot
if ($LASTEXITCODE -ne 10) {
    throw "A marked clone outside repository artifacts was not rejected; exit code was $LASTEXITCODE."
}

$aliasPath = Join-Path $repositoryRoot "artifacts\tmp\outside-clone-alias-$([Guid]::NewGuid().ToString('N'))"
New-Item -ItemType Junction -Path $aliasPath -Value $outsideClone | Out-Null
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $smokeScript `
    -ClonePath $aliasPath `
    -ReportRoot $reportRoot
if ($LASTEXITCODE -ne 10) {
    throw "A repository-local junction alias to an outside clone was not rejected; exit code was $LASTEXITCODE."
}
[IO.Directory]::Delete($aliasPath)

$aliasedRepository = Join-Path $outsideRoot 'aliased-repository'
$artifactsTarget = Join-Path $outsideRoot 'external-artifacts'
$aliasedArtifacts = Join-Path $aliasedRepository 'artifacts'
$aliasedClone = Join-Path $aliasedArtifacts 'clone'
New-Item -ItemType Directory -Path $aliasedRepository -Force | Out-Null
New-Item -ItemType Directory -Path $artifactsTarget -Force | Out-Null
New-Item -ItemType Junction -Path $aliasedArtifacts -Value $artifactsTarget | Out-Null
New-Item -ItemType Directory -Path $aliasedClone -Force | Out-Null

$aliasedArtifactsRejected = $false
try {
    Assert-SptTestClone -ClonePath $aliasedClone -RepositoryRoot $aliasedRepository
}
catch {
    $aliasedArtifactsRejected = $true
}
if (-not $aliasedArtifactsRejected) {
    throw 'A repository whose artifacts root is a junction was not rejected.'
}
[IO.Directory]::Delete($aliasedArtifacts)
Remove-Item -LiteralPath $outsideRoot -Recurse -Force

Remove-Item -LiteralPath $testRoot -Recurse -Force
Write-Host 'PASS: SPT smoke CLI success and failure contracts satisfied.'
