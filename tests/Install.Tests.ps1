$ErrorActionPreference = "Stop"

$installer = Join-Path (Split-Path $PSScriptRoot -Parent) "install.ps1"
$originalProcessorArchitecture = $env:PROCESSOR_ARCHITECTURE
$originalProcessorArchitectureW6432 = $env:PROCESSOR_ARCHITEW6432

function Assert-Architecture {
    param(
        [string]$ProcessorArchitecture,
        [AllowNull()]
        [string]$ProcessorArchitectureW6432,
        [string]$Expected
    )

    [Environment]::SetEnvironmentVariable(
        "PROCESSOR_ARCHITECTURE",
        $ProcessorArchitecture,
        "Process"
    )
    [Environment]::SetEnvironmentVariable(
        "PROCESSOR_ARCHITEW6432",
        $ProcessorArchitectureW6432,
        "Process"
    )

    $actual = & $installer -ArchitectureOnly
    if ($actual -ne $Expected) {
        throw "Expected '$Expected' for PROCESSOR_ARCHITECTURE='$ProcessorArchitecture' and PROCESSOR_ARCHITEW6432='$ProcessorArchitectureW6432', got '$actual'."
    }
}

try {
    Assert-Architecture -ProcessorArchitecture "AMD64" -Expected "x64"
    Assert-Architecture -ProcessorArchitecture "ARM64" -Expected "arm64"
    Assert-Architecture -ProcessorArchitecture "x86" -ProcessorArchitectureW6432 "AMD64" -Expected "x64"
    Assert-Architecture -ProcessorArchitecture "x86" -ProcessorArchitectureW6432 "ARM64" -Expected "arm64"
    Write-Host "Windows installer architecture tests passed."
} finally {
    [Environment]::SetEnvironmentVariable(
        "PROCESSOR_ARCHITECTURE",
        $originalProcessorArchitecture,
        "Process"
    )
    [Environment]::SetEnvironmentVariable(
        "PROCESSOR_ARCHITEW6432",
        $originalProcessorArchitectureW6432,
        "Process"
    )
}
