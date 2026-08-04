[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ApplicationDirectory,

    [string]$DataRoot = $env:OPTIMIZATION_XY_DATA_DIR,

    [string]$SqlInstanceName = "SQLEXPRESS",

    [string]$PlcAddress = "172.16.60.2"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$checks = [System.Collections.Generic.List[object]]::new()
function Add-Check([string]$Name, [string]$Status, [string]$Message) {
    $checks.Add([pscustomobject]@{ Name = $Name; Status = $Status; Message = $Message })
}

function Get-Sha256HexFromText([string]$Text) {
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $bytes = [Text.Encoding]::UTF8.GetBytes($Text)
        $hash = $sha.ComputeHash($bytes)
        return ([BitConverter]::ToString($hash)).Replace("-", "")
    }
    finally {
        $sha.Dispose()
    }
}

function Get-AcquisitionGeometrySha256($Settings) {
    $concatenation = $Settings.Acquisition.Concatenation
    $head = $Settings.Acquisition.HeadCamera
    $tail = $Settings.Acquisition.TailCamera
    $fingerprintRecord = [ordered]@{
        Schema                       = 3
        TargetFramePairsPerLog       = [int]$Settings.Acquisition.TargetFramePairsPerLog
        HeadPosition                 = [string]$head.Position
        TailPosition                 = [string]$tail.Position
        HeadServer                   = [string]$head.ServerName
        TailServer                   = [string]$tail.ServerName
        HeadDeviceIndex              = [int]$head.DeviceIndex
        TailDeviceIndex              = [int]$tail.DeviceIndex
        HeadExpectedSerial           = [string]$head.ExpectedSerialNumber
        TailExpectedSerial           = [string]$tail.ExpectedSerialNumber
        HeadInitialization           = [string]$head.InitializationMode
        TailInitialization           = [string]$tail.InitializationMode
        HeadPixelFormat              = [string]$head.PixelFormat
        TailPixelFormat              = [string]$tail.PixelFormat
        HeadMode                     = [string]$head.Mode
        TailMode                     = [string]$tail.Mode
        HeadTriggerSource            = [string]$head.TriggerSource
        TailTriggerSource            = [string]$tail.TriggerSource
        HeadTriggerActivation        = [string]$head.TriggerActivation
        TailTriggerActivation        = [string]$tail.TriggerActivation
        HeadOnLeft                   = [bool]$concatenation.HeadOnLeft
        ReverseHeadHorizontal        = [bool]$concatenation.ReverseHeadHorizontal
        ReverseTailHorizontal        = [bool]$concatenation.ReverseTailHorizontal
        HeadCropLeftPixels           = [int]$concatenation.HeadCropLeftPixels
        HeadCropRightPixels          = [int]$concatenation.HeadCropRightPixels
        TailCropLeftPixels           = [int]$concatenation.TailCropLeftPixels
        TailCropRightPixels          = [int]$concatenation.TailCropRightPixels
        TailVerticalOffsetPixels     = [int]$concatenation.TailVerticalOffsetPixels
        OverlapPixels                = [int]$concatenation.OverlapPixels
        RequireEqualHeights          = [bool]$concatenation.RequireEqualHeights
    }

    $json = $fingerprintRecord | ConvertTo-Json -Compress
    return Get-Sha256HexFromText $json
}

if ([string]::IsNullOrWhiteSpace($DataRoot)) {
    $DataRoot = Join-Path $env:LOCALAPPDATA "OptimizationXY\Charger"
}

$DataRoot = [IO.Path]::GetFullPath([Environment]::ExpandEnvironmentVariables($DataRoot))
$ApplicationDirectory = [IO.Path]::GetFullPath([Environment]::ExpandEnvironmentVariables($ApplicationDirectory))

Add-Check "Windows x64" $(if ($env:OS -eq "Windows_NT" -and [Environment]::Is64BitOperatingSystem) { "PASS" } else { "BLOCK" }) "Production requires 64-bit Windows."

$requiredApplicationFiles = @(
    "OptimizationXY.Charger.exe",
    "XYRestricted.dll",
    "AutomatedSolutions.ASCommStd.dll",
    "NLog.dll",
    "DALSA.SaperaLT.SapClassBasic.Core.dll",
    "NLog.config"
)
foreach ($name in $requiredApplicationFiles) {
    $path = Join-Path $ApplicationDirectory $name
    Add-Check "Application file: $name" $(if (Test-Path -LiteralPath $path -PathType Leaf) { "PASS" } else { "BLOCK" }) $path
}

