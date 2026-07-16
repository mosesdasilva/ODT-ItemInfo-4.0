[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$releaseScript = Join-Path $repositoryRoot 'scripts\Release.ps1'
$releaseLibrary = Join-Path $repositoryRoot 'scripts\lib\ReleaseGate.ps1'
$testRoot = Join-Path $repositoryRoot 'artifacts\tmp\release-gate-regression'
$successOutput = Join-Path $testRoot 'success-output'
$expectedArchiveName = 'ODT-ItemInfo-4.0_2.0.14.zip'
$expectedEntries = @(
    'SPT/user/mods/ODT-ItemInfo-4.0/ODT-ItemInfo-4.0.dll',
    'SPT/user/mods/ODT-ItemInfo-4.0/LICENSE',
    'SPT/user/mods/ODT-ItemInfo-4.0/config/bsgblacklist.json',
    'SPT/user/mods/ODT-ItemInfo-4.0/config/config.json',
    'SPT/user/mods/ODT-ItemInfo-4.0/config/translations.json'
)

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
. $releaseLibrary

function Assert-True {
    param([bool] $Condition, [string] $Message)
    if (-not $Condition) {
        throw $Message
    }
}

function Invoke-ReleaseTestCommand {
    param([string] $Repository, [string] $OutputDirectory, [string] $LogName, [string] $Fault)

    $scriptPath = Join-Path $Repository 'scripts\Release.ps1'
    $logPath = Join-Path $testRoot $LogName
    $previousPreference = $ErrorActionPreference
    $previousFault = [Environment]::GetEnvironmentVariable('ODT_ITEMINFO_RELEASE_TEST_FAULT')
    $ErrorActionPreference = 'Continue'
    try {
        [Environment]::SetEnvironmentVariable('ODT_ITEMINFO_RELEASE_TEST_FAULT', $Fault)
        $commandOutput = & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $scriptPath -OutputDirectory $OutputDirectory 2>&1
        $exitCode = $LASTEXITCODE
        $commandOutput | Out-File -LiteralPath $logPath -Encoding utf8
        return $exitCode
    }
    finally {
        [Environment]::SetEnvironmentVariable('ODT_ITEMINFO_RELEASE_TEST_FAULT', $previousFault)
        $ErrorActionPreference = $previousPreference
    }
}

function Copy-ReleaseTestRepository {
    param([string] $Name)

    $destination = Join-Path $testRoot $Name
    New-Item -ItemType Directory -Path $destination -Force | Out-Null
    $files = @(& git -C $repositoryRoot ls-files)
    Assert-True ($LASTEXITCODE -eq 0) 'Could not enumerate repository files for the release regression fixture.'
    $files += @('scripts/Release.ps1', 'scripts/lib/ReleaseGate.ps1')
    foreach ($relativePath in @($files | Sort-Object -Unique)) {
        $source = Join-Path $repositoryRoot $relativePath.Replace('/', '\')
        if (-not (Test-Path -LiteralPath $source -PathType Leaf)) {
            continue
        }
        $target = Join-Path $destination $relativePath.Replace('/', '\')
        New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
        Copy-Item -LiteralPath $source -Destination $target
    }
    return $destination
}

function Assert-FailedRunIsClean {
    param([string] $OutputDirectory)

    if (-not (Test-Path -LiteralPath $OutputDirectory)) {
        return
    }
    $temporaryItems = @(Get-ChildItem -LiteralPath $OutputDirectory -Force | Where-Object {
        $_.Name.StartsWith('.release-', [StringComparison]::Ordinal) -or
        $_.Name.EndsWith('.tmp', [StringComparison]::Ordinal) -or
        $_.Name.EndsWith('.backup', [StringComparison]::Ordinal)
    })
    Assert-True ($temporaryItems.Count -eq 0) 'A failed release left temporary staging or archive output behind.'
}

function Get-ReleaseValidationDirectories {
    return @(
        Get-ChildItem -LiteralPath ([IO.Path]::GetTempPath()) -Directory -Filter 'odt-release-validation-*' -ErrorAction SilentlyContinue |
            ForEach-Object { $_.FullName }
    )
}

function Assert-NoNewValidationDirectories {
    param([string[]] $Before)

    $beforeSet = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($path in $Before) {
        $null = $beforeSet.Add($path)
    }
    $leftovers = @(Get-ReleaseValidationDirectories | Where-Object { -not $beforeSet.Contains($_) })
    Assert-True ($leftovers.Count -eq 0) "Release validation left temporary directories behind: $($leftovers -join ', ')"
}

function Assert-ThrowsLike {
    param([scriptblock] $Action, [string] $Pattern, [string] $Message)

    try {
        & $Action
    }
    catch {
        if ($_.Exception.Message -match $Pattern) {
            return
        }
        throw "$Message Actual error: $($_.Exception.Message)"
    }
    throw $Message
}

function New-TestZip {
    param([string] $Path, [object[]] $Entries)

    if (Test-Path -LiteralPath $Path) {
        Remove-Item -LiteralPath $Path -Force
    }
    $archive = [IO.Compression.ZipFile]::Open($Path, [IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($entry in $Entries) {
            [IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
                $archive,
                [string] $entry.Source,
                [string] $entry.Name,
                [IO.Compression.CompressionLevel]::Optimal
            ) | Out-Null
        }
    }
    finally {
        $archive.Dispose()
    }
}

if (Test-Path -LiteralPath $testRoot) {
    Remove-Item -LiteralPath $testRoot -Recurse -Force
}
New-Item -ItemType Directory -Path $testRoot -Force | Out-Null
$testPassed = $false

try {
    $tokens = $null
    $parseErrors = $null
    $releaseAst = [Management.Automation.Language.Parser]::ParseFile($releaseScript, [ref] $tokens, [ref] $parseErrors)
    Assert-True ($parseErrors.Count -eq 0) 'The release command does not parse under Windows PowerShell 5.1.'
    $declaredParameters = @($releaseAst.ParamBlock.Parameters | ForEach-Object { $_.Name.VariablePath.UserPath })
    Assert-True ($declaredParameters.Count -eq 1 -and $declaredParameters[0] -eq 'OutputDirectory') 'The release command exposes a version override or skip switch.'
    $projectSource = [IO.File]::ReadAllText((Join-Path $repositoryRoot 'ODT-ItemInfo-4.0.csproj'))
    Assert-True ($projectSource.Contains('<Target Name="DebugCopy"')) 'The separate Debug copy behavior was removed.'
    Assert-True (-not $projectSource.Contains('StagingForge') -and -not $projectSource.Contains('D:\Staging Area')) 'The hard-coded Release staging path remains active.'

    New-Item -ItemType Directory -Path $successOutput -Force | Out-Null
    $successArchive = Join-Path $successOutput $expectedArchiveName
    [IO.File]::WriteAllText($successArchive, 'previous artifact')
    $successExit = Invoke-ReleaseTestCommand -Repository $repositoryRoot -OutputDirectory $successOutput -LogName 'success.log'
    Assert-True ($successExit -eq 0) "The complete Windows PowerShell 5.1 release command failed with exit code $successExit."
    Assert-True (Test-Path -LiteralPath $successArchive -PathType Leaf) 'The versioned release archive was not produced.'
    Assert-True ([IO.File]::ReadAllText($successArchive) -ne 'previous artifact') 'A successful release did not replace the previous same-version artifact.'

    $archive = [IO.Compression.ZipFile]::OpenRead($successArchive)
    try {
        $archiveEntries = @($archive.Entries | ForEach-Object { $_.FullName })
        Assert-ExactReleaseMembership -ActualEntries $archiveEntries -ExpectedEntries $expectedEntries -Location 'Regression release archive'
    }
    finally {
        $archive.Dispose()
    }

    $stageRoot = Join-Path $testRoot 'validated-stage'
    [IO.Compression.ZipFile]::ExtractToDirectory($successArchive, $stageRoot)
    $releaseAssembly = Join-Path $repositoryRoot 'bin\Release\ODT-ItemInfo-4.0\ODT-ItemInfo-4.0.dll'
    Assert-ReleaseArchive -ArchivePath $successArchive -StageRoot $stageRoot -ReleaseAssemblyPath $releaseAssembly -ExpectedAssemblyName 'ODT-ItemInfo-4.0' -ExpectedEntries $expectedEntries

    $versionRepository = Copy-ReleaseTestRepository -Name 'version-failure'
    $versionProject = Join-Path $versionRepository 'ODT-ItemInfo-4.0.csproj'
    [IO.File]::WriteAllText($versionProject, ([IO.File]::ReadAllText($versionProject).Replace('<Version>2.0.14</Version>', '<Version>9.9.9</Version>')))
    $versionOutput = Join-Path $versionRepository 'release-output'
    $versionExit = Invoke-ReleaseTestCommand -Repository $versionRepository -OutputDirectory $versionOutput -LogName 'version-failure.log'
    Assert-True ($versionExit -ne 0) 'Mismatched project and runtime metadata versions were accepted.'
    Assert-True ([IO.File]::ReadAllText((Join-Path $testRoot 'version-failure.log')).Contains('does not equal runtime metadata Version')) 'The version failure did not reach the exact metadata equality gate.'
    Assert-FailedRunIsClean -OutputDirectory $versionOutput

    $allowlistRepository = Copy-ReleaseTestRepository -Name 'allowlist-failure'
    Remove-Item -LiteralPath (Join-Path $allowlistRepository 'LICENSE') -Force
    $allowlistOutput = Join-Path $allowlistRepository 'release-output'
    $allowlistExit = Invoke-ReleaseTestCommand -Repository $allowlistRepository -OutputDirectory $allowlistOutput -LogName 'allowlist-failure.log'
    Assert-True ($allowlistExit -ne 0) 'A missing allowlisted file was accepted.'
    Assert-True ([IO.File]::ReadAllText((Join-Path $testRoot 'allowlist-failure.log')).Contains('LICENSE')) 'The allowlist failure did not identify the missing release member.'
    Assert-FailedRunIsClean -OutputDirectory $allowlistOutput

    $jsonRepository = Copy-ReleaseTestRepository -Name 'json-preservation-failure'
    [IO.File]::WriteAllText((Join-Path $jsonRepository 'config\bsgblacklist.json'), '{ invalid json')
    $jsonOutput = Join-Path $jsonRepository 'release-output'
    New-Item -ItemType Directory -Path $jsonOutput -Force | Out-Null
    $preservedArchive = Join-Path $jsonOutput $expectedArchiveName
    $preservedBytes = [Text.Encoding]::UTF8.GetBytes('known validated artifact')
    [IO.File]::WriteAllBytes($preservedArchive, $preservedBytes)
    $jsonExit = Invoke-ReleaseTestCommand -Repository $jsonRepository -OutputDirectory $jsonOutput -LogName 'json-preservation-failure.log'
    Assert-True ($jsonExit -ne 0) 'Invalid release JSON was accepted.'
    Assert-True ([IO.File]::ReadAllText((Join-Path $testRoot 'json-preservation-failure.log')).Contains('Release JSON is invalid')) 'The JSON failure did not reach the archive JSON gate.'
    Assert-True ([Convert]::ToBase64String([IO.File]::ReadAllBytes($preservedArchive)) -eq [Convert]::ToBase64String($preservedBytes)) 'A failed release replaced the previous validated artifact.'
    Assert-FailedRunIsClean -OutputDirectory $jsonOutput

    $entrySources = @($expectedEntries | ForEach-Object {
        [pscustomobject]@{ Source = Join-Path $stageRoot $_.Replace('/', '\'); Name = $_ }
    })
    $validationDirectoriesBefore = @(Get-ReleaseValidationDirectories)

    $pathRepository = Copy-ReleaseTestRepository -Name 'path-failure'
    $pathOutput = Join-Path $pathRepository 'release-output'
    New-Item -ItemType Directory -Path $pathOutput -Force | Out-Null
    $pathPreservedArchive = Join-Path $pathOutput $expectedArchiveName
    [IO.File]::WriteAllBytes($pathPreservedArchive, $preservedBytes)
    $pathExit = Invoke-ReleaseTestCommand -Repository $pathRepository -OutputDirectory $pathOutput -LogName 'path-failure.log' -Fault 'UnsafePath'
    Assert-True ($pathExit -ne 0) 'The release command accepted an unsafe archive path.'
    Assert-True ([IO.File]::ReadAllText((Join-Path $testRoot 'path-failure.log')).Contains('unsafe path')) 'The path failure did not reach archive path validation.'
    Assert-True ([Convert]::ToBase64String([IO.File]::ReadAllBytes($pathPreservedArchive)) -eq [Convert]::ToBase64String($preservedBytes)) 'The path failure replaced the previous validated artifact.'
    Assert-FailedRunIsClean -OutputDirectory $pathOutput
    Assert-NoNewValidationDirectories -Before $validationDirectoriesBefore

    $collisionZip = Join-Path $testRoot 'collision.zip'
    $collisionEntries = @($entrySources + [pscustomobject]@{ Source = $entrySources[0].Source; Name = $entrySources[0].Name.ToUpperInvariant() })
    New-TestZip -Path $collisionZip -Entries $collisionEntries
    Assert-ThrowsLike -Pattern 'case-insensitive path collision' -Message 'A case-insensitive archive collision was accepted.' -Action {
        Assert-ReleaseArchive -ArchivePath $collisionZip -StageRoot $stageRoot -ReleaseAssemblyPath $releaseAssembly -ExpectedAssemblyName 'ODT-ItemInfo-4.0' -ExpectedEntries $expectedEntries
    }

    $duplicateZip = Join-Path $testRoot 'duplicate.zip'
    New-TestZip -Path $duplicateZip -Entries @($entrySources + $entrySources[0])
    Assert-ThrowsLike -Pattern 'duplicate entry' -Message 'An exact duplicate archive entry was accepted.' -Action {
        Assert-ReleaseArchive -ArchivePath $duplicateZip -StageRoot $stageRoot -ReleaseAssemblyPath $releaseAssembly -ExpectedAssemblyName 'ODT-ItemInfo-4.0' -ExpectedEntries $expectedEntries
    }

    $byteRepository = Copy-ReleaseTestRepository -Name 'byte-failure'
    $byteOutput = Join-Path $byteRepository 'release-output'
    New-Item -ItemType Directory -Path $byteOutput -Force | Out-Null
    $bytePreservedArchive = Join-Path $byteOutput $expectedArchiveName
    [IO.File]::WriteAllBytes($bytePreservedArchive, $preservedBytes)
    $byteExit = Invoke-ReleaseTestCommand -Repository $byteRepository -OutputDirectory $byteOutput -LogName 'byte-failure.log' -Fault 'ByteIdentity'
    Assert-True ($byteExit -ne 0) 'The release command accepted archived bytes that differ from staging.'
    Assert-True ([IO.File]::ReadAllText((Join-Path $testRoot 'byte-failure.log')).Contains('Archived bytes do not match staged bytes')) 'The byte failure did not reach staged/archive SHA-256 validation.'
    Assert-True ([Convert]::ToBase64String([IO.File]::ReadAllBytes($bytePreservedArchive)) -eq [Convert]::ToBase64String($preservedBytes)) 'The byte failure replaced the previous validated artifact.'
    Assert-FailedRunIsClean -OutputDirectory $byteOutput
    Assert-NoNewValidationDirectories -Before $validationDirectoriesBefore

    Write-Host 'PASS: Windows PowerShell 5.1 release success, failure, validation, preservation, and cleanup gates.'
    $testPassed = $true
}
finally {
    if ($testPassed -and (Test-Path -LiteralPath $testRoot)) {
        Remove-Item -LiteralPath $testRoot -Recurse -Force
    }
}
