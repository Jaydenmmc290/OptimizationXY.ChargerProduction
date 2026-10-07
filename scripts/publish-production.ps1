[CmdletBinding()]
param(
    [ValidateSet("Release")]
    [string]$Configuration = "Release",

    [string]$SaperaSdkDir = $env:SAPERA_SDK_DIR,

    [bool]$SelfContained = $true,

    [string]$ReleaseRoot = ""
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

if ($env:OS -ne "Windows_NT") {
    throw "Production publish must run on Windows because Sapera and ASComm are Windows-only."
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot "src\OptimizationXY.Charger\OptimizationXY.Charger.csproj"
$template = Join-Path $repoRoot "config\appsettings.production.template.json"
$xyLib = Join-Path $repoRoot "lib\XYRestricted\XYRestricted.dll"
$asCommLib = Join-Path $repoRoot "lib\XYRestricted\AutomatedSolutions.ASCommStd.dll"

if ([string]::IsNullOrWhiteSpace($ReleaseRoot)) {
    $ReleaseRoot = Join-Path $repoRoot "artifacts\production-release"
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw ".NET SDK was not found in PATH. Install the SDK version pinned by global.json."
}

foreach ($required in @($project, $template, $xyLib, $asCommLib)) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) {
        throw "Required production file is missing: $required"
    }
}

if ([string]::IsNullOrWhiteSpace($SaperaSdkDir)) {
    $SaperaSdkDir = Join-Path $env:ProgramFiles "Teledyne DALSA\Sapera\Components\NET\Bin"
}

$saperaAssembly = Join-Path $SaperaSdkDir "DALSA.SaperaLT.SapClassBasic.Core.dll"
if (-not (Test-Path -LiteralPath $saperaAssembly -PathType Leaf)) {
    throw "Sapera hardware assembly not found: $saperaAssembly"
}

$templateJson = Get-Content -LiteralPath $template -Raw | ConvertFrom-Json
if ($templateJson.Production.OperatingMode -eq "Production") {
    throw "The distributed template must remain in Commissioning mode. Physical approval cannot be pre-authorized in a build."
}
if ([double]$templateJson.Safety.KnifeClearancePeelThicknessMultiplier -lt 10.0) {
    throw "The production template violates the required 10x peel-thickness knife clearance."
}
if (-not $templateJson.Safety.BlockOnMovementWarning) {
    throw "The production template must block PLC delivery when a movement warning is present."
}
if ([int]$templateJson.Acquisition.TargetFramePairsPerLog -ne 36) {
    throw "The production template must request exactly 36 synchronized frame pairs."
}
if ($templateJson.OptimizationMode -ne "RecoveryOptimized" -or
    -not $templateJson.RecoveryOptimization.IncludeBaselineCandidate -or
    -not $templateJson.RecoveryOptimization.IncludePenaltyCandidate -or
    -not $templateJson.RecoveryOptimization.IncludeCylinderCandidate -or
    -not $templateJson.RecoveryOptimization.IncludeOvalCandidate -or
    -not $templateJson.RecoveryOptimization.EnableDirectRecoveryRefinement) {
    throw "The production template must use RecoveryOptimized mode with all seed strategies and direct virtual-peel refinement enabled."
}
if (-not $templateJson.Plc.RequireScanCompleteBeforeProcessing -or
    -not $templateJson.Plc.RequireReadbackVerification -or
    -not $templateJson.Plc.RequireGoodQuality -or
    -not $templateJson.Plc.WriteCarriageRetractFirst) {
    throw "The production template must preserve ScanComplete gating, good-quality readback, and safe PLC write order."
}
if ([int]$templateJson.SettingsVersion -lt 10) {
    throw "The production template must use settings schema version 10 or newer."
}
if ($templateJson.Database.EnableDatabaseSaving) {
    throw "The distributed template must leave SQL saving disabled until the local SQL Express schema is created by the setup wizard."
}
$setupFlags = @(
    $templateJson.Setup.DatabaseConfigured,
    $templateJson.Setup.CameraConfigurationVerified,
    $templateJson.Setup.CameraPhysicalPositionsConfirmed,
    $templateJson.Setup.LaserDetectionVerified,
    $templateJson.Setup.LengthCalibrationConfigured,
    $templateJson.Setup.ScannerCalibrationCreated,
    $templateJson.Setup.ScannerCalibrationRepeatabilityVerified
)
if ($setupFlags -contains $true -or
    -not [string]::IsNullOrWhiteSpace([string]$templateJson.Setup.ActiveCalibrationId)) {
    throw "The distributed template cannot pre-authorize SQL, cameras, laser detection, length calibration, or scanner calibration."
}

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$publishDir = Join-Path $ReleaseRoot "OptimizationXY.Charger-$timestamp\Application"
$packageRoot = Split-Path -Parent $publishDir
New-Item -ItemType Directory -Force -Path $publishDir | Out-Null

$arguments = @(
    "publish",
    $project,
    "--configuration", $Configuration,
    "--runtime", "win-x64",
    "--output", $publishDir,
    "--nologo",
    "--self-contained", $SelfContained.ToString().ToLowerInvariant(),
    "-p:UseSapera=true",
    "-p:RequireSapera=true",
    "-p:SaperaSdkDir=$SaperaSdkDir",
    "-p:ContinuousIntegrationBuild=true"
)

