param(
    [switch]$ArchitectureOnly
)

$ErrorActionPreference = "Stop"

$detectedArchitecture = if ($env:PROCESSOR_ARCHITEW6432) {
    $env:PROCESSOR_ARCHITEW6432
} elseif ($env:PROCESSOR_ARCHITECTURE) {
    $env:PROCESSOR_ARCHITECTURE
} else {
    try {
        [string][System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture
    } catch {
        $null
    }
}

if ([string]::IsNullOrWhiteSpace($detectedArchitecture)) {
    throw "lag: unable to detect Windows architecture"
}

$architecture = switch ($detectedArchitecture.ToUpperInvariant()) {
    { $_ -in "AMD64", "X64", "X86_64" } { "x64"; break }
    { $_ -in "ARM64", "AARCH64" } { "arm64"; break }
    default { throw "lag: unsupported architecture: $detectedArchitecture" }
}

if ($ArchitectureOnly) {
    Write-Output $architecture
    return
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
