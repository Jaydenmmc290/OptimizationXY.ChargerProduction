using OptimizationXY.Charger.Models.Acquisition;
using OptimizationXY.Charger.Models.Settings;

namespace OptimizationXY.Charger.Services.Configuration;

/// <summary>
/// Validates the application settings to ensure they are complete and consistent.
/// </summary>
public static class AppSettingsValidator
{
    /// <summary>
    /// Validates the provided application settings, throwing an exception if any required settings are missing or inconsistent.
    /// </summary>
    /// <param name="settings">The application settings to validate.</param>
    /// <exception cref="InvalidOperationException">Thrown if any required settings are missing or inconsistent.</exception>
    public static void Validate(AppSettings settings)
    {
        if (settings == null)
            throw new InvalidOperationException("App settings are missing.");

        ValidateAcquisition(settings);
        ValidateOperatorView(settings);
        ValidateUnits(settings);
        ValidateRotatingScan(settings);
        ValidateGeometry(settings);
        ValidateDefects(settings);
        ValidateOptimization(settings);
        ValidateInscribedGeometry(settings);
        ValidatePeelView(settings);
        ValidateUsableRadiusAndWane(settings);
        ValidateMachineLimits(settings);
        ValidateProduction(settings);
        ValidatePlc(settings);
        ValidateSafety(settings);
        ValidateRecoveryOptimization(settings);
        ValidateStorage(settings);
        ValidateDatabase(settings);
        ValidateSetup(settings);
    }

    /// <summary>
    /// Validates the camera acquisition settings, ensuring that both head and tail cameras are properly
    /// configured and do not conflict with each other.
    /// </summary>
    /// <param name="settings">The application settings to validate.</param>
    /// <exception cref="InvalidOperationException">Thrown if any required camera acquisition settings are missing or inconsistent.</exception>
    private static void ValidateAcquisition(AppSettings settings)
    {
        if (settings.Acquisition == null)
        {
            throw new InvalidOperationException(
                "Camera acquisition settings are missing.");
        }

        CameraDeviceSettings? head = settings.Acquisition.HeadCamera;
        CameraDeviceSettings? tail = settings.Acquisition.TailCamera;
        if (head is null || tail is null)
            throw new InvalidOperationException("Head and tail camera settings are required.");

        ValidateCameraDeviceShape(head, CameraPosition.Head);
        ValidateCameraDeviceShape(tail, CameraPosition.Tail);

        if (!string.IsNullOrWhiteSpace(head.ServerName) &&
     !string.IsNullOrWhiteSpace(tail.ServerName) &&
     string.Equals(
         head.ServerName,
         tail.ServerName,
         StringComparison.OrdinalIgnoreCase) &&
     head.DeviceIndex == tail.DeviceIndex)
     {
         throw new InvalidOperationException(
             "Head and tail cameras cannot use the same Sapera server and device index.");
     }

     if (settings.Acquisition.TargetFramePairsPerLog <= 0)
     {
         throw new InvalidOperationException(
             "Acquisition.TargetFramePairsPerLog must be greater than zero.");
     }

        if (settings.Acquisition.Concatenation == null)
        {
            throw new InvalidOperationException(
                "Acquisition.Concatenation settings are missing.");
        }

        settings.Acquisition.Concatenation.Validate();
    }


