param(
    [ValidateSet("A", "B", "C")]
    [string]$Variant = "A",

    [switch]$NoPrompt
)

$ErrorActionPreference = "Stop"
$prototypePath = Join-Path $PSScriptRoot "palettes.prototype.json"
$prototype = Get-Content -Raw $prototypePath | ConvertFrom-Json
$variantIndex = [Array]::FindIndex(
    [object[]]$prototype.variants,
    [Predicate[object]] { param($candidate) $candidate.key -eq $Variant }
)

$nativeRgb = @{
    default    = "7F7F7F"
    grey       = "1D1D1D"
    green      = "152D00"
    yellow     = "686628"
    blue       = "1C4156"
    violet     = "4C2A55"
    orange     = "3C1900"
    red        = "6D2418"
    tracerRed  = "FF3C3C"
}

function Get-Swatch([string]$colorName) {
    $hex = $nativeRgb[$colorName]
    $red = [Convert]::ToInt32($hex.Substring(0, 2), 16)
    $green = [Convert]::ToInt32($hex.Substring(2, 2), 16)
    $blue = [Convert]::ToInt32($hex.Substring(4, 2), 16)
    $escape = [char]27
    return "$escape[48;2;$red;$green;${blue}m        $escape[0m"
}

function Show-Variant([int]$index) {
    if (-not $NoPrompt) {
        Clear-Host
    }

    $selected = $prototype.variants[$index]
    Write-Host "THROWAWAY PROTOTYPE - Weapon Category Color Map"
    Write-Host "Variant $($selected.key) - $($selected.name)"
    Write-Host ""
    Write-Host ("{0,-20} {1,-18} {2,-12} {3,5}" -f "Contextual label", "Config key", "Color", "SPT")
    Write-Host ("{0,-20} {1,-18} {2,-12} {3,5}" -f "------------------", "----------", "-----", "---")

    foreach ($category in $prototype.categories) {
        $colorName = $selected.colors.($category.key)
        $swatch = Get-Swatch $colorName
        Write-Host ("{0,-20} {1,-18} {2} {3,-11} {4,5}" -f $category.label, $category.key, $swatch, $colorName, $category.count)
    }

    Write-Host ""
    Write-Host "$($prototype.excluded.label): $($prototype.excluded.behavior) ($($prototype.excluded.count) templates)"
    Write-Host "$($prototype.warning.label): $(Get-Swatch $prototype.warning.color) $($prototype.warning.color) (independent warning)"

    if (-not $NoPrompt) {
        Write-Host ""
        Write-Host "Left/Right: compare variants    Q: quit"
    }
}

do {
    Show-Variant $variantIndex
    if ($NoPrompt) {
        break
    }

    $key = [Console]::ReadKey($true).Key
    switch ($key) {
        "LeftArrow"  { $variantIndex = ($variantIndex - 1 + $prototype.variants.Count) % $prototype.variants.Count }
        "RightArrow" { $variantIndex = ($variantIndex + 1) % $prototype.variants.Count }
        "Q"          { return }
    }
} while ($true)
