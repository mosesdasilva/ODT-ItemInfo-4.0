[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$sentinelRoot = Join-Path $repositoryRoot 'artifacts\tmp\build-content-regression'
$sentinel = Join-Path $sentinelRoot 'must-not-copy.txt'
$output = Join-Path $repositoryRoot 'bin\Release\ODT-ItemInfo-4.0'

New-Item -ItemType Directory -Path $sentinelRoot -Force | Out-Null
Set-Content -LiteralPath $sentinel -Value 'repository-local artifact'
if (Test-Path -LiteralPath $output) {
    Remove-Item -LiteralPath $output -Recurse -Force
}

& dotnet build (Join-Path $repositoryRoot 'ODT-ItemInfo-4.0.sln') `
    --configuration Release `
    --nologo `
    --no-restore `
    --property:EnableForgeStaging=false
if ($LASTEXITCODE -ne 0) {
    throw "Release build failed with exit code $LASTEXITCODE."
}
if (-not (Test-Path -LiteralPath (Join-Path $output 'ODT-ItemInfo-4.0.dll') -PathType Leaf)) {
    throw 'Release build did not produce the mod assembly.'
}
if (Test-Path -LiteralPath (Join-Path $output 'artifacts')) {
    throw 'Repository-local artifacts leaked into Release build output.'
}

Remove-Item -LiteralPath $sentinelRoot -Recurse -Force
Write-Host 'PASS: repository-local artifacts are excluded from Release build output.'