    /// <summary>
    /// Validates the shape and required properties of a camera device settings object, ensuring that
    /// it is properly configured for the expected camera position (head or tail).
    /// </summary>
    /// <param name="camera">The camera device settings to validate.</param>
    /// <param name="expectedPosition">The expected camera position (head or tail).</param>
    /// <exception cref="InvalidOperationException">Thrown if the camera device settings are missing or inconsistent.</exception>
    private static void ValidateCameraDeviceShape(
        CameraDeviceSettings camera,
        CameraPosition expectedPosition)
    {
        if (camera.Position != expectedPosition)
            throw new InvalidOperationException($"{expectedPosition} camera position is incorrect.");
        
        if (camera.DeviceIndex < 0)
            throw new InvalidOperationException($"{expectedPosition} camera DeviceIndex cannot be negative.");
        if (camera.BufferCount < 4)
            throw new InvalidOperationException($"{expectedPosition} camera BufferCount must be at least 4.");
        if (string.IsNullOrWhiteSpace(camera.PixelFormat))
            throw new InvalidOperationException($"{expectedPosition} camera PixelFormat is required.");
        if (!double.IsFinite(camera.ExposureTimeMicroseconds) || camera.ExposureTimeMicroseconds <= 0)
            throw new InvalidOperationException($"{expectedPosition} camera ExposureTimeMicroseconds must be positive and finite.");
        if (!double.IsFinite(camera.Gain) || camera.Gain < 0)
            throw new InvalidOperationException($"{expectedPosition} camera Gain must be non-negative and finite.");
        if (!double.IsFinite(camera.AcquisitionFrameRate) || camera.AcquisitionFrameRate <= 0)
            throw new InvalidOperationException($"{expectedPosition} camera AcquisitionFrameRate must be positive and finite.");
        if (camera.Mode == AcquisitionMode.Encoder && string.IsNullOrWhiteSpace(camera.TriggerSource))
            throw new InvalidOperationException($"{expectedPosition} camera TriggerSource is required in Encoder mode.");
        if (camera.Mode == AcquisitionMode.Encoder && string.IsNullOrWhiteSpace(camera.TriggerActivation))
            throw new InvalidOperationException($"{expectedPosition} camera TriggerActivation is required in Encoder mode.");
        if (camera.InitializationMode == CameraInitializationMode.ConfigurationFile &&
            string.IsNullOrWhiteSpace(camera.ConfigurationFileName))
        {
            throw new InvalidOperationException(
                $"{expectedPosition} camera ConfigurationFileName is required only when legacy CCF mode is selected.");
        }
    }

    /// <summary>
    /// Validates the operator view settings, ensuring that all required properties are present and consistent.
    /// </summary>
    /// <param name="settings">The application settings containing the operator view settings to validate.</param>
    /// <exception cref="InvalidOperationException">Thrown if the operator view settings are missing or inconsistent.</exception>
    private static void ValidateOperatorView(AppSettings settings)
    {
        if (settings.OperatorView == null)
            throw new InvalidOperationException("OperatorView settings are missing.");

        OperatorViewSettings view = settings.OperatorView;
        if (view.DisplayIndex < 0)
            throw new InvalidOperationException("OperatorView.DisplayIndex cannot be negative.");

        double[] percents =
        [
            view.GoodMinimumFramePercent,
            view.WarningMinimumFramePercent,
            view.GoodMinimumProfileCoveragePercent,
            view.WarningMinimumProfileCoveragePercent,
            view.GoodMinimumValidSlicePercent,
            view.WarningMinimumValidSlicePercent,
            view.MinimumAngularCoveragePercentPerSlice
        ];

        if (percents.Any(value => !IsPercent(value)))
            throw new InvalidOperationException("Operator-view percentages must be finite values between 0 and 100.");

        if (view.GoodMinimumFramePercent < view.WarningMinimumFramePercent ||
            view.GoodMinimumProfileCoveragePercent < view.WarningMinimumProfileCoveragePercent ||
            view.GoodMinimumValidSlicePercent < view.WarningMinimumValidSlicePercent)
        {
            throw new InvalidOperationException("Operator-view good thresholds cannot be lower than warning thresholds.");
        }

        if (!IsNonNegativeFinite(view.GoodMaximumMissingGapMM) ||
            !IsNonNegativeFinite(view.WarningMaximumMissingGapMM) ||
            view.GoodMaximumMissingGapMM > view.WarningMaximumMissingGapMM)
        {
            throw new InvalidOperationException("Operator-view missing-gap thresholds are invalid.");
        }

        if (!double.IsFinite(view.MaximumAngularGapDeg) ||
            view.MaximumAngularGapDeg <= 0.0 ||
            view.MaximumAngularGapDeg > 360.0)
        {
            throw new InvalidOperationException("OperatorView.MaximumAngularGapDeg must be greater than 0 and at most 360.");
        }
    }

