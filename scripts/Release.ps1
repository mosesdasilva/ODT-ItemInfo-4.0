[CmdletBinding()]
param(
    [string] $OutputDirectory
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$projectPath = Join-Path $repositoryRoot 'ODT-ItemInfo-4.0.csproj'
$metadataPath = Join-Path $repositoryRoot 'ItemInfo.cs'
$solutionPath = Join-Path $repositoryRoot 'ODT-ItemInfo-4.0.sln'

. (Join-Path $PSScriptRoot 'lib\ReleaseGate.ps1')

$propertyOutput = & dotnet msbuild $projectPath -getProperty:Version -getProperty:AssemblyName --nologo
if ($LASTEXITCODE -ne 0) {
    throw "Could not evaluate release properties; dotnet msbuild exited with code $LASTEXITCODE."
}
$properties = ($propertyOutput -join [Environment]::NewLine) | ConvertFrom-Json
$projectVersion = [string] $properties.Properties.Version
$assemblyName = [string] $properties.Properties.AssemblyName
if ([string]::IsNullOrWhiteSpace($projectVersion)) {
    throw 'The evaluated project Version is empty.'
}
if ([string]::IsNullOrWhiteSpace($assemblyName)) {
    throw 'The evaluated project AssemblyName is empty.'
}

$metadataSource = Get-Content -LiteralPath $metadataPath -Raw
$versionPattern = 'public\s+override\s+SemanticVersioning\.Version\s+Version\s*\{[^}]+\}\s*=\s*new\("([^"]+)"\)\s*;'
$runtimeVersionMatches = [regex]::Matches($metadataSource, $versionPattern)
if ($runtimeVersionMatches.Count -ne 1) {
    throw "Expected exactly one runtime metadata Version declaration, but found $($runtimeVersionMatches.Count)."
}
$runtimeVersion = $runtimeVersionMatches[0].Groups[1].Value
if (-not [string]::Equals($projectVersion, $runtimeVersion, [StringComparison]::Ordinal)) {
    throw "Evaluated project Version '$projectVersion' does not equal runtime metadata Version '$runtimeVersion'."
}

$modRoot = 'SPT/user/mods/ODT-ItemInfo-4.0'
$expectedEntries = @(
    "$modRoot/$assemblyName.dll",
    "$modRoot/LICENSE",
    "$modRoot/config/bsgblacklist.json",
    "$modRoot/config/config.json",
    "$modRoot/config/translations.json"
)
$releaseAssembly = Join-Path $repositoryRoot "bin\Release\$assemblyName\$assemblyName.dll"

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repositoryRoot 'artifacts\releases'
}
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

$archiveName = "ODT-ItemInfo-4.0_$projectVersion.zip"
$targetArchive = Join-Path $OutputDirectory $archiveName
$runId = [Guid]::NewGuid().ToString('N')
$temporaryRoot = Join-Path $OutputDirectory ".release-$runId"
$stageRoot = Join-Path $temporaryRoot 'stage'
$temporaryArchive = Join-Path $OutputDirectory ".$archiveName.$runId.tmp"
$replacementBackup = Join-Path $OutputDirectory ".$archiveName.$runId.backup"
$artifactCommitted = $false

try {
    Write-Host "Release gate: restore ($projectVersion)"
    & dotnet restore $solutionPath --nologo
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet restore failed with exit code $LASTEXITCODE."
    }

    Write-Host 'Release gate: build Release'
    & dotnet build $solutionPath --configuration Release --nologo
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet build failed with exit code $LASTEXITCODE."
    }

    Write-Host 'Release gate: test Release'
    & dotnet test $solutionPath --configuration Release --nologo
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet test failed with exit code $LASTEXITCODE."
    }

    if (-not (Test-Path -LiteralPath $releaseAssembly -PathType Leaf)) {
        throw "Release build output is missing the mod assembly: $releaseAssembly"
    }

    $stageModRoot = Join-Path $stageRoot $modRoot.Replace('/', '\')
    $stageConfigRoot = Join-Path $stageModRoot 'config'
    New-Item -ItemType Directory -Path $stageConfigRoot -Force | Out-Null
    Copy-Item -LiteralPath $releaseAssembly -Destination (Join-Path $stageModRoot "$assemblyName.dll")
    Copy-Item -LiteralPath (Join-Path $repositoryRoot 'LICENSE') -Destination (Join-Path $stageModRoot 'LICENSE')
    foreach ($configurationFile in @('bsgblacklist.json', 'config.json', 'translations.json')) {
        Copy-Item -LiteralPath (Join-Path $repositoryRoot "config\$configurationFile") -Destination (Join-Path $stageConfigRoot $configurationFile)
    }

    Assert-ExactReleaseMembership -ActualEntries @(Get-ReleaseStageEntries -StageRoot $stageRoot) -ExpectedEntries $expectedEntries -Location 'Release staging directory'
    foreach ($configurationFile in @('bsgblacklist.json', 'config.json', 'translations.json')) {
        Assert-ReleaseJsonFile -Path (Join-Path $stageConfigRoot $configurationFile)
    }
    New-ReleaseArchive -StageRoot $stageRoot -ArchivePath $temporaryArchive -ExpectedEntries $expectedEntries
    $testFault = [Environment]::GetEnvironmentVariable('ODT_ITEMINFO_RELEASE_TEST_FAULT')
    if (-not [string]::IsNullOrWhiteSpace($testFault)) {
        Invoke-ReleaseArchiveTestFault -ArchivePath $temporaryArchive -Fault $testFault
    }
    Assert-ReleaseArchive `
        -ArchivePath $temporaryArchive `
        -StageRoot $stageRoot `
        -ReleaseAssemblyPath $releaseAssembly `
        -ExpectedAssemblyName $assemblyName `
        -ExpectedEntries $expectedEntries

    Remove-Item -LiteralPath $temporaryRoot -Recurse -Force
    $artifactHash = Get-ReleaseFileHash -Path $temporaryArchive
    if (Test-Path -LiteralPath $targetArchive -PathType Leaf) {
        [IO.File]::Replace($temporaryArchive, $targetArchive, $replacementBackup)
    }
    else {
        [IO.File]::Move($temporaryArchive, $targetArchive)
    }
    $artifactCommitted = $true

    try {
        Write-Host "Release Candidate Artifact: $targetArchive"
        Write-Host "Version: $projectVersion"
        Write-Host "SHA-256: $artifactHash"
        Write-Host "File count: $($expectedEntries.Count)"
        Write-Output $targetArchive
    }
    catch {
        # The validated artifact is already committed. Console output cannot turn
        # that successful atomic replacement into a failed release run.
    }
}
finally {
    if (-not $artifactCommitted) {
        if (Test-Path -LiteralPath $temporaryArchive) {
            Remove-Item -LiteralPath $temporaryArchive -Force
        }
        if (Test-Path -LiteralPath $replacementBackup) {
            Remove-Item -LiteralPath $replacementBackup -Force
        }
        if (Test-Path -LiteralPath $temporaryRoot) {
            Remove-Item -LiteralPath $temporaryRoot -Recurse -Force
        }
    }
    else {
        try {
            if (Test-Path -LiteralPath $replacementBackup) {
                Remove-Item -LiteralPath $replacementBackup -Force -ErrorAction SilentlyContinue
            }
        }
        catch {
            # Nothing after the atomic commit may turn success into a failed run.
        }
    }
}
