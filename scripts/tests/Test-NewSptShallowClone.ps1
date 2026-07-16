[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$cloneLibrary = Join-Path $repositoryRoot 'scripts\lib\SptShallowClone.ps1'
$testRoot = Join-Path $repositoryRoot 'artifacts\tmp\shallow-clone-regression'

if (-not $testRoot.StartsWith($repositoryRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to use a test directory outside the repository: $testRoot"
}

if (Test-Path -LiteralPath $testRoot) {
    Remove-Item -LiteralPath $testRoot -Recurse -Force
}

$source = Join-Path $testRoot 'source'
$target = Join-Path $testRoot 'target'
$sourceData = Join-Path $source 'EscapeFromTarkov_Data'

New-Item -ItemType Directory -Path (Join-Path $source 'SPT\user\mods') -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $sourceData 'Managed') -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $sourceData 'StreamingAssets') -Force | Out-Null
Set-Content -LiteralPath (Join-Path $source 'SPT\user\mods\source-mod.txt') -Value 'mutable source content'
Set-Content -LiteralPath (Join-Path $sourceData 'Managed\Assembly-CSharp.dll') -Value 'managed copy'
Set-Content -LiteralPath (Join-Path $sourceData 'StreamingAssets\shared.bundle') -Value 'shared directory content'
Set-Content -LiteralPath (Join-Path $sourceData 'globalgamemanagers') -Value 'shared file content'

if (-not (Test-Path -LiteralPath $cloneLibrary -PathType Leaf)) {
    throw "Clone library is missing: $cloneLibrary"
}

. $cloneLibrary
New-SptShallowClone -SourcePath $source -TargetPath $target

$copiedMutable = Join-Path $target 'SPT\user\mods\source-mod.txt'
$copiedManaged = Join-Path $target 'EscapeFromTarkov_Data\Managed\Assembly-CSharp.dll'
$linkedDirectory = Get-Item -LiteralPath (Join-Path $target 'EscapeFromTarkov_Data\StreamingAssets') -Force
$linkedFile = Get-Item -LiteralPath (Join-Path $target 'EscapeFromTarkov_Data\globalgamemanagers') -Force
$cloneMarker = Join-Path $target '.odt-spt-test-clone.json'

if ((Get-Content -Raw -LiteralPath $copiedMutable).Trim() -ne 'mutable source content') {
    throw 'Mutable SPT content was not copied.'
}
if ((Get-Content -Raw -LiteralPath $copiedManaged).Trim() -ne 'managed copy') {
    throw 'EscapeFromTarkov_Data\Managed was not copied.'
}
if ($linkedDirectory.LinkType -ne 'Junction') {
    throw "Expected a directory junction, found '$($linkedDirectory.LinkType)'."
}
if ($linkedFile.LinkType -ne 'HardLink') {
    throw "Expected a same-volume file hard link, found '$($linkedFile.LinkType)'."
}
if (-not (Test-Path -LiteralPath $cloneMarker -PathType Leaf)) {
    throw 'Clone ownership marker was not created.'
}

$marker = Get-Content -Raw -LiteralPath $cloneMarker | ConvertFrom-Json
if ($marker.sourcePath -ne (Resolve-Path -LiteralPath $source).Path) {
    throw 'Clone ownership marker does not identify the source installation.'
}
if ($marker.targetPath -ne (Resolve-Path -LiteralPath $target).Path) {
    throw 'Clone ownership marker does not identify the target installation.'
}

Set-Content -LiteralPath $copiedMutable -Value 'target-only mutable change'
Set-Content -LiteralPath $copiedManaged -Value 'target-only managed change'
if ((Get-Content -Raw -LiteralPath (Join-Path $source 'SPT\user\mods\source-mod.txt')).Trim() -ne 'mutable source content') {
    throw 'Mutable content is shared with the source installation.'
}
if ((Get-Content -Raw -LiteralPath (Join-Path $sourceData 'Managed\Assembly-CSharp.dll')).Trim() -ne 'managed copy') {
    throw 'Managed content is shared with the source installation.'
}

$sharedDataEntries = @(Get-ChildItem -LiteralPath (Join-Path $target 'EscapeFromTarkov_Data') -Force |
    Where-Object Name -ne 'Managed')
foreach ($entry in $sharedDataEntries) {
    $expectedLinkType = if ($entry.PSIsContainer) { 'Junction' } else { 'HardLink' }
    if ($entry.LinkType -ne $expectedLinkType) {
        throw "Expected shared entry '$($entry.Name)' to use '$expectedLinkType', found '$($entry.LinkType)'."
    }
}

[IO.Directory]::Delete($linkedDirectory.FullName)
[IO.File]::Delete($linkedFile.FullName)
Remove-Item -LiteralPath $testRoot -Recurse -Force
Write-Host 'PASS: non-admin shallow clone contract satisfied with isolated mutable data and shared immutable data.'