    /// <summary>
    /// Validates the unit settings, ensuring that decimal precision for inches and millimeters is non-negative.
    /// </summary>
    /// <param name="settings">The application settings containing the unit settings to validate.</param>
    /// <exception cref="InvalidOperationException">Thrown if the unit settings are missing or inconsistent.</exception>
    private static void ValidateUnits(AppSettings settings)
    {
        if (settings.Units.InchesDecimals < 0)
            throw new InvalidOperationException("InchesDecimals cannot be negative.");

        if (settings.Units.MillimetersDecimals < 0)
            throw new InvalidOperationException("MillimetersDecimals cannot be negative.");
    }

    /// <summary>
    /// Validates the rotating scan settings, ensuring that minimum and preferred valid frames,
    /// expected angle step, and minimum profile points per frame are all positive and consistent.
    /// </summary>
    /// <param name="settings">The application settings containing the rotating scan settings to validate.</param>
    /// <exception cref="InvalidOperationException">Thrown if the rotating scan settings are missing or inconsistent.</exception>
    private static void ValidateRotatingScan(AppSettings settings)
    {
        if (settings.RotatingScan.MinimumValidFrames <= 0)
            throw new InvalidOperationException("MinimumValidFrames must be greater than zero.");

        if (settings.RotatingScan.PreferredValidFrames < settings.RotatingScan.MinimumValidFrames)
            throw new InvalidOperationException("PreferredValidFrames cannot be less than MinimumValidFrames.");

        if (settings.RotatingScan.ExpectedAngleStepDeg <= 0)
            throw new InvalidOperationException("ExpectedAngleStepDeg must be greater than zero.");

        if (settings.RotatingScan.MinimumProfilePointsPerFrame <= 0)
            throw new InvalidOperationException("MinimumProfilePointsPerFrame must be greater than zero.");

        if (!double.IsFinite(settings.RotatingScan.StationaryProfileChangeMM) ||
            settings.RotatingScan.StationaryProfileChangeMM <= 0)
            throw new InvalidOperationException("StationaryProfileChangeMM must be greater than zero.");

        if (!double.IsFinite(settings.RotatingScan.RevolutionClosureToleranceMM) ||
            settings.RotatingScan.RevolutionClosureToleranceMM <= 0)
            throw new InvalidOperationException("RevolutionClosureToleranceMM must be greater than zero.");

        if (!double.IsFinite(settings.RotatingScan.ApproximateClosureToleranceMM) ||
            settings.RotatingScan.ApproximateClosureToleranceMM < 0 ||
            settings.RotatingScan.ApproximateClosureToleranceMM > 10)
            throw new InvalidOperationException("ApproximateClosureToleranceMM must be between 0 and 10.");

        if (settings.RotatingScan.MinimumFramesPerRevolution < 3)
            throw new InvalidOperationException("MinimumFramesPerRevolution must be at least 3.");

        if (!double.IsFinite(settings.RotatingScan.MaximumCalibrationExtrapolationPixels) ||
            settings.RotatingScan.MaximumCalibrationExtrapolationPixels < 0 ||
            settings.RotatingScan.MaximumCalibrationExtrapolationPixels > 40)
            throw new InvalidOperationException("MaximumCalibrationExtrapolationPixels must be between 0 and 40.");

        if (settings.RotatingScan.FramesAcceptedAfterScanComplete < 0 ||
            settings.RotatingScan.FramesAcceptedAfterScanComplete > 60)
            throw new InvalidOperationException("FramesAcceptedAfterScanComplete must be between 0 and 60.");

        if (!double.IsFinite(settings.RotatingScan.LaserAngleOffsetDeg))
            throw new InvalidOperationException("LaserAngleOffsetDeg must be a finite number.");
    }

    /// <summary>
    /// Validates the geometry settings, ensuring that various geometric parameters are positive and within valid ranges.
    /// </summary>
    /// <param name="settings">The application settings containing the geometry settings to validate.</param>
    /// <exception cref="InvalidOperationException">Thrown if the geometry settings are missing or inconsistent.</exception>
    private static void ValidateGeometry(AppSettings settings)
    {
        if (settings.Geometry.LocalCenterlineSliceThicknessMM <= 0)
            throw new InvalidOperationException("LocalCenterlineSliceThicknessMM must be greater than zero.");

        if (settings.Geometry.MinPointsPerLocalCenterlineSlice <= 0)
            throw new InvalidOperationException("MinPointsPerLocalCenterlineSlice must be greater than zero.");

        if (settings.Geometry.RadiusFieldZBinSizeMM <= 0)
            throw new InvalidOperationException("RadiusFieldZBinSizeMM must be greater than zero.");

        if (settings.Geometry.RadiusFieldAngleBinSizeDeg <= 0 ||
            settings.Geometry.RadiusFieldAngleBinSizeDeg > 360)
        {
            throw new InvalidOperationException("RadiusFieldAngleBinSizeDeg must be between 0 and 360.");
        }

        if (settings.Geometry.MinPointsPerRadiusBin <= 0)
            throw new InvalidOperationException("MinPointsPerRadiusBin must be greater than zero.");
    }

