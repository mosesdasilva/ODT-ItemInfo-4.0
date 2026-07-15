[CmdletBinding()]
param(
    [ValidateSet('Ready', 'Fatal', 'Dependency', 'Configuration', 'Timeout')]
    [string] $Mode,

    [Parameter(Mandatory)]
    [string] $LogPath,

    [Parameter(Mandatory)]
    [string] $ClonePath
)

New-Item -ItemType Directory -Path (Split-Path -Parent $LogPath) -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $ClonePath 'Logs') -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $ClonePath 'BepInEx') -Force | Out-Null
Set-Content -LiteralPath (Join-Path $ClonePath 'Logs\fake-game.log') -Value 'current game log'
Set-Content -LiteralPath (Join-Path $ClonePath 'BepInEx\LogOutput.log') -Value 'current BepInEx log'
function Write-ServerMessage {
    param([string] $Message)
    Write-Output $Message
    Add-Content -LiteralPath $LogPath -Value $Message
}

switch ($Mode) {
    'Ready' {
        Write-ServerMessage '[Info][SPTarkov.Server.Core.Utils.App] Server has started, happy playing'
        Start-Sleep -Seconds 30
    }
    'Fatal' {
        Write-ServerMessage '[Fatal][Server] Critical exception, stopping server...'
        exit 1
    }
    'Dependency' {
        Write-ServerMessage "Could not load file or assembly 'Missing.Dependency'"
        exit 1
    }
    'Configuration' {
        Write-ServerMessage 'ODT Item Info config file was not found.'
        exit 1
    }
    'Timeout' {
        Write-ServerMessage '[Info][Server] Still starting'
        Start-Sleep -Seconds 30
    }
}
