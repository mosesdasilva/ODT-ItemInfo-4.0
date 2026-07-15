Set-StrictMode -Version Latest

function Get-FullPath {
    param(
        [Parameter(Mandatory)]
        [string] $Path
    )

    return [IO.Path]::GetFullPath($Path).TrimEnd('\')
}

function Assert-SptTestClone {
    param(
        [Parameter(Mandatory)]
        [string] $ClonePath,

        [Parameter(Mandatory)]
        [string] $RepositoryRoot
    )

    $clone = Get-FullPath $ClonePath
    $artifactsRoot = Get-FullPath (Join-Path $RepositoryRoot 'artifacts')
    if (-not $clone.StartsWith("$artifactsRoot\", [StringComparison]::OrdinalIgnoreCase)) {
        throw "SPT Test Clone must remain under the repository-local artifacts directory. Clone: $clone Artifacts: $artifactsRoot"
    }

    if (Test-Path -LiteralPath $artifactsRoot) {
        $artifactsItem = Get-Item -LiteralPath $artifactsRoot -Force
        if ($artifactsItem.Attributes -band [IO.FileAttributes]::ReparsePoint) {
            throw "The repository artifacts directory must not be a junction or symbolic-link alias: $artifactsRoot"
        }
    }

    $pathToCheck = $artifactsRoot
    $relativeClonePath = $clone.Substring($artifactsRoot.Length).TrimStart('\')
    foreach ($segment in $relativeClonePath.Split('\')) {
        $pathToCheck = Join-Path $pathToCheck $segment
        if (Test-Path -LiteralPath $pathToCheck) {
            $pathItem = Get-Item -LiteralPath $pathToCheck -Force
            if ($pathItem.Attributes -band [IO.FileAttributes]::ReparsePoint) {
                throw "SPT Test Clone path must not contain a junction or symbolic-link alias: $pathToCheck"
            }
        }
    }
    if (-not (Test-Path -LiteralPath $clone -PathType Container)) {
        throw "SPT Test Clone does not exist: $clone"
    }

    $markerPath = Join-Path $clone '.odt-spt-test-clone.json'
    if (-not (Test-Path -LiteralPath $markerPath -PathType Leaf)) {
        throw "SPT Test Clone ownership marker is missing: $markerPath"
    }

    $marker = Get-Content -Raw -LiteralPath $markerPath | ConvertFrom-Json
    $markedTarget = Get-FullPath ([string] $marker.targetPath)
    $source = Get-FullPath ([string] $marker.sourcePath)
    if (-not $markedTarget.Equals($clone, [StringComparison]::OrdinalIgnoreCase)) {
        throw "SPT Test Clone marker target does not match the requested clone. Marker: $markedTarget Requested: $clone"
    }
    if ($source.Equals($clone, [StringComparison]::OrdinalIgnoreCase) -or
        $clone.StartsWith("$source\", [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to use the source/everyday SPT installation as the test clone. Source: $source Clone: $clone"
    }
    if (-not (Test-Path -LiteralPath $source -PathType Container)) {
        throw "SPT Test Clone source recorded in the marker no longer exists: $source"
    }

    foreach ($requiredDirectory in @('SPT\user\mods', 'SPT\user\logs')) {
        $requiredPath = Join-Path $clone $requiredDirectory
        if (-not (Test-Path -LiteralPath $requiredPath -PathType Container)) {
            throw "SPT Test Clone is incomplete; required directory is missing: $requiredPath"
        }
    }

    return $clone
}

function Reset-SptTestClone {
    param(
        [Parameter(Mandatory)]
        [string] $ClonePath
    )

    $clone = Get-FullPath $ClonePath
    $modsPath = Join-Path $clone 'SPT\user\mods'
    if (-not $modsPath.StartsWith("$clone\", [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to reset a mods path outside the SPT Test Clone: $modsPath"
    }
    if (Test-Path -LiteralPath $modsPath -PathType Container) {
        Get-ChildItem -LiteralPath $modsPath -Force | Remove-Item -Recurse -Force
    }
    else {
        New-Item -ItemType Directory -Path $modsPath -Force | Out-Null
    }

    foreach ($logDirectory in @('SPT\user\logs', 'Logs')) {
        $logPath = Join-Path $clone $logDirectory
        if (Test-Path -LiteralPath $logPath -PathType Container) {
            Get-ChildItem -LiteralPath $logPath -Force | Remove-Item -Recurse -Force
        }
        else {
            New-Item -ItemType Directory -Path $logPath -Force | Out-Null
        }
    }

    $bepInExLog = Join-Path $clone 'BepInEx\LogOutput.log'
    if (Test-Path -LiteralPath $bepInExLog -PathType Leaf) {
        Remove-Item -LiteralPath $bepInExLog -Force
    }
}

function Set-SptTestClonePort {
    param(
        [Parameter(Mandatory)]
        [string] $ClonePath,

        [Parameter(Mandatory)]
        [ValidateRange(1, 65535)]
        [int] $ServerPort
    )

    $activePort = [Net.NetworkInformation.IPGlobalProperties]::GetIPGlobalProperties().GetActiveTcpListeners() |
        Where-Object Port -eq $ServerPort |
        Select-Object -First 1
    if ($null -ne $activePort) {
        throw "SPT Test Clone port is already in use: $ServerPort"
    }

    $configPath = Join-Path $ClonePath 'SPT\SPT_Data\configs\http.json'
    if (-not (Test-Path -LiteralPath $configPath -PathType Leaf)) {
        throw "SPT HTTP configuration is missing from the test clone: $configPath"
    }

    $config = Get-Content -Raw -LiteralPath $configPath | ConvertFrom-Json
    $config.ip = '127.0.0.1'
    $config.backendIp = '127.0.0.1'
    $config.port = $ServerPort
    $config.backendPort = $ServerPort
    $config | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $configPath -Encoding UTF8
}

function Invoke-LoggedProcess {
    param(
        [Parameter(Mandatory)]
        [string] $Executable,

        [AllowEmptyString()]
        [string] $Arguments,

        [Parameter(Mandatory)]
        [string] $WorkingDirectory,

        [Parameter(Mandatory)]
        [string] $StandardOutputPath,

        [Parameter(Mandatory)]
        [string] $StandardErrorPath,

        [switch] $CaptureOutput,
        [switch] $Wait
    )

    $startInfo = [Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $Executable
    $startInfo.Arguments = $Arguments
    $startInfo.WorkingDirectory = $WorkingDirectory
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    if ($CaptureOutput) {
        $startInfo.RedirectStandardOutput = $true
        $startInfo.RedirectStandardError = $true
    }

    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    if (-not $process.Start()) {
        throw "Process did not start: $Executable"
    }

    if ($CaptureOutput) {
        $outputStream = [IO.File]::Open($StandardOutputPath, [IO.FileMode]::Create, [IO.FileAccess]::Write, [IO.FileShare]::ReadWrite)
        $errorStream = [IO.File]::Open($StandardErrorPath, [IO.FileMode]::Create, [IO.FileAccess]::Write, [IO.FileShare]::ReadWrite)
        $standardOutput = $process.StandardOutput.BaseStream.CopyToAsync($outputStream)
        $standardError = $process.StandardError.BaseStream.CopyToAsync($errorStream)
    }
    if ($Wait) {
        $process.WaitForExit()
        if ($CaptureOutput) {
            $standardOutput.Wait()
            $standardError.Wait()
            $outputStream.Dispose()
            $errorStream.Dispose()
        }
    }
    elseif ($CaptureOutput) {
        $process | Add-Member -NotePropertyName StandardOutputCapture -NotePropertyValue $standardOutput
        $process | Add-Member -NotePropertyName StandardErrorCapture -NotePropertyValue $standardError
        $process | Add-Member -NotePropertyName StandardOutputStream -NotePropertyValue $outputStream
        $process | Add-Member -NotePropertyName StandardErrorStream -NotePropertyValue $errorStream
    }
    return $process
}

function Start-SptConsoleProcess {
    param(
        [Parameter(Mandatory)]
        [string] $ServerExecutable,

        [AllowEmptyString()]
        [string] $ServerArguments,

        [Parameter(Mandatory)]
        [string] $WorkingDirectory,

        [Parameter(Mandatory)]
        [string] $StandardOutputPath,

        [Parameter(Mandatory)]
        [string] $StandardErrorPath,

        [Parameter(Mandatory)]
        [string] $LaunchScriptPath
    )

    foreach ($value in @($ServerExecutable, $ServerArguments, $WorkingDirectory, $StandardOutputPath, $StandardErrorPath, $LaunchScriptPath)) {
        if ($value -match '[\r\n]') {
            throw 'Server launch values must not contain newlines.'
        }
    }

    $consoleHost = Join-Path $env:SystemRoot 'System32\conhost.exe'
    $line = '"{0}" --headless "{1}" {2} 1>"{3}" 2>"{4}"' -f `
        $consoleHost, $ServerExecutable, $ServerArguments, $StandardOutputPath, $StandardErrorPath
    [IO.File]::WriteAllLines($LaunchScriptPath, @('@echo off', $line), [Text.Encoding]::Default)

    $commandLine = "cmd.exe /d /c `"`"$LaunchScriptPath`"`""
    $result = Invoke-CimMethod `
        -ClassName Win32_Process `
        -MethodName Create `
        -Arguments @{ CommandLine = $commandLine; CurrentDirectory = $WorkingDirectory }
    if ($result.ReturnValue -ne 0) {
        throw "Windows process creation failed with code $($result.ReturnValue)."
    }
    return [int] $result.ProcessId
}

function Invoke-SptModBuild {
    param(
        [Parameter(Mandatory)]
        [string] $RepositoryRoot,

        [Parameter(Mandatory)]
        [string] $BuildExecutable,

        [Parameter(Mandatory)]
        [string] $BuildArguments,

        [Parameter(Mandatory)]
        [string] $BuildOutputPath,

        [Parameter(Mandatory)]
        [string] $ReportDirectory
    )

    $repository = Get-FullPath $RepositoryRoot
    $output = Get-FullPath $BuildOutputPath
    if (-not $output.StartsWith("$repository\", [StringComparison]::OrdinalIgnoreCase)) {
        throw "Build output must remain inside the repository: $output"
    }
    if (Test-Path -LiteralPath $output) {
        Remove-Item -LiteralPath $output -Recurse -Force
    }

    $process = Invoke-LoggedProcess `
        -Executable $BuildExecutable `
        -Arguments $BuildArguments `
        -WorkingDirectory $repository `
        -StandardOutputPath (Join-Path $ReportDirectory 'build.stdout.log') `
        -StandardErrorPath (Join-Path $ReportDirectory 'build.stderr.log') `
        -CaptureOutput `
        -Wait
    if ($process.ExitCode -ne 0) {
        throw "Build failed with exit code $($process.ExitCode)."
    }

    $assembly = Join-Path $output 'ODT-ItemInfo-4.0.dll'
    if (-not (Test-Path -LiteralPath $assembly -PathType Leaf)) {
        throw "Build completed without the mod assembly: $assembly"
    }
}

function Install-SptModOutput {
    param(
        [Parameter(Mandatory)]
        [string] $RepositoryRoot,

        [Parameter(Mandatory)]
        [string] $BuildOutputPath,

        [Parameter(Mandatory)]
        [string] $ClonePath,

        [Parameter(Mandatory)]
        [string] $ModName
    )

    $destination = Join-Path $ClonePath "SPT\user\mods\$ModName"
    New-Item -ItemType Directory -Path $destination -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $BuildOutputPath 'ODT-ItemInfo-4.0.dll') -Destination $destination -Force
    Copy-Item -LiteralPath (Join-Path $RepositoryRoot 'LICENSE') -Destination $destination -Force
    Copy-Item -LiteralPath (Join-Path $RepositoryRoot 'config') -Destination $destination -Recurse -Force
}

function Get-SptServerOutput {
    param(
        [Parameter(Mandatory)]
        [string] $ClonePath,

        [Parameter(Mandatory)]
        [string] $StandardOutputPath,

        [Parameter(Mandatory)]
        [string] $StandardErrorPath
    )

    $parts = [Collections.Generic.List[string]]::new()
    foreach ($path in @($StandardOutputPath, $StandardErrorPath)) {
        if (Test-Path -LiteralPath $path -PathType Leaf) {
            $parts.Add((Get-Content -Raw -LiteralPath $path))
        }
    }

    $sptLogRoot = Join-Path $ClonePath 'SPT\user\logs'
    if (Test-Path -LiteralPath $sptLogRoot -PathType Container) {
        Get-ChildItem -LiteralPath $sptLogRoot -Filter '*.log' -Recurse -File -ErrorAction SilentlyContinue |
            ForEach-Object { $parts.Add((Get-Content -Raw -LiteralPath $_.FullName)) }
    }
    return ($parts -join [Environment]::NewLine)
}

function Stop-SptServerProcess {
    param(
        [Nullable[int]] $RootProcessId
    )

    if ($null -eq $RootProcessId) {
        return @()
    }

    $allProcesses = @(Get-CimInstance Win32_Process | Select-Object ProcessId, ParentProcessId)
    $processIds = [Collections.Generic.List[int]]::new()
    $frontier = [Collections.Generic.Queue[int]]::new()
    $frontier.Enqueue([int] $RootProcessId)
    while ($frontier.Count -gt 0) {
        $parentId = $frontier.Dequeue()
        foreach ($child in @($allProcesses | Where-Object ParentProcessId -eq $parentId)) {
            $childId = [int] $child.ProcessId
            $processIds.Add($childId)
            $frontier.Enqueue($childId)
        }
    }
    $processIds.Add([int] $RootProcessId)

    $idsToStop = @($processIds)
    [array]::Reverse($idsToStop)
    foreach ($processId in $idsToStop) {
        if (Get-Process -Id $processId -ErrorAction SilentlyContinue) {
            try {
                Stop-Process -Id $processId -Force -ErrorAction Stop
            }
            catch {
                if (Get-Process -Id $processId -ErrorAction SilentlyContinue) {
                    throw
                }
            }
        }
    }

    $deadline = [DateTime]::UtcNow.AddSeconds(5)
    do {
        $remaining = @($idsToStop | Where-Object { Get-Process -Id $_ -ErrorAction SilentlyContinue })
        if ($remaining.Count -eq 0) {
            return $idsToStop
        }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)

    throw "SPT server process tree did not stop: $($remaining -join ', ')"
}

function Copy-LogDirectory {
    param(
        [Parameter(Mandatory)]
        [string] $Source,

        [Parameter(Mandatory)]
        [string] $Destination
    )

    if (Test-Path -LiteralPath $Source -PathType Container) {
        Copy-Item -LiteralPath $Source -Destination $Destination -Recurse -Force
    }
}

function Copy-SptLogsToReport {
    param(
        [Parameter(Mandatory)]
        [string] $ClonePath,

        [Parameter(Mandatory)]
        [string] $ReportDirectory
    )

    Copy-LogDirectory `
        -Source (Join-Path $ClonePath 'SPT\user\logs') `
        -Destination (Join-Path $ReportDirectory 'spt-logs')
    Copy-LogDirectory `
        -Source (Join-Path $ClonePath 'Logs') `
        -Destination (Join-Path $ReportDirectory 'game-logs')

    $bepInExLog = Join-Path $ClonePath 'BepInEx\LogOutput.log'
    if (Test-Path -LiteralPath $bepInExLog -PathType Leaf) {
        $bepInExDestination = Join-Path $ReportDirectory 'bepinex'
        New-Item -ItemType Directory -Path $bepInExDestination -Force | Out-Null
        Copy-Item -LiteralPath $bepInExLog -Destination $bepInExDestination -Force
    }
}