    /// <summary>
    /// Validates the defect settings, ensuring that radius thresholds and weights are consistent and within valid ranges.
    /// </summary>
    /// <param name="settings">The application settings containing the defect settings to validate.</param>
    /// <exception cref="InvalidOperationException">Thrown if the defect settings are missing or inconsistent.</exception>
    private static void ValidateDefects(AppSettings settings)
    {
        if (settings.Defects.SevereLowRadiusThresholdMM >= settings.Defects.LowRadiusThresholdMM)
        {
            throw new InvalidOperationException(
                "SevereLowRadiusThresholdMM must be more negative than LowRadiusThresholdMM.");
        }

        if (settings.Defects.HighRadiusThresholdMM <= 0)
            throw new InvalidOperationException("HighRadiusThresholdMM must be greater than zero.");

        if (settings.Defects.LowRadiusWeight < 0 ||
            settings.Defects.SevereLowRadiusWeight < 0 ||
            settings.Defects.HighRadiusWeight < 0)
        {
            throw new InvalidOperationException("Defect weights cannot be negative.");
        }
    }

    /// <summary>
    /// Validates the optimization settings, ensuring that various optimization parameters are positive and within valid ranges.
    /// </summary>
    /// <param name="settings">The application settings containing the optimization settings to validate.</param>
    /// <exception cref="InvalidOperationException">Thrown if the optimization settings are missing or inconsistent.</exception>
    private static void ValidateOptimization(AppSettings settings)
    {
        if (settings.Optimization.MinimumImprovementPercent < 0)
            throw new InvalidOperationException("MinimumImprovementPercent cannot be negative.");

        if (settings.Optimization.CoarseMaxMoveMM <= 0)
            throw new InvalidOperationException("CoarseMaxMoveMM must be greater than zero.");

        if (settings.Optimization.CoarseStepMM <= 0)
            throw new InvalidOperationException("CoarseStepMM must be greater than zero.");

        if (settings.Optimization.MediumStepMM <= 0)
            throw new InvalidOperationException("MediumStepMM must be greater than zero.");

        if (settings.Optimization.FineStepMM <= 0)
            throw new InvalidOperationException("FineStepMM must be greater than zero.");
    }

