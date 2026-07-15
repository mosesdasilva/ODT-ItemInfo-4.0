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
    if ([IO.Path]::GetPathRoot($source) -ne [IO.Path]::GetPathRoot($target)) {
        throw "Source and target must be on the same volume so shared files can use non-admin hardlinks. Source: $source Target: $target"
    }
    if ($target.StartsWith("$source\", [StringComparison]::OrdinalIgnoreCase)) {
        throw "Target must not be inside the source installation: $target"
    }
    if (Test-Path -LiteralPath $target) {
        throw "Target already exists; refusing to overwrite it: $target"
    }

    $targetParent = Split-Path -Parent $target
    New-Item -ItemType Directory -Path $targetParent -Force | Out-Null
    $staging = "$target.incomplete.$([Guid]::NewGuid().ToString('N'))"
    $createdJunctions = [Collections.Generic.List[string]]::new()

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

        Write-Host 'Creating non-admin links for shared game data...'
        Get-ChildItem -LiteralPath $sourceData -Force |
            Where-Object Name -ne 'Managed' |
            ForEach-Object {
                $destination = Join-Path $stagingData $_.Name
                if ($_.PSIsContainer) {
                    New-Item -ItemType Junction -Path $destination -Value $_.FullName | Out-Null
                    $createdJunctions.Add($destination)
                }
                elseif (-not ($_.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
                    New-Item -ItemType HardLink -Path $destination -Value $_.FullName | Out-Null
                }
                else {
                    throw "Unsupported source reparse point: $($_.FullName)"
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
                throw "Expected a junction for shared directory '$($entry.Name)', found '$($clonedEntry.LinkType)'."
            }
            elseif (-not $entry.PSIsContainer -and $clonedEntry.LinkType -ne 'HardLink') {
                throw "Expected a hardlink for shared file '$($entry.Name)', found '$($clonedEntry.LinkType)'."
            }
        }

        $symbolicLinks = @(Get-ChildItem -LiteralPath $staging -Recurse -Force | Where-Object LinkType -eq 'SymbolicLink')
        if ($symbolicLinks.Count -ne 0) {
            throw "Clone validation found $($symbolicLinks.Count) symbolic links."
        }

        Move-Item -LiteralPath $staging -Destination $target
        $copiedBytes = (Get-ChildItem -LiteralPath $target -Recurse -File -Force |
            Where-Object LinkType -ne 'HardLink' |
            Measure-Object -Property Length -Sum).Sum
        Write-Host ("Shallow clone complete: {0} ({1:N2} MB physically copied)" -f $target, ($copiedBytes / 1MB))
    }
    catch {
        $originalError = $_
        foreach ($junction in $createdJunctions) {
            if (Test-Path -LiteralPath $junction) {
                try {
                    [IO.Directory]::Delete($junction)
                }
                catch {
                    Write-Warning "Could not remove incomplete junction '$junction': $($_.Exception.Message)"
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
