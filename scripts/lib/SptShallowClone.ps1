Set-StrictMode -Version Latest

function New-SptShallowClone {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string] $SourcePath,

        [Parameter(Mandatory)]
        [string] $TargetPath
    )

    $ErrorActionPreference = 'Stop'
    $source = (Resolve-Path -LiteralPath $SourcePath).Path.TrimEnd('\')
    $sourceData = Join-Path $source 'EscapeFromTarkov_Data'
    $sourceManaged = Join-Path $sourceData 'Managed'

    if (-not (Test-Path -LiteralPath $sourceData -PathType Container)) {
        throw "Source is not a complete SPT installation; EscapeFromTarkov_Data is missing: $source"
    }
    if (-not (Test-Path -LiteralPath $sourceManaged -PathType Container)) {
        throw "Source is not a complete SPT installation; EscapeFromTarkov_Data\Managed is missing: $source"
    }

    $target = [IO.Path]::GetFullPath($TargetPath).TrimEnd('\')
    if ($target.StartsWith("$source\", [StringComparison]::OrdinalIgnoreCase)) {
        throw "Target must not be inside the source installation: $target"
    }
    if ($source.Equals($target, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Target must not be the source installation: $target"
    }
    if (Test-Path -LiteralPath $target) {
        throw "Target already exists; refusing to overwrite it: $target"
    }

    $targetParent = Split-Path -Parent $target
    New-Item -ItemType Directory -Path $targetParent -Force | Out-Null
    $staging = "$target.incomplete.$([Guid]::NewGuid().ToString('N'))"
    $createdLinks = [Collections.Generic.List[object]]::new()

    try {
        New-Item -ItemType Directory -Path $staging | Out-Null

        Write-Host "Copying mutable SPT content from '$source'..."
        Get-ChildItem -LiteralPath $source -Force |
            Where-Object Name -ne 'EscapeFromTarkov_Data' |
            ForEach-Object {
                Copy-Item -LiteralPath $_.FullName -Destination $staging -Recurse -Force
            }

        $stagingData = Join-Path $staging 'EscapeFromTarkov_Data'
        New-Item -ItemType Directory -Path $stagingData | Out-Null

        Write-Host 'Sharing immutable game data without administrator privileges...'
        Get-ChildItem -LiteralPath $sourceData -Force |
            Where-Object Name -ne 'Managed' |
            ForEach-Object {
                $destination = Join-Path $stagingData $_.Name
                if ($_.Attributes -band [IO.FileAttributes]::ReparsePoint) {
                    throw "Unsupported source reparse point: $($_.FullName)"
                }
                if ($_.PSIsContainer) {
                    New-Item -ItemType Junction -Path $destination -Value $_.FullName | Out-Null
                    $createdLinks.Add([pscustomobject]@{
                        Path = $destination
                        IsDirectory = $true
                    })
                }
                elseif ([IO.Path]::GetPathRoot($_.FullName).Equals([IO.Path]::GetPathRoot($destination), [StringComparison]::OrdinalIgnoreCase)) {
                    New-Item -ItemType HardLink -Path $destination -Value $_.FullName | Out-Null
                    $createdLinks.Add([pscustomobject]@{
                        Path = $destination
                        IsDirectory = $false
                    })
                }
                else {
                    Copy-Item -LiteralPath $_.FullName -Destination $destination -Force
                }
            }

        Write-Host 'Copying mutable Managed assemblies...'
        Copy-Item -LiteralPath $sourceManaged -Destination $stagingData -Recurse -Force

        $sourceRootNames = @(Get-ChildItem -LiteralPath $source -Force | ForEach-Object Name | Sort-Object)
        $stagingRootNames = @(Get-ChildItem -LiteralPath $staging -Force | ForEach-Object Name | Sort-Object)
        if (Compare-Object $sourceRootNames $stagingRootNames) {
            throw 'Top-level clone validation failed: source and target entries differ.'
        }

        $sourceDataEntries = @(Get-ChildItem -LiteralPath $sourceData -Force)
        $stagingDataNames = @(Get-ChildItem -LiteralPath $stagingData -Force | ForEach-Object Name | Sort-Object)
        $sourceDataNames = @($sourceDataEntries | ForEach-Object Name | Sort-Object)
        if (Compare-Object $sourceDataNames $stagingDataNames) {
            throw 'Game-data clone validation failed: source and target entries differ.'
        }

        foreach ($entry in $sourceDataEntries) {
            $clonedEntry = Get-Item -LiteralPath (Join-Path $stagingData $entry.Name) -Force
            if ($entry.Name -eq 'Managed') {
                if ($clonedEntry.Attributes -band [IO.FileAttributes]::ReparsePoint) {
                    throw 'Managed must be copied, not linked.'
                }
            }
            elseif ($entry.PSIsContainer -and $clonedEntry.LinkType -ne 'Junction') {
                throw "Expected a directory junction for shared entry '$($entry.Name)', found '$($clonedEntry.LinkType)'."
            }
            elseif (-not $entry.PSIsContainer) {
                $sameVolume = [IO.Path]::GetPathRoot($entry.FullName).Equals(
                    [IO.Path]::GetPathRoot($clonedEntry.FullName),
                    [StringComparison]::OrdinalIgnoreCase
                )
                if ($sameVolume -and $clonedEntry.LinkType -ne 'HardLink') {
                    throw "Expected a file hard link for same-volume shared entry '$($entry.Name)', found '$($clonedEntry.LinkType)'."
                }
                if (-not $sameVolume) {
                    if ($clonedEntry.Attributes -band [IO.FileAttributes]::ReparsePoint) {
                        throw "Expected a copied cross-volume shared file for '$($entry.Name)', found a reparse point."
                    }
                    if ((Get-FileHash -LiteralPath $entry.FullName -Algorithm SHA256).Hash -ne
                        (Get-FileHash -LiteralPath $clonedEntry.FullName -Algorithm SHA256).Hash) {
                        throw "Copied cross-volume shared file differs from its source: $($entry.Name)"
                    }
                }
            }
        }

        $marker = [ordered]@{
            schemaVersion = 1
            sourcePath = $source
            targetPath = $target
            createdAtUtc = [DateTime]::UtcNow.ToString('o')
        }
        $marker | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $staging '.odt-spt-test-clone.json') -Encoding UTF8

        Move-Item -LiteralPath $staging -Destination $target
        $copiedBytes = (Get-ChildItem -LiteralPath $target -Recurse -File -Force |
            Where-Object {
                -not ($_.Attributes -band [IO.FileAttributes]::ReparsePoint) -and $_.LinkType -ne 'HardLink'
            } |
            Measure-Object -Property Length -Sum).Sum
        Write-Host ("Shallow clone complete: {0} ({1:N2} MB physically copied)" -f $target, ($copiedBytes / 1MB))
    }
    catch {
        $originalError = $_
        foreach ($link in $createdLinks) {
            if (Test-Path -LiteralPath $link.Path) {
                try {
                    if ($link.IsDirectory) {
                        [IO.Directory]::Delete($link.Path)
                    }
                    else {
                        [IO.File]::Delete($link.Path)
                    }
                }
                catch {
                    Write-Warning "Could not remove incomplete shared link '$($link.Path)': $($_.Exception.Message)"
                }
            }
        }
        if (Test-Path -LiteralPath $staging) {
            try {
                Remove-Item -LiteralPath $staging -Recurse -Force
            }
            catch {
                Write-Warning "Could not remove incomplete staging directory '$staging': $($_.Exception.Message)"
            }
        }
        throw $originalError
    }
}