    /// <summary>
    /// Validates the inscribed geometry settings, ensuring that angular bins, axis ratios, orientation steps, and
    /// tolerances are within valid ranges.
    /// </summary>
    /// <param name="settings">The application settings containing the inscribed geometry settings to validate.</param>
    /// <exception cref="InvalidOperationException">Thrown if the inscribed geometry settings are missing or inconsistent.</exception>
    private static void ValidateInscribedGeometry(AppSettings settings)
    {
        if (settings.InscribedGeometry == null)
            throw new InvalidOperationException("InscribedGeometry settings are missing.");

        var geometry = settings.InscribedGeometry;

        if (geometry.AngularBins < 12)
            throw new InvalidOperationException("InscribedGeometry.AngularBins must be at least 12.");

        if (geometry.MinimumValidAngularBins < 3 ||
            geometry.MinimumValidAngularBins > geometry.AngularBins)
        {
            throw new InvalidOperationException(
                "InscribedGeometry.MinimumValidAngularBins must be between 3 and AngularBins.");
        }

        if (geometry.SafetyMarginMM < 0)
            throw new InvalidOperationException("InscribedGeometry.SafetyMarginMM cannot be negative.");

        if (!double.IsFinite(geometry.OvalMinimumWaneAdvantagePercent) ||
            geometry.OvalMinimumWaneAdvantagePercent < 0 ||
            geometry.OvalMinimumWaneAdvantagePercent > 100)
        {
            throw new InvalidOperationException(
                "InscribedGeometry.OvalMinimumWaneAdvantagePercent must be between 0 and 100.");
        }

        if (geometry.MinimumAxisRatio <= 0 ||
            geometry.MinimumAxisRatio > 1)
        {
            throw new InvalidOperationException(
                "InscribedGeometry.MinimumAxisRatio must be greater than zero and no more than one.");
        }

        if (geometry.AxisRatioStep <= 0 ||
            geometry.AxisRatioStep > 1)
        {
            throw new InvalidOperationException(
                "InscribedGeometry.AxisRatioStep must be greater than zero and no more than one.");
        }

        if (geometry.OrientationStepDeg <= 0 ||
            geometry.OrientationStepDeg > 180)
        {
            throw new InvalidOperationException(
                "InscribedGeometry.OrientationStepDeg must be greater than zero and no more than 180.");
        }

        if (!double.IsFinite(geometry.CenterlinePullWeight) ||
            geometry.CenterlinePullWeight < 0 ||
            geometry.CenterlinePullWeight > 10)
        {
            throw new InvalidOperationException(
                "InscribedGeometry.CenterlinePullWeight must be between 0 and 10.");
        }

        if (geometry.GeometryTieToleranceMM < 0)
        {
            throw new InvalidOperationException(
                "InscribedGeometry.GeometryTieToleranceMM cannot be negative.");
        }
    }


    /// <summary>
    /// Validates the peel view settings, ensuring that veneer lengths, marker spacing, coverage percentages,
    /// and other related settings are within valid ranges.
    /// </summary>
    /// <param name="settings">The application settings containing the peel view settings to validate.</param>
    /// <exception cref="InvalidOperationException">Thrown if the peel view settings are missing or inconsistent.</exception>
    private static void ValidatePeelView(AppSettings settings)
    {
        if (settings.PeelView == null)
            throw new InvalidOperationException("PeelView settings are missing.");

        PeelViewSettings peel = settings.PeelView;

        if (!IsPositiveFinite(peel.VeneerLengthPerRevolutionInches))
            throw new InvalidOperationException("VeneerLengthPerRevolutionInches must be a positive finite value.");

        if (!IsPositiveFinite(peel.VeneerMarkerSpacingInches))
            throw new InvalidOperationException("VeneerMarkerSpacingInches must be a positive finite value.");

        if (!IsPercent(peel.MinimumUsableSheetCoveragePercent))
        {
            throw new InvalidOperationException(
                "MinimumUsableSheetCoveragePercent must be a finite value between 0 and 100.");
        }

        if (!IsPercent(peel.MaximumUsableSheetSevereWanePercent))
        {
            throw new InvalidOperationException(
                "MaximumUsableSheetSevereWanePercent must be a finite value between 0 and 100.");
        }

        if (peel.LengthBins <= 0 || peel.AngleBins < 12)
            throw new InvalidOperationException("PeelView LengthBins must be positive and AngleBins must be at least 12.");

        if (!IsPositiveFinite(peel.PixelsPerMM))
            throw new InvalidOperationException("PeelView.PixelsPerMM must be a positive finite value.");

        if (peel.TopMargin < 0 || peel.LeftMargin < 0 ||
            peel.RightMargin < 0 || peel.BottomMargin < 0)
        {
            throw new InvalidOperationException("Peel-view margins cannot be negative.");
        }

        if (!IsNonNegativeFinite(peel.LowSpotVisualWarningMM) ||
            peel.LowSpotVisualMaxRevolutions < 0 ||
            peel.SurfaceSmoothPasses < 0)
        {
            throw new InvalidOperationException(
                "Peel-view warning and smoothing settings cannot be negative and must be finite.");
        }

        if (!double.IsFinite(peel.OuterRadiusPercentile) ||
            peel.OuterRadiusPercentile <= 0.0 ||
            peel.OuterRadiusPercentile > 1.0)
        {
            throw new InvalidOperationException(
                "OuterRadiusPercentile must be a finite value greater than 0 and no more than 1.");
        }
    }


