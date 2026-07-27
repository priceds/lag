$ErrorActionPreference = "Stop"

$architecture = switch ([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture) {
    "X64" { "x64" }
    "Arm64" { "arm64" }
    default { throw "lag: unsupported architecture: $_" }
}

$installDir = if ($env:LAG_INSTALL_DIR) {
    $env:LAG_INSTALL_DIR
} else {
    Join-Path $env:LOCALAPPDATA "Programs\lag"
}

$asset = "lag-win-$architecture.zip"
$url = "https://github.com/priceds/lag/releases/latest/download/$asset"
$tempDir = Join-Path ([System.IO.Path]::GetTempPath()) ("lag-" + [guid]::NewGuid())

try {
    New-Item -ItemType Directory -Path $tempDir | Out-Null
    Write-Host "Installing lag for win-$architecture..."
    Invoke-WebRequest -Uri $url -OutFile (Join-Path $tempDir $asset)
    Expand-Archive -Path (Join-Path $tempDir $asset) -DestinationPath $tempDir
    New-Item -ItemType Directory -Force -Path $installDir | Out-Null
    Copy-Item (Join-Path $tempDir "lag.exe") (Join-Path $installDir "lag.exe") -Force

    $userPath = [Environment]::GetEnvironmentVariable("Path", "User")
    $pathEntries = @($userPath -split ";" | Where-Object { $_ })
    if ($installDir -notin $pathEntries) {
        $newPath = (@($pathEntries) + $installDir) -join ";"
        [Environment]::SetEnvironmentVariable("Path", $newPath, "User")
        $env:Path = "$env:Path;$installDir"
        Write-Host "Added $installDir to your user PATH."
    }

    Write-Host "Installed lag to $installDir\lag.exe"
    Write-Host "Open a new terminal, then run: lag"
} finally {
    if (Test-Path $tempDir) {
        Remove-Item -Recurse -Force $tempDir
    }
}