Add-Check "Camera initialization policy" "PASS" "Production uses direct Sapera server/resource initialization and does not require or install CCF files."

$serviceName = if ($SqlInstanceName -eq "MSSQLSERVER") { "MSSQLSERVER" } else { "MSSQL`$$SqlInstanceName" }
$sqlService = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
Add-Check "SQL Server service" $(if ($null -ne $sqlService -and $sqlService.Status -eq "Running") { "PASS" } else { "BLOCK" }) $(if ($null -eq $sqlService) { "$serviceName was not found." } else { "$serviceName status: $($sqlService.Status)." })

$settingsPath = Join-Path $DataRoot "Config\appsettings.json"
$calibrationDirectory = Join-Path $DataRoot "Config\Calibration"
$calibrationPath = Join-Path $calibrationDirectory "scanner-lookup-table.json"
$calibrationMetadataPath = Join-Path $calibrationDirectory "scanner-calibration.json"
$calibrationManifestPath = Join-Path $calibrationDirectory "sha256-manifest.txt"
$calibrationHeadSnapshotPath = Join-Path $calibrationDirectory "head-camera-configuration.snapshot"
$calibrationTailSnapshotPath = Join-Path $calibrationDirectory "tail-camera-configuration.snapshot"
$setupStatusPath = Join-Path $DataRoot "Config\Setup\production-setup-status.json"

Add-Check "Runtime settings" $(if (Test-Path -LiteralPath $settingsPath -PathType Leaf) { "PASS" } else { "BLOCK" }) $settingsPath
Add-Check "Production setup status" $(if (Test-Path -LiteralPath $setupStatusPath -PathType Leaf) { "PASS" } else { "BLOCK" }) $setupStatusPath

$calibrationPackageFiles = @(
    $calibrationPath,
    $calibrationMetadataPath,
    $calibrationHeadSnapshotPath,
    $calibrationTailSnapshotPath,
    $calibrationManifestPath
)
$missingCalibrationFiles = @($calibrationPackageFiles | Where-Object { -not (Test-Path -LiteralPath $_ -PathType Leaf) })
Add-Check "Scanner calibration package" $(if ($missingCalibrationFiles.Count -eq 0) { "PASS" } else { "BLOCK" }) $(if ($missingCalibrationFiles.Count -eq 0) { $calibrationDirectory } else { "Missing: " + ($missingCalibrationFiles -join "; ") })

try {
    New-Item -ItemType Directory -Force -Path $DataRoot | Out-Null
    $writeProbe = Join-Path $DataRoot (".write-probe-" + [Guid]::NewGuid().ToString("N") + ".tmp")
    Set-Content -LiteralPath $writeProbe -Value "production-host-write-test" -Encoding utf8
    Remove-Item -LiteralPath $writeProbe -Force
    Add-Check "Runtime data directory writable" "PASS" $DataRoot
}
catch {
    Add-Check "Runtime data directory writable" "BLOCK" $_.Exception.Message
}

$calibration = $null
$settings = $null

