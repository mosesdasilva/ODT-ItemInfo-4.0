[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $OutputPath
)

$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null
Set-Content -LiteralPath (Join-Path $OutputPath 'ODT-ItemInfo-4.0.dll') -Value 'synthetic mod assembly'
Write-Output 'Synthetic build completed.'
