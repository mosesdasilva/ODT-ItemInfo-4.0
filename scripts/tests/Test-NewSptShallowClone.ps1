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
$junction = Get-Item -LiteralPath (Join-Path $target 'EscapeFromTarkov_Data\StreamingAssets') -Force
$hardlink = Get-Item -LiteralPath (Join-Path $target 'EscapeFromTarkov_Data\globalgamemanagers') -Force

if ((Get-Content -Raw -LiteralPath $copiedMutable).Trim() -ne 'mutable source content') {
    throw 'Mutable SPT content was not copied.'
}
if ((Get-Content -Raw -LiteralPath $copiedManaged).Trim() -ne 'managed copy') {
    throw 'EscapeFromTarkov_Data\Managed was not copied.'
}
if ($junction.LinkType -ne 'Junction') {
    throw "Expected a directory junction, found '$($junction.LinkType)'."
}
if ($hardlink.LinkType -ne 'HardLink') {
    throw "Expected a file hardlink, found '$($hardlink.LinkType)'."
}

Set-Content -LiteralPath $copiedMutable -Value 'target-only mutable change'
Set-Content -LiteralPath $copiedManaged -Value 'target-only managed change'
if ((Get-Content -Raw -LiteralPath (Join-Path $source 'SPT\user\mods\source-mod.txt')).Trim() -ne 'mutable source content') {
    throw 'Mutable content is shared with the source installation.'
}
if ((Get-Content -Raw -LiteralPath (Join-Path $sourceData 'Managed\Assembly-CSharp.dll')).Trim() -ne 'managed copy') {
    throw 'Managed content is shared with the source installation.'
}

$symbolicLinks = @(Get-ChildItem -LiteralPath $target -Recurse -Force | Where-Object LinkType -eq 'SymbolicLink')
if ($symbolicLinks.Count -ne 0) {
    throw "The clone contains $($symbolicLinks.Count) symbolic links."
}

[IO.Directory]::Delete($junction.FullName)
Remove-Item -LiteralPath $testRoot -Recurse -Force
Write-Host 'PASS: non-admin shallow clone contract satisfied.'
