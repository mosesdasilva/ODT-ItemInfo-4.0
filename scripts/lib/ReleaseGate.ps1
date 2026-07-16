Set-StrictMode -Version Latest

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type -AssemblyName System.Web.Extensions

function Get-ReleaseFileHash {
    param([Parameter(Mandatory = $true)][string] $Path)

    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
}

function Get-ExpectedReleaseEntries {
    param(
        [Parameter(Mandatory = $true)]
        [string] $AssemblyName
    )

    $modRoot = 'SPT/user/mods/ODT-ItemInfo-4.0'
    return @(
        "$modRoot/$AssemblyName.dll",
        "$modRoot/LICENSE",
        "$modRoot/config/bsgblacklist.json",
        "$modRoot/config/config.json",
        "$modRoot/config/translations.json"
    )
}

function Assert-SafeReleaseEntryPath {
    param([Parameter(Mandatory = $true)][string] $EntryPath)

    if ([string]::IsNullOrWhiteSpace($EntryPath)) {
        throw 'Release archive contains an empty path.'
    }

    $normalized = $EntryPath.Replace('\', '/')
    if ($normalized.StartsWith('/') -or $normalized -match '^[A-Za-z]:' -or [IO.Path]::IsPathRooted($EntryPath)) {
        throw "Release archive contains a rooted path: $EntryPath"
    }

    $segments = @($normalized.Split('/'))
    if ($segments.Count -eq 0 -or $segments -contains '' -or $segments -contains '.' -or $segments -contains '..') {
        throw "Release archive contains an unsafe path: $EntryPath"
    }

    return $normalized
}

function Assert-ReleaseJsonFile {
    param([Parameter(Mandatory = $true)][string] $Path)

    try {
        $parser = New-Object System.Web.Script.Serialization.JavaScriptSerializer
        $parser.MaxJsonLength = [int]::MaxValue
        $null = $parser.DeserializeObject([IO.File]::ReadAllText($Path))
    }
    catch {
        throw "Release JSON is invalid: $Path. $($_.Exception.Message)"
    }
}

function Get-ReleaseStageEntries {
    param([Parameter(Mandatory = $true)][string] $StageRoot)

    $stagePath = [IO.Path]::GetFullPath($StageRoot).TrimEnd('\')
    return @(
        Get-ChildItem -LiteralPath $stagePath -File -Recurse | ForEach-Object {
            $_.FullName.Substring($stagePath.Length + 1).Replace('\', '/')
        }
    )
}

function Assert-ExactReleaseMembership {
    param(
        [Parameter(Mandatory = $true)][string[]] $ActualEntries,
        [Parameter(Mandatory = $true)][string[]] $ExpectedEntries,
        [Parameter(Mandatory = $true)][string] $Location
    )

    $actualSet = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($entry in $ActualEntries) {
        if (-not $actualSet.Add($entry)) {
            throw "$Location contains duplicate entry '$entry'."
        }
    }

    $caseInsensitiveSet = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($entry in $ActualEntries) {
        if (-not $caseInsensitiveSet.Add($entry)) {
            throw "$Location contains a case-insensitive path collision at '$entry'."
        }
    }

    $expectedSet = [Collections.Generic.HashSet[string]]::new($ExpectedEntries, [StringComparer]::Ordinal)
    $missing = @($ExpectedEntries | Where-Object { -not $actualSet.Contains($_) })
    $unexpected = @($ActualEntries | Where-Object { -not $expectedSet.Contains($_) })
    if ($missing.Count -gt 0 -or $unexpected.Count -gt 0 -or $ActualEntries.Count -ne $ExpectedEntries.Count) {
        throw "$Location does not match the exact release allowlist. Missing: [$($missing -join ', ')] Unexpected: [$($unexpected -join ', ')]"
    }
}

function Assert-ReleaseArchive {
    param(
        [Parameter(Mandatory = $true)][string] $ArchivePath,
        [Parameter(Mandatory = $true)][string] $StageRoot,
        [Parameter(Mandatory = $true)][string] $ReleaseAssemblyPath,
        [Parameter(Mandatory = $true)][string] $ExpectedAssemblyName,
        [Parameter(Mandatory = $true)][string[]] $ExpectedEntries
    )

    $archive = [IO.Compression.ZipFile]::OpenRead($ArchivePath)
    $validationRoot = Join-Path ([IO.Path]::GetTempPath()) ('odt-release-validation-' + [Guid]::NewGuid().ToString('N'))
    try {
        $entryNames = @()
        foreach ($entry in $archive.Entries) {
            $entryNames += Assert-SafeReleaseEntryPath -EntryPath $entry.FullName
        }
        Assert-ExactReleaseMembership -ActualEntries $entryNames -ExpectedEntries $ExpectedEntries -Location 'Release archive'
        Assert-ExactReleaseMembership -ActualEntries @(Get-ReleaseStageEntries -StageRoot $StageRoot) -ExpectedEntries $ExpectedEntries -Location 'Release staging directory'

        New-Item -ItemType Directory -Path $validationRoot -Force | Out-Null
        foreach ($entry in $archive.Entries) {
            $normalized = $entry.FullName.Replace('\', '/')
            $destination = Join-Path $validationRoot $normalized.Replace('/', '\')
            New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
            [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $destination, $false)

            $stagedPath = Join-Path $StageRoot $normalized.Replace('/', '\')
            if ((Get-ReleaseFileHash -Path $stagedPath) -ne (Get-ReleaseFileHash -Path $destination)) {
                throw "Archived bytes do not match staged bytes for '$normalized'."
            }
        }

        foreach ($jsonEntry in @($ExpectedEntries | Where-Object { $_.EndsWith('.json', [StringComparison]::Ordinal) })) {
            Assert-ReleaseJsonFile -Path (Join-Path $validationRoot $jsonEntry.Replace('/', '\'))
        }

        $assemblyEntry = $ExpectedEntries | Where-Object { $_.EndsWith('/' + $ExpectedAssemblyName + '.dll', [StringComparison]::Ordinal) }
        if (@($assemblyEntry).Count -ne 1) {
            throw "Expected exactly one '$ExpectedAssemblyName.dll' entry in the release allowlist."
        }
        $stagedAssembly = Join-Path $StageRoot $assemblyEntry.Replace('/', '\')
        $archivedAssembly = Join-Path $validationRoot $assemblyEntry.Replace('/', '\')
        if ((Get-Item -LiteralPath $stagedAssembly).Length -le 0) {
            throw 'The staged mod assembly is empty.'
        }
        if ((Get-ReleaseFileHash -Path $ReleaseAssemblyPath) -ne (Get-ReleaseFileHash -Path $stagedAssembly)) {
            throw 'The staged mod assembly is not byte-identical to the Release build output.'
        }

        $releaseIdentity = [Reflection.AssemblyName]::GetAssemblyName($ReleaseAssemblyPath)
        $archivedIdentity = [Reflection.AssemblyName]::GetAssemblyName($archivedAssembly)
        if ($releaseIdentity.Name -ne $ExpectedAssemblyName -or $archivedIdentity.FullName -ne $releaseIdentity.FullName) {
            throw "Archived assembly identity '$($archivedIdentity.FullName)' does not match Release output '$($releaseIdentity.FullName)'."
        }
    }
    finally {
        $archive.Dispose()
        if (Test-Path -LiteralPath $validationRoot) {
            Remove-Item -LiteralPath $validationRoot -Recurse -Force
        }
    }
}

function New-ReleaseArchive {
    param(
        [Parameter(Mandatory = $true)][string] $StageRoot,
        [Parameter(Mandatory = $true)][string] $ArchivePath,
        [Parameter(Mandatory = $true)][string[]] $ExpectedEntries
    )

    $archive = [IO.Compression.ZipFile]::Open($ArchivePath, [IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($entryPath in $ExpectedEntries) {
            $sourcePath = Join-Path $StageRoot $entryPath.Replace('/', '\')
            [IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
                $archive,
                $sourcePath,
                $entryPath,
                [IO.Compression.CompressionLevel]::Optimal
            ) | Out-Null
        }
    }
    finally {
        $archive.Dispose()
    }
}

function Invoke-ReleaseArchiveTestFault {
    param(
        [Parameter(Mandatory = $true)][string] $ArchivePath,
        [Parameter(Mandatory = $true)][ValidateSet('UnsafePath', 'ByteIdentity')][string] $Fault
    )

    $archive = [IO.Compression.ZipFile]::Open($ArchivePath, [IO.Compression.ZipArchiveMode]::Update)
    try {
        if ($Fault -eq 'UnsafePath') {
            $entry = $archive.CreateEntry('../escape.txt')
            $writer = New-Object IO.StreamWriter($entry.Open())
            try {
                $writer.Write('unsafe')
            }
            finally {
                $writer.Dispose()
            }
            return
        }

        $entryPath = 'SPT/user/mods/ODT-ItemInfo-4.0/config/config.json'
        $entry = $archive.GetEntry($entryPath)
        if ($null -eq $entry) {
            throw "Could not inject the byte-identity test fault; archive entry is missing: $entryPath"
        }
        $entry.Delete()
        $replacement = $archive.CreateEntry($entryPath)
        $writer = New-Object IO.StreamWriter($replacement.Open())
        try {
            $writer.Write('{}')
        }
        finally {
            $writer.Dispose()
        }
    }
    finally {
        $archive.Dispose()
    }
}
