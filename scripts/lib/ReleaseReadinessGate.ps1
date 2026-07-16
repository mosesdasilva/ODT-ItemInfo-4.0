Set-StrictMode -Version Latest

function Get-ReleaseReadinessInputIdentity {
    param(
        [Parameter(Mandatory)]
        [string] $RepositoryRoot
    )

    $root = [IO.Path]::GetFullPath($RepositoryRoot).TrimEnd('\')
    $paths = [Collections.Generic.List[string]]::new()
    foreach ($name in @('LICENSE', 'global.json', 'NuGet.config')) {
        $path = Join-Path $root $name
        if (Test-Path -LiteralPath $path -PathType Leaf) {
            $paths.Add($path)
        }
    }
    foreach ($pattern in @('*.csproj', '*.sln', '*.props', '*.targets')) {
        Get-ChildItem -LiteralPath $root -Filter $pattern -File -Recurse -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName.Substring($root.Length + 1) -notmatch '(?:^|[\\/])(?:bin|obj|artifacts|\.git)[\\/]' } |
            ForEach-Object { $paths.Add($_.FullName) }
    }
    Get-ChildItem -LiteralPath $root -Filter '*.cs' -File -Recurse -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName.Substring($root.Length + 1) -notmatch '(?:^|[\\/])(?:bin|obj|artifacts|\.git)[\\/]' } |
        ForEach-Object { $paths.Add($_.FullName) }
    foreach ($directory in @('config', 'scripts')) {
        $path = Join-Path $root $directory
        if (Test-Path -LiteralPath $path -PathType Container) {
            Get-ChildItem -LiteralPath $path -File -Recurse |
                Where-Object { $_.Extension -in @('.json', '.ps1') } |
                ForEach-Object { $paths.Add($_.FullName) }
        }
    }

    $files = @(
        $paths |
            Select-Object -Unique |
            ForEach-Object {
                [pscustomobject]@{
                    path = $_.Substring($root.Length + 1).Replace('\', '/')
                    sha256 = (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash
                }
            } |
            Sort-Object path
    )
    $manifest = ($files | ForEach-Object { "$($_.path)`0$($_.sha256)" }) -join "`n"
    $hasher = [Security.Cryptography.SHA256]::Create()
    try {
        $manifestHash = [BitConverter]::ToString($hasher.ComputeHash([Text.Encoding]::UTF8.GetBytes($manifest))).Replace('-', '')
    }
    finally {
        $hasher.Dispose()
    }
    return [pscustomobject]@{
        sha256 = $manifestHash
        fileCount = $files.Count
        files = $files
    }
}

function Write-ReleaseReadinessRecord {
    param(
        [Parameter(Mandatory)]
        [string] $Path,

        [Parameter(Mandatory)]
        [string] $SourceRevision,

        [Parameter(Mandatory)]
        [object] $InputIdentity,

        [Parameter(Mandatory)]
        [object] $ReleaseCandidate,

        [Parameter(Mandatory)]
        [Collections.IDictionary] $Gates,

        [Parameter(Mandatory)]
        [string[]] $LogLocations
    )

    if ($ReleaseCandidate.fileCount -ne 5) {
        throw "A readiness record requires the exact five-file Release Candidate Artifact; found $($ReleaseCandidate.fileCount)."
    }
    $failedGates = @($Gates.GetEnumerator() | Where-Object { $_.Value.outcome -ne 'passed' })
    if ($failedGates.Count -gt 0) {
        throw "A passing readiness record cannot contain failed gates: $($failedGates.Name -join ', ')"
    }

    $record = [ordered]@{
        schemaVersion = 1
        outcome = 'passed'
        recordedAtUtc = [DateTime]::UtcNow.ToString('o')
        sourceRevision = $SourceRevision
        releaseInputIdentity = $InputIdentity
        releaseCandidate = $ReleaseCandidate
        gates = $Gates
        logLocations = $LogLocations
        invalidationRule = 'Any code, project, configuration, translation, license, dependency-version, release-script, archive, or installed-byte change invalidates this candidate and requires release readiness to restart.'
    }
    $parent = Split-Path -Parent $Path
    New-Item -ItemType Directory -Path $parent -Force | Out-Null
    $record | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $Path -Encoding UTF8
}
