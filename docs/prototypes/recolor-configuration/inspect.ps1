# THROWAWAY PROTOTYPE: interactively inspect the proposed RarityRecolor data shape.
$ErrorActionPreference = 'Stop'
$prototypePath = Join-Path $PSScriptRoot 'rarity-recolor.prototype.json'
$state = Get-Content -LiteralPath $prototypePath -Raw | ConvertFrom-Json
$basis = @('TraderTier', 'TraderBuyValue')
$weaponModes = @('Inherit', 'TraderTier', 'WeaponCategory')

function Step-Value([string] $current, [string[]] $values) {
    $index = [Array]::IndexOf($values, $current)
    return $values[($index + 1) % $values.Count]
}

function Show-State($currentState) {
    Clear-Host
    Write-Host 'Consolidated Recolor Configuration' -ForegroundColor Cyan
    Write-Host 'THROWAWAY PROTOTYPE — no files are modified.' -ForegroundColor DarkGray
    Write-Host
    $currentState | ConvertTo-Json -Depth 10
    Write-Host
    Write-Host '[b]' -NoNewline -ForegroundColor Yellow
    Write-Host ' cycle basis  ' -NoNewline -ForegroundColor DarkGray
    Write-Host '[w]' -NoNewline -ForegroundColor Yellow
    Write-Host ' cycle weapon mode  ' -NoNewline -ForegroundColor DarkGray
    Write-Host '[f]' -NoNewline -ForegroundColor Yellow
    Write-Host ' toggle flea warning  ' -NoNewline -ForegroundColor DarkGray
    Write-Host '[q]' -NoNewline -ForegroundColor Yellow
    Write-Host ' quit' -ForegroundColor DarkGray
}

while ($true) {
    Show-State $state
    $key = [Console]::ReadKey($true).KeyChar.ToString().ToLowerInvariant()
    switch ($key) {
        'b' { $state.basis = Step-Value $state.basis $basis }
        'w' { $state.specializedClassifiers.weapons.mode = Step-Value $state.specializedClassifiers.weapons.mode $weaponModes }
        'f' { $state.fleaBanWarning.enabled = -not $state.fleaBanWarning.enabled }
        'q' { break }
    }
    if ($key -eq 'q') { break }
}