    /// <summary>
    /// Validates the usable radius and wane settings, ensuring that core diameter, veneer thickness, wane tolerances,
    /// </summary>
    /// <param name="settings">The application settings containing the usable radius and wane settings to validate.</param>
    /// <exception cref="InvalidOperationException">Thrown if the usable radius or wane settings are missing or inconsistent.</exception>
    private static void ValidateUsableRadiusAndWane(AppSettings settings)
    {
        if (settings.UsableRadius == null)
            throw new InvalidOperationException("UsableRadius settings are missing.");

        if (!IsPositiveFinite(settings.UsableRadius.CoreDiameterInches))
            throw new InvalidOperationException("CoreDiameterInches must be a positive finite value.");

        if (!IsPositiveFinite(settings.UsableRadius.VeneerThicknessMM))
            throw new InvalidOperationException("VeneerThicknessMM must be a positive finite value.");

        if (!IsNonNegativeFinite(settings.UsableRadius.ThinCleanupWarningMM))
            throw new InvalidOperationException("ThinCleanupWarningMM must be a non-negative finite value.");

        if (settings.Wane == null)
            throw new InvalidOperationException("Wane settings are missing.");

        if (!IsNonNegativeFinite(settings.Wane.WaneToleranceMM) ||
            !IsPositiveFinite(settings.Wane.SevereWaneMM) ||
            settings.Wane.SevereWaneMM <= settings.Wane.WaneToleranceMM)
        {
            throw new InvalidOperationException(
                "WaneToleranceMM must be non-negative and SevereWaneMM must be finite and greater than the wane tolerance.");
        }

        if (settings.Wane.MinAngleBinsPerWaneSlice <= 0)
            throw new InvalidOperationException("MinAngleBinsPerWaneSlice must be greater than zero.");
    }
    
    /// <summary>
    /// Validates the machine limits settings, ensuring that maximum tail and head movements and warning moves are within valid ranges.
    /// </summary>
    /// <param name="settings">The application settings containing the machine limits settings to validate.</param>
    /// <exception cref="InvalidOperationException">Thrown if the machine limits settings are missing or inconsistent.</exception>  
    private static void ValidateMachineLimits(AppSettings settings)
    {
        if (settings.MachineLimits == null)
            throw new InvalidOperationException("MachineLimits settings are missing.");

        if (!IsPositiveFinite(settings.MachineLimits.MaxTailMoveXMM) ||
            !IsPositiveFinite(settings.MachineLimits.MaxTailMoveYMM) ||
            !IsPositiveFinite(settings.MachineLimits.MaxHeadMoveXMM) ||
            !IsPositiveFinite(settings.MachineLimits.MaxHeadMoveYMM))
        {
            throw new InvalidOperationException(
                "Machine movement limits must be positive finite values.");
        }

        if (!IsNonNegativeFinite(settings.MachineLimits.WarningMoveMM))
            throw new InvalidOperationException("WarningMoveMM must be a non-negative finite value.");
    }

    /// <summary>
    /// Validates the production settings, ensuring that maximum dropped source frames, minimum point cloud points,
    /// minimum local centerline slices, and minimum binned radius cells are within valid ranges.
    /// </summary>
    /// <param name="settings">The application settings containing the production settings to validate.</param>
    /// <exception cref="InvalidOperationException">Thrown if the production settings are missing or inconsistent.</exception>
    private static void ValidateProduction(AppSettings settings)
    {
        if (settings.Production == null)
            throw new InvalidOperationException("Production settings are missing.");

        if (settings.Production.MaximumDroppedSourceFrames < 0)
            throw new InvalidOperationException("MaximumDroppedSourceFrames cannot be negative.");

        if (settings.Production.MinimumPointCloudPoints <= 0)
            throw new InvalidOperationException("MinimumPointCloudPoints must be greater than zero.");

        if (settings.Production.MinimumLocalCenterlineSlices <= 0)
            throw new InvalidOperationException("MinimumLocalCenterlineSlices must be greater than zero.");

        if (settings.Production.MinimumBinnedRadiusCells <= 0)
            throw new InvalidOperationException("MinimumBinnedRadiusCells must be greater than zero.");

        if (settings.Production.CommissioningApprovedUtc.HasValue &&
            string.IsNullOrWhiteSpace(settings.Production.CommissioningApprovedBy))
        {
            throw new InvalidOperationException(
                "CommissioningApprovedBy is required when CommissioningApprovedUtc is set.");
        }
    }