& dotnet @arguments
if ($LASTEXITCODE -ne 0) {
    throw "Production publish failed with exit code $LASTEXITCODE."
}

$requiredPublishedFiles = @(
    "OptimizationXY.Charger.exe",
    "XYRestricted.dll",
    "AutomatedSolutions.ASCommStd.dll",
    "NLog.dll",
    "DALSA.SaperaLT.SapClassBasic.Core.dll",
    "NLog.config"
)

foreach ($name in $requiredPublishedFiles) {
    $path = Join-Path $publishDir $name
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Published package is incomplete. Missing: $path"
    }
}

$publishedCcfFiles = @(Get-ChildItem -LiteralPath $publishDir -Recurse -Filter *.ccf -File -ErrorAction SilentlyContinue)
if ($publishedCcfFiles.Count -gt 0) {
    throw "Schema 10 production packages must not contain camera CCF files. Direct Sapera feature configuration is required."
}

$headCameraTemplate = $templateJson.Acquisition.HeadCamera
$tailCameraTemplate = $templateJson.Acquisition.TailCamera
foreach ($camera in @($headCameraTemplate, $tailCameraTemplate)) {
    if ([string]$camera.InitializationMode -ne "Programmatic") {
        throw "Production camera templates must use Programmatic initialization, not a CCF file."
    }
    if ([string]::IsNullOrWhiteSpace([string]$camera.ServerName) -or [int]$camera.DeviceIndex -lt 0) {
        throw "Each production camera requires a Sapera server name and non-negative device index."
    }
    if ([string]$camera.PixelFormat -ne "Mono8" -or [double]$camera.ExposureTimeMicroseconds -le 0 -or [double]$camera.Gain -lt 0) {
        throw "Each production camera requires Mono8, positive exposure, and non-negative gain settings."
    }
    if ([string]$camera.Mode -eq "Encoder" -and
        ([string]::IsNullOrWhiteSpace([string]$camera.TriggerSource) -or
         [string]::IsNullOrWhiteSpace([string]$camera.TriggerActivation))) {
        throw "Encoder-mode cameras require a trigger source and trigger activation."
    }
}

New-Item -ItemType Directory -Force -Path (Join-Path $packageRoot "ConfigTemplate") | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $packageRoot "Database") | Out-Null
Copy-Item -LiteralPath $template -Destination (Join-Path $packageRoot "ConfigTemplate\appsettings.json") -Force
Copy-Item -LiteralPath (Join-Path $repoRoot "database\OptimizationXY.Schema.sql") -Destination (Join-Path $packageRoot "Database") -Force
Copy-Item -LiteralPath (Join-Path $repoRoot "docs\deployment\PRODUCTION_SETUP_WIZARD.md") -Destination $packageRoot -Force
Copy-Item -LiteralPath (Join-Path $repoRoot "docs\deployment\PRODUCTION_DEPLOYMENT.md") -Destination $packageRoot -Force
Copy-Item -LiteralPath (Join-Path $repoRoot "docs\deployment\COMMISSIONING_CHECKLIST.md") -Destination $packageRoot -Force
Copy-Item -LiteralPath (Join-Path $repoRoot "docs\deployment\SAFETY_AND_INTERLOCKS.md") -Destination $packageRoot -Force
Copy-Item -LiteralPath (Join-Path $repoRoot "docs\plc\PLC_INTEGRATION.md") -Destination $packageRoot -Force
Copy-Item -LiteralPath (Join-Path $repoRoot "docs\scanning\DIRECT_CAMERA_AND_OPERATOR_VIEW_IMPLEMENTATION.md") -Destination $packageRoot -Force
Copy-Item -LiteralPath (Join-Path $repoRoot "scripts\validate-production-host.ps1") -Destination $packageRoot -Force
Copy-Item -LiteralPath (Join-Path $repoRoot "scripts\install-production.ps1") -Destination $packageRoot -Force

$manifestPath = Join-Path $packageRoot "SHA256SUMS.txt"
$normalizedPackageRoot = $packageRoot.TrimEnd('\') + '\'
$manifestLines = @(
    Get-ChildItem -LiteralPath $packageRoot -Recurse -File |
        Where-Object { $_.FullName -ne $manifestPath } |
        Sort-Object FullName |
        ForEach-Object {
            $hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
            $relative = $_.FullName.Substring($normalizedPackageRoot.Length).Replace('\', '/')
            "$hash  $relative"
        }
)
[IO.File]::WriteAllLines(
    $manifestPath,
    $manifestLines,
    (New-Object Text.UTF8Encoding($false)))

$zipPath = "$packageRoot.zip"
if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}
Compress-Archive -LiteralPath $packageRoot -DestinationPath $zipPath -CompressionLevel Optimal

Write-Host "Production package created: $packageRoot"
Write-Host "ZIP package: $zipPath"
Write-Host "The included appsettings.json remains in Commissioning mode by design."
