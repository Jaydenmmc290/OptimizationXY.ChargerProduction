[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [string]$SaperaSdkDir = $env:SAPERA_SDK_DIR,

    [switch]$SkipSapera
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $repoRoot "OptimizationXY.Charger.slnx"

if (-not (Test-Path $solution)) {
    throw "Solution file not found: $solution"
}

$arguments = @(
    "build",
    $solution,
    "--configuration", $Configuration,
    "--nologo"
)

if ($SkipSapera) {
    $arguments += "-p:UseSapera=false"
}
else {
    $arguments += "-p:UseSapera=true"
    $arguments += "-p:RequireSapera=true"

    if (-not [string]::IsNullOrWhiteSpace($SaperaSdkDir)) {
        $arguments += "-p:SaperaSdkDir=$SaperaSdkDir"
    }
}

Write-Host "Building solution: $solution"
Write-Host "Configuration: $Configuration"
Write-Host "Use Sapera: $(-not $SkipSapera)"

& dotnet @arguments

if ($LASTEXITCODE -ne 0) {
    throw "Build failed with exit code $LASTEXITCODE."
}

Write-Host "Build completed successfully."