    /// <summary>
    /// Validates the PLC integration settings, ensuring that readback timeouts, float comparison tolerances,
    /// and scan complete timeouts are within valid ranges.
    /// </summary>
    /// <param name="settings">The application settings containing the PLC integration settings to validate.</param>
    /// <exception cref="InvalidOperationException">Thrown if the PLC integration settings are missing or inconsistent.</exception>
    private static void ValidatePlc(AppSettings settings)
    {
        if (settings.Plc == null)
            throw new InvalidOperationException("PLC integration settings are missing.");

        if (settings.Plc.ReadbackTimeoutMilliseconds < 250)
            throw new InvalidOperationException(
                "Plc.ReadbackTimeoutMilliseconds must be at least 250.");

        if (!IsNonNegativeFinite(settings.Plc.FloatComparisonTolerance))
            throw new InvalidOperationException(
                "Plc.FloatComparisonTolerance must be a non-negative finite value.");

        if (settings.Plc.ScanCompleteTimeoutMilliseconds < 1000)
            throw new InvalidOperationException(
                "Plc.ScanCompleteTimeoutMilliseconds must be at least 1000.");
    }

    /// <summary>
    /// Validates the safety settings, ensuring that knife clearance, peel thickness multipliers, and carriage-retract
    /// buffers are within valid ranges and consistent with veneer thickness.
    /// </summary>
    /// <param name="settings">The application settings containing the safety settings to validate.</param>
    /// <exception cref="InvalidOperationException">Thrown if the safety settings are missing or inconsistent.</exception>
    private static void ValidateSafety(AppSettings settings)
    {
        if (settings.Safety == null)
            throw new InvalidOperationException("Safety settings are missing.");

        if (!double.IsFinite(settings.Safety.KnifeClearancePeelThicknessMultiplier) ||
            settings.Safety.KnifeClearancePeelThicknessMultiplier < 10.0)
        {
            throw new InvalidOperationException(
                "KnifeClearancePeelThicknessMultiplier must be finite and cannot be less than the required 10x peel thickness.");
        }

        if (!IsNonNegativeFinite(settings.Safety.MinimumKnifeClearanceMM) ||
            !IsNonNegativeFinite(settings.Safety.AdditionalCarriageRetractBufferMM))
        {
            throw new InvalidOperationException(
                "Knife clearance and carriage-retract buffers must be non-negative finite values.");
        }

        double effectiveClearance = settings.Safety.ComputeKnifeClearanceMM(
            settings.UsableRadius.VeneerThicknessMM);

        if (!double.IsFinite(effectiveClearance) ||
            effectiveClearance + 1e-9 < 10.0 * settings.UsableRadius.VeneerThicknessMM)
        {
            throw new InvalidOperationException(
                "The effective carriage-retract clearance must remain at least ten veneer thicknesses.");
        }
    }

    /// <summary>
    /// Validates the recovery optimization settings, ensuring that at least one seed candidate strategy is included
    /// and that numerical parameters are within valid ranges.
    /// </summary>
    /// <param name="settings">The application settings containing the recovery optimization settings to validate.</param>
    /// <exception cref="InvalidOperationException">Thrown if the recovery optimization settings are missing or inconsistent.</exception>
    private static void ValidateRecoveryOptimization(AppSettings settings)
    {
        if (settings.RecoveryOptimization == null)
            throw new InvalidOperationException("Recovery optimization settings are missing.");

        RecoveryOptimizationSettings recovery = settings.RecoveryOptimization;

        if (!recovery.IncludeBaselineCandidate &&
            !recovery.IncludePenaltyCandidate &&
            !recovery.IncludeCylinderCandidate &&
            !recovery.IncludeOvalCandidate)
        {
            throw new InvalidOperationException(
                "Recovery optimization must include at least one seed candidate strategy.");
        }

        if (recovery.DirectRefinementRadiusMM < 0.0 ||
            !double.IsFinite(recovery.DirectRefinementRadiusMM))
        {
            throw new InvalidOperationException(
                "DirectRefinementRadiusMM must be finite and cannot be negative.");
        }

        if (recovery.DirectRefinementStepMM <= 0.0 ||
            !double.IsFinite(recovery.DirectRefinementStepMM))
        {
            throw new InvalidOperationException(
                "DirectRefinementStepMM must be a positive finite value.");
        }

        if (recovery.MaximumDirectRecoveryCandidates <= 0 ||
            recovery.MaximumDirectRecoveryCandidates > 10000)
        {
            throw new InvalidOperationException(
                "MaximumDirectRecoveryCandidates must be between 1 and 10000.");
        }

        if (recovery.MinimumRecoverySearchZBins <= 0)
            throw new InvalidOperationException("MinimumRecoverySearchZBins must be greater than zero.");

        if (recovery.MinimumRecoverySearchAngularBins <= 0 ||
            recovery.MinimumRecoverySearchAngularBins > 360)
        {
            throw new InvalidOperationException(
                "MinimumRecoverySearchAngularBins must be between 1 and 360.");
        }
    }
    
