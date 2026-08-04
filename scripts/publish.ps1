[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [string]$SaperaSdkDir = $env:SAPERA_SDK_DIR,

    [switch]$SelfContained,

    [switch]$SkipSapera
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot "src\OptimizationXY.Charger\OptimizationXY.Charger.csproj"
$output = Join-Path $repoRoot "artifacts\publish\win-x64"

$arguments = @(
    "publish",
    $project,
    "--configuration", $Configuration,
    "--runtime", "win-x64",
    "--output", $output,
    "--nologo",
    "--self-contained", $SelfContained.IsPresent.ToString().ToLowerInvariant()
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

& dotnet @arguments
if ($LASTEXITCODE -ne 0) {
    throw "Publish failed with exit code $LASTEXITCODE."
}

Write-Host "Published to $output"
