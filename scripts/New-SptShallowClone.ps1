[CmdletBinding()]
param(
    [string] $SourcePath = 'C:\Games\SPT 4.0\4.0.13\Single Player Tarkov'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$targetPath = Join-Path $repositoryRoot 'artifacts\spt-test-clone'

. (Join-Path $PSScriptRoot 'lib\SptShallowClone.ps1')

New-SptShallowClone -SourcePath $SourcePath -TargetPath $targetPath