    /// <summary>
    /// Validates the storage settings, ensuring that the number of days to keep saved scans is positive.
    /// </summary>
    /// <param name="settings">The application settings containing the storage settings to validate.</param>
    /// <exception cref="InvalidOperationException">Thrown if the storage settings are missing or inconsistent.</exception> 
    private static void ValidateStorage(AppSettings settings)
    {
        if (settings.Storage == null)
            throw new InvalidOperationException("Storage settings are missing.");

        if (settings.Storage.DaysToKeepSavedScans <= 0)
        {
            throw new InvalidOperationException(
                "DaysToKeepSavedScans must be greater than zero.");
        }
    }

    /// <summary>
    /// Validates the database settings, ensuring that if database saving is enabled, a valid connection string is provided
    /// either in the settings or through the environment variable.
    /// </summary>
    /// <param name="settings">The application settings containing the database settings to validate.</param>
    /// <exception cref="InvalidOperationException">Thrown if the database settings are missing or inconsistent.</exception>
    private static void ValidateDatabase(AppSettings settings)
    {
        if (settings.Database == null)
            throw new InvalidOperationException("Database settings are missing.");

        if (settings.Database.EnableDatabaseSaving &&
            string.IsNullOrWhiteSpace(
                DatabaseConnectionStringResolver.Resolve(settings.Database)))
        {
            throw new InvalidOperationException(
                "Database saving is enabled, but no connection string was provided in settings or the " +
                $"{DatabaseConnectionStringResolver.EnvironmentVariableName} environment variable.");
        }
    }


    /// <summary>
    /// Validates the production setup settings, ensuring that if length calibration is configured, a valid pending
    /// length pixels per millimeter value is provided,
    /// </summary>
    /// <param name="settings">The application settings containing the production setup settings to validate.</param>
    /// <exception cref="InvalidOperationException">Thrown if the production setup settings are missing or inconsistent.</exception>
    private static void ValidateSetup(AppSettings settings)
    {
        if (settings.Setup == null)
        {
            throw new InvalidOperationException(
                "Production setup settings are missing.");
        }

        if (settings.Setup.LengthCalibrationConfigured &&
            !settings.Setup.ScannerCalibrationCreated &&
            (!double.IsFinite(settings.Setup.PendingLengthPixelsPerMM) ||
             settings.Setup.PendingLengthPixelsPerMM <= 0))
        {
            throw new InvalidOperationException(
                "Setup.PendingLengthPixelsPerMM must be positive when a new axial length calibration is marked configured.");
        }

        if (settings.Setup.ScannerCalibrationCreated &&
            string.IsNullOrWhiteSpace(settings.Setup.ActiveCalibrationId))
        {
            throw new InvalidOperationException(
                "Setup.ActiveCalibrationId is required when ScannerCalibrationCreated is true.");
        }
    }

    private static bool IsPositiveFinite(double value) =>
        double.IsFinite(value) && value > 0.0;

    private static bool IsNonNegativeFinite(double value) =>
        double.IsFinite(value) && value >= 0.0;

    private static bool IsPercent(double value) =>
        double.IsFinite(value) && value >= 0.0 && value <= 100.0;

}