if ($missingCalibrationFiles.Count -eq 0) {
    try {
        $expectedManifestFiles = @(
            "scanner-lookup-table.json",
            "scanner-calibration.json",
            "head-camera-configuration.snapshot",
            "tail-camera-configuration.snapshot"
        )
        $manifestLines = @(Get-Content -LiteralPath $calibrationManifestPath)
        $seenManifestFiles = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
        $manifestOk = $manifestLines.Count -eq $expectedManifestFiles.Count

        foreach ($line in $manifestLines) {
            $parts = $line -split '\s{2,}', 2
            if ($parts.Count -ne 2 -or [string]$parts[0] -notmatch '^[0-9A-Fa-f]{64}$') {
                $manifestOk = $false
                break
            }

            $fileName = [IO.Path]::GetFileName([string]$parts[1])
            if ($fileName -ne [string]$parts[1] -or
                $expectedManifestFiles -notcontains $fileName -or
                -not $seenManifestFiles.Add($fileName)) {
                $manifestOk = $false
                break
            }

            $filePath = Join-Path $calibrationDirectory $fileName
            if (-not (Test-Path -LiteralPath $filePath -PathType Leaf)) {
                $manifestOk = $false
                break
            }

            $actualHash = (Get-FileHash -LiteralPath $filePath -Algorithm SHA256).Hash
            if ($actualHash -ne [string]$parts[0]) {
                $manifestOk = $false
                break
            }
        }

        foreach ($expectedName in $expectedManifestFiles) {
            if (-not $seenManifestFiles.Contains($expectedName)) {
                $manifestOk = $false
            }
        }

        Add-Check "Calibration SHA-256 manifest" $(if ($manifestOk) { "PASS" } else { "BLOCK" }) $calibrationManifestPath
    }
    catch {
        Add-Check "Calibration SHA-256 manifest" "BLOCK" $_.Exception.Message
    }

    try {
        $calibration = Get-Content -LiteralPath $calibrationPath -Raw | ConvertFrom-Json
        $metadata = Get-Content -LiteralPath $calibrationMetadataPath -Raw | ConvertFrom-Json
        $shaPattern = '^[0-9A-Fa-f]{64}$'
        $schemaValid = [int]$calibration.SchemaVersion -ge 3
        $calibrationIdValid = -not [string]::IsNullOrWhiteSpace([string]$calibration.CalibrationId)
        $geometryHashValid = [string]$calibration.AcquisitionGeometrySha256 -match $shaPattern
        $headRecordedHashValid = [string]$calibration.HeadCameraConfigurationSha256 -match $shaPattern
        $tailRecordedHashValid = [string]$calibration.TailCameraConfigurationSha256 -match $shaPattern
        $imageWidth = [int]$calibration.ImageWidth
        $imageHeight = [int]$calibration.ImageHeight
        $dimensionsValid = $imageWidth -gt 0 -and $imageHeight -gt 0

        $lengthPixelsPerMM = [double]$calibration.LengthPixelsPerMM
        $points = @($calibration.Points)
        $lengthValid =
            $null -ne $calibration.LengthPixelsPerMM -and
            -not [double]::IsNaN($lengthPixelsPerMM) -and
            -not [double]::IsInfinity($lengthPixelsPerMM) -and
            $lengthPixelsPerMM -gt 0

        $pointsValid = $points.Count -ge 2
        foreach ($point in $points) {
            $laserY = [double]$point.LaserY
            $radiusMM = [double]$point.RadiusMM
            if ([int]$point.X -lt 0 -or
                [int]$point.X -ge $imageWidth -or
                [double]::IsNaN($laserY) -or
                [double]::IsInfinity($laserY) -or
                $laserY -lt 0 -or
                $laserY -ge $imageHeight -or
                [double]::IsNaN($radiusMM) -or
                [double]::IsInfinity($radiusMM) -or
                $radiusMM -le 0) {
                $pointsValid = $false
                break
            }
        }

        $radiusBandCount = @(
            $points |
                ForEach-Object {
                    if ($null -ne $_.RadiusBandId -and [int]$_.RadiusBandId -gt 0) {
                        "B:$([int]$_.RadiusBandId)"
                    }
                    else {
                        "R:$([Math]::Round([double]$_.RadiusMM, 4))"
                    }
                } |
                Sort-Object -Unique
        ).Count

        $minimumRadius = [double]$calibration.MinimumCalibratedRadiusMM
        $maximumRadius = [double]$calibration.MaximumCalibratedRadiusMM
        $actualMinimumRadius = if ($points.Count -gt 0) {
            [double](($points | Measure-Object -Property RadiusMM -Minimum).Minimum)
        }
        else { [double]::NaN }
        $actualMaximumRadius = if ($points.Count -gt 0) {
            [double](($points | Measure-Object -Property RadiusMM -Maximum).Maximum)
        }
        else { [double]::NaN }
        $rangeValid =
            $minimumRadius -gt 0 -and
            $maximumRadius -gt $minimumRadius -and
            [Math]::Abs($actualMinimumRadius - $minimumRadius) -le 0.01 -and
            [Math]::Abs($actualMaximumRadius - $maximumRadius) -le 0.01
        $legacyEstimated = $null -ne ($points | Where-Object {
            [string]$_.SourceName -match "Estimated scanner-orientation grid"
        } | Select-Object -First 1)

        $metadataMatches =
            [int]$metadata.SchemaVersion -eq [int]$calibration.SchemaVersion -and
            [string]$metadata.CalibrationId -eq [string]$calibration.CalibrationId -and
            [string]$metadata.HeadCameraConfigurationSha256 -eq [string]$calibration.HeadCameraConfigurationSha256 -and
            [string]$metadata.TailCameraConfigurationSha256 -eq [string]$calibration.TailCameraConfigurationSha256 -and
            [string]$metadata.AcquisitionGeometrySha256 -eq [string]$calibration.AcquisitionGeometrySha256 -and
            [int]$metadata.ImageWidth -eq $imageWidth -and
            [int]$metadata.ImageHeight -eq $imageHeight -and
            [Math]::Abs([double]$metadata.LengthPixelsPerMM - $lengthPixelsPerMM) -le 1e-9 -and
            [Math]::Abs([double]$metadata.MinimumCalibratedRadiusMM - $minimumRadius) -le 1e-9 -and
            [Math]::Abs([double]$metadata.MaximumCalibratedRadiusMM - $maximumRadius) -le 1e-9

        $calibrationValid =
            $schemaValid -and
            $calibrationIdValid -and
            $geometryHashValid -and
            $headRecordedHashValid -and
            $tailRecordedHashValid -and
            $dimensionsValid -and
            $lengthValid -and
            $pointsValid -and
            $radiusBandCount -ge 2 -and
            $rangeValid -and
            $metadataMatches -and
            -not $legacyEstimated

        Add-Check "Calibration content" $(if ($calibrationValid) { "PASS" } else { "BLOCK" }) "Schema=$($calibration.SchemaVersion); ID=$($calibration.CalibrationId); image=${imageWidth}x${imageHeight}; LengthPixelsPerMM=$lengthPixelsPerMM; points=$($points.Count); radius bands=$radiusBandCount; radius range=$minimumRadius..$maximumRadius mm; metadata match=$metadataMatches; legacy estimated=$legacyEstimated."

        $headHash = (Get-FileHash -LiteralPath $calibrationHeadSnapshotPath -Algorithm SHA256).Hash
        $tailHash = (Get-FileHash -LiteralPath $calibrationTailSnapshotPath -Algorithm SHA256).Hash
        $cameraSnapshotHashesMatch =
            $headHash -eq [string]$calibration.HeadCameraConfigurationSha256 -and
            $tailHash -eq [string]$calibration.TailCameraConfigurationSha256
        Add-Check "Calibration camera snapshot hashes" $(if ($cameraSnapshotHashesMatch) { "PASS" } else { "BLOCK" }) "Calibration package camera snapshots must match the hashes in scanner-lookup-table.json."
    }
    catch {
        Add-Check "Calibration content" "BLOCK" $_.Exception.Message
        $calibration = $null
    }
}

if (Test-Path -LiteralPath $settingsPath -PathType Leaf) {
    try {
        $settings = Get-Content -LiteralPath $settingsPath -Raw | ConvertFrom-Json
        Add-Check "Settings schema" $(if ([int]$settings.SettingsVersion -ge 10) { "PASS" } else { "BLOCK" }) "Configured: $($settings.SettingsVersion)."
        Add-Check "36 synchronized pairs" $(if ([int]$settings.Acquisition.TargetFramePairsPerLog -eq 36) { "PASS" } else { "BLOCK" }) "Configured: $($settings.Acquisition.TargetFramePairsPerLog)."
        Add-Check "Head encoder acquisition" $(if ($settings.Acquisition.HeadCamera.Mode -eq "Encoder") { "PASS" } else { "BLOCK" }) "Configured: $($settings.Acquisition.HeadCamera.Mode)."
        Add-Check "Tail encoder acquisition" $(if ($settings.Acquisition.TailCamera.Mode -eq "Encoder") { "PASS" } else { "BLOCK" }) "Configured: $($settings.Acquisition.TailCamera.Mode)."

        foreach ($cameraName in @("HeadCamera", "TailCamera")) {
            $camera = $settings.Acquisition.$cameraName
            $directReady = (
                [string]$camera.InitializationMode -eq "Programmatic" -and
                -not [string]::IsNullOrWhiteSpace([string]$camera.ServerName) -and
                [int]$camera.DeviceIndex -ge 0 -and
                -not [string]::IsNullOrWhiteSpace([string]$camera.ExpectedSerialNumber) -and
                [string]$camera.PixelFormat -eq "Mono8" -and
                [double]$camera.ExposureTimeMicroseconds -gt 0 -and
                [double]$camera.Gain -ge 0 -and
                [bool]$camera.RequireConfiguredFeatures -and
                ([string]$camera.Mode -ne "Encoder" -or (
                    -not [string]::IsNullOrWhiteSpace([string]$camera.TriggerSource) -and
                    -not [string]::IsNullOrWhiteSpace([string]$camera.TriggerActivation)))
            )
            Add-Check "$cameraName direct Sapera configuration" $(if ($directReady) { "PASS" } else { "BLOCK" }) "Server=$($camera.ServerName); resource=$($camera.DeviceIndex); expected serial=$($camera.ExpectedSerialNumber); mode=$($camera.Mode); pixel=$($camera.PixelFormat); exposure=$($camera.ExposureTimeMicroseconds); gain=$($camera.Gain); trigger=$($camera.TriggerSource)/$($camera.TriggerActivation)."
        }

        $headSerial = [string]$settings.Acquisition.HeadCamera.ExpectedSerialNumber
        $tailSerial = [string]$settings.Acquisition.TailCamera.ExpectedSerialNumber
        $serialAssignmentsValid =
            -not [string]::IsNullOrWhiteSpace($headSerial) -and
            -not [string]::IsNullOrWhiteSpace($tailSerial) -and
            -not [string]::Equals($headSerial.Trim(), $tailSerial.Trim(), [StringComparison]::OrdinalIgnoreCase)
        Add-Check "Distinct physical camera serials" $(if ($serialAssignmentsValid) { "PASS" } else { "BLOCK" }) "Head=$headSerial; Tail=$tailSerial."

        $setupComplete =
            $settings.Setup.DatabaseConfigured -and
            $settings.Setup.CameraConfigurationVerified -and
            $settings.Setup.CameraPhysicalPositionsConfirmed -and
            $settings.Setup.LaserDetectionVerified -and
            $settings.Setup.LengthCalibrationConfigured -and
            [double]$settings.Setup.PendingLengthPixelsPerMM -gt 0 -and
            $settings.Setup.ScannerCalibrationCreated -and
            $settings.Setup.ScannerCalibrationRepeatabilityVerified -and
            -not [string]::IsNullOrWhiteSpace([string]$settings.Setup.ActiveCalibrationId)
        Add-Check "Production setup wizard" $(if ($setupComplete) { "PASS" } else { "BLOCK" }) "SQL, cameras, physical sides, laser, length scale, and active calibration must all be recorded."

        if ($null -ne $calibration) {
            $currentGeometryHash = Get-AcquisitionGeometrySha256 $settings
            $geometryMatches = $currentGeometryHash -eq [string]$calibration.AcquisitionGeometrySha256
            Add-Check "Calibration acquisition geometry" $(if ($geometryMatches) { "PASS" } else { "BLOCK" }) "Current=$currentGeometryHash; calibrated=$($calibration.AcquisitionGeometrySha256). Crop, orientation, overlap, seam, side assignment, and frame count must match."

            $activeIdMatches =
                [string]$settings.Setup.ActiveCalibrationId -eq [string]$calibration.CalibrationId
            Add-Check "Active calibration identity" $(if ($activeIdMatches) { "PASS" } else { "BLOCK" }) "Settings=$($settings.Setup.ActiveCalibrationId); package=$($calibration.CalibrationId)."

            $lengthScaleMatches =
                [Math]::Abs(
                    [double]$settings.Setup.PendingLengthPixelsPerMM -
                    [double]$calibration.LengthPixelsPerMM) -le 1e-9
            Add-Check "Active axial length scale" $(if ($lengthScaleMatches) { "PASS" } else { "BLOCK" }) "Settings=$($settings.Setup.PendingLengthPixelsPerMM); package=$($calibration.LengthPixelsPerMM)."

            Add-Check "Runtime camera snapshot compatibility" "PASS" "The application verifies the exact programmatic feature snapshot against the active calibration after Sapera connects; this hardware readback cannot be reproduced by the offline host validator."

        }

        Add-Check "Local SQL saving enabled" $(if ($settings.Database.EnableDatabaseSaving -and $settings.Setup.DatabaseConfigured) { "PASS" } else { "BLOCK" }) "Database saving must be enabled after the in-program schema test succeeds."

        $veneerThickness = [double]$settings.UsableRadius.VeneerThicknessMM
        $clearanceMultiplier = [double]$settings.Safety.KnifeClearancePeelThicknessMultiplier
        $minimumClearance = [double]$settings.Safety.MinimumKnifeClearanceMM
        $additionalBuffer = [double]$settings.Safety.AdditionalCarriageRetractBufferMM
        $effectiveClearance = [Math]::Max($minimumClearance, $veneerThickness * [Math]::Max(10.0, $clearanceMultiplier)) + $additionalBuffer
        Add-Check "10x knife clearance" $(if ($veneerThickness -gt 0 -and $clearanceMultiplier -ge 10.0 -and $effectiveClearance -ge (10.0 * $veneerThickness)) { "PASS" } else { "BLOCK" }) "Peel $veneerThickness mm; effective retract clearance $effectiveClearance mm."
        Add-Check "Movement warnings block" $(if ($settings.Safety.BlockOnMovementWarning) { "PASS" } else { "BLOCK" }) "Production PLC delivery must be blocked when any configured movement warning is present."
        $allRecoverySeeds =
            $settings.RecoveryOptimization.IncludeBaselineCandidate -and
            $settings.RecoveryOptimization.IncludePenaltyCandidate -and
            $settings.RecoveryOptimization.IncludeCylinderCandidate -and
            $settings.RecoveryOptimization.IncludeOvalCandidate
        Add-Check "Recovery optimization mode" $(if ($settings.OptimizationMode -eq "RecoveryOptimized" -and $allRecoverySeeds -and $settings.RecoveryOptimization.EnableDirectRecoveryRefinement) { "PASS" } else { "BLOCK" }) "Production requires baseline, penalty, cylinder, oval, and direct virtual-peel recovery refinement candidates."
        Add-Check "PLC readback" $(if ($settings.Plc.RequireReadbackVerification -and $settings.Plc.RequireGoodQuality) { "PASS" } else { "BLOCK" }) "Readback and good-quality enforcement must both be enabled."
        Add-Check "PLC ScanComplete gate" $(if ($settings.Plc.RequireScanCompleteBeforeProcessing) { "PASS" } else { "BLOCK" }) "Processing must wait for PLC-controlled rotation completion."
        Add-Check "Safe PLC write order" $(if ($settings.Plc.WriteCarriageRetractFirst) { "PASS" } else { "BLOCK" }) "Supporting radius packets must be written before ScannedBlockData."

        $commissioningFlags = @(
            $settings.Production.CameraSeamCalibrationVerified,
            $settings.Production.ScannerCalibrationVerified,
            $settings.Production.MachineTravelLimitsVerified,
            $settings.Production.PlcSignConventionVerified,
            $settings.Production.KnifeClearanceVerified,
            $settings.Production.EmergencyStopAndInterlocksVerified,
            $settings.Production.PlcRuntimeLicenseVerified
        )
        $commissioned = -not ($commissioningFlags -contains $false) -and
            -not [string]::IsNullOrWhiteSpace([string]$settings.Production.CommissioningApprovedBy) -and
            $null -ne $settings.Production.CommissioningApprovedUtc
        Add-Check "Commissioning approval" $(if ($commissioned) { "PASS" } else { "BLOCK" }) "All physical checks, approver, and UTC approval time are required."
        Add-Check "Operating mode" $(if ($settings.Production.OperatingMode -eq "Production") { "PASS" } else { "WARN" }) "Configured: $($settings.Production.OperatingMode). Commissioning mode intentionally blocks machine writes."
    }
    catch {
        Add-Check "Settings JSON parsing" "BLOCK" $_.Exception.Message
        $settings = $null
    }
}

try {
    $reachable = Test-Connection -ComputerName $PlcAddress -Count 1 -Quiet -ErrorAction Stop
    Add-Check "PLC network reachability" $(if ($reachable) { "PASS" } else { "WARN" }) "ICMP check to $PlcAddress. A WARN can occur when ping is disabled; verify the 172.16.0.0/16 route separately."
}
catch {
    Add-Check "PLC network reachability" "WARN" "Could not complete non-writing connectivity check to $PlcAddress: $($_.Exception.Message)"
}

$checks | Format-Table -AutoSize -Wrap
$blockCount = @($checks | Where-Object Status -eq "BLOCK").Count
$warningCount = @($checks | Where-Object Status -eq "WARN").Count
Write-Host "Blocking checks: $blockCount; warnings: $warningCount"
Write-Host "This script performs no PLC tag writes and does not replace physical commissioning."

if ($blockCount -gt 0) { exit 1 }
exit 0
