using System;

namespace OptimizationXY.Charger.Kinematic;

/// <summary>
/// Serializable diagnostic state that captures the complete kinematic rotation logic calculation.
/// 
/// This class is used to record every step of the kinematic calculation for verification, testing,
/// and offline analysis of the software rotation logic versus actual PLC rotation behavior.
/// 
/// The state includes:
/// - Original optimization input positions (Head X/Y, Tail X/Y in MM)
/// - Input angle calculations (which angles trigger rotation zones)
/// - Zone detection results (which components are in danger zone)
/// - Rotation case determination (which rotation case was applied)
/// - Intermediate results after rotation applied
/// - Final corrected positions (what goes to PLC)
/// - Distance calculations and angle adjustments
/// 
/// This entire state can be serialized to JSON and saved with production logs
/// for offline verification and debugging.
/// </summary>
public sealed class KinematicVisualizationState
{
    // ========================================================
    // SECTION 1: INPUT POSITIONS
    // Original XY values from Optimization software (millimeters)
    // ========================================================

    /// <summary>
    /// Original Head (Dog) X position from Optimization, in millimeters.
    /// This is the input to the kinematic calculator before any rotation.
    /// </summary>
    public double InputHeadXMM { get; set; }

    /// <summary>
    /// Original Head (Dog) Y position from Optimization, in millimeters.
    /// This is the input to the kinematic calculator before any rotation.
    /// </summary>
    public double InputHeadYMM { get; set; }

    /// <summary>
    /// Original Tail X position from Optimization, in millimeters.
    /// This is the input to the kinematic calculator before any rotation.
    /// </summary>
    public double InputTailXMM { get; set; }

    /// <summary>
    /// Original Tail Y position from Optimization, in millimeters.
    /// This is the input to the kinematic calculator before any rotation.
    /// </summary>
    public double InputTailYMM { get; set; }

    // ========================================================
    // SECTION 2: INPUT ANGLE CALCULATIONS
    // Angles calculated from input positions (0-360 degrees)
    // ========================================================

    /// <summary>
    /// Calculated angle of Head component from spindle center origin, in degrees (0-360).
    /// Used to determine if Head is in rotation zone (danger zone).
    /// </summary>
    public double InputHeadAngleDeg { get; set; }

    /// <summary>
    /// Calculated angle of Tail component from spindle center origin, in degrees (0-360).
    /// Used to determine if Tail is in rotation zone (danger zone).
    /// </summary>
    public double InputTailAngleDeg { get; set; }

    // ========================================================
    // SECTION 3: ZONE DETECTION
    // Boolean flags indicating which components need rotation
    // ========================================================

    /// <summary>
    /// True if Head angle is within the rotation zone (danger zone), false if safe.
    /// When true, Head component requires spindle rotation to avoid collision.
    /// </summary>
    public bool HeadInRotationZone { get; set; }

    /// <summary>
    /// True if Tail angle is within the rotation zone (danger zone), false if safe.
    /// When true, Tail component requires spindle rotation to avoid collision.
    /// </summary>
    public bool TailInRotationZone { get; set; }

    // ========================================================
    // SECTION 4: ROTATION ZONE CONFIGURATION
    // The boundaries of the danger zone in degrees
    // ========================================================

    /// <summary>
    /// Left boundary of the rotation zone (danger zone) in degrees (0-360).
    /// Components with angles in range [RotationZoneLeftLimit, RotationZoneRightLimit] need rotation.
    /// Default: 135 degrees
    /// </summary>
    public double RotationZoneLeftLimitDeg { get; set; }

    /// <summary>
    /// Right boundary of the rotation zone (danger zone) in degrees (0-360).
    /// Components with angles in range [RotationZoneLeftLimit, RotationZoneRightLimit] need rotation.
    /// Default: 315 degrees
    /// </summary>
    public double RotationZoneRightLimitDeg { get; set; }

    // ========================================================
    // SECTION 5: ROTATION CASE DETERMINATION
    // Which rotation case was applied (Both, Head Only, Tail Only, Neither)
    // ========================================================

    /// <summary>
    /// Which rotation case was applied.
    /// Possible values:
    /// - "BOTH_REQUIRE_ROTATION" - Both Head and Tail in danger zone
    /// - "HEAD_ONLY_REQUIRES_ROTATION" - Only Head needs rotation
    /// - "TAIL_ONLY_REQUIRES_ROTATION" - Only Tail needs rotation
    /// - "NEITHER_REQUIRES_ROTATION" - Both components safe, no rotation applied
    /// </summary>
    public string RotationCase { get; set; } = string.Empty;

    /// <summary>
    /// Additional details about which rotation branch was taken within the case.
    /// Used for detailed debugging and verification of rotation logic.
    /// Example: "Head-Only Case A: rotate Head to Left Limit"
    /// </summary>
    public string RotationBranch { get; set; } = string.Empty;

    // ========================================================
    // SECTION 6: ROTATION CALCULATION INTERMEDIATES
    // Distance calculations used to determine rotation amount
    // ========================================================

    /// <summary>
    /// Radial distance from spindle center to Head component, in millimeters.
    /// Preserved during rotation (only angle changes).
    /// </summary>
    public double HeadRadialDistanceMM { get; set; }

    /// <summary>
    /// Radial distance from spindle center to Tail component, in millimeters.
    /// Preserved during rotation (only angle changes).
    /// </summary>
    public double TailRadialDistanceMM { get; set; }

    /// <summary>
    /// Angular distance from Head to left boundary of rotation zone, in degrees.
    /// Used to calculate minimum rotation needed.
    /// </summary>
    public double HeadDistanceFromLeftLimitDeg { get; set; }

    /// <summary>
    /// Angular distance from Head to right boundary of rotation zone, in degrees.
    /// Used to calculate minimum rotation needed.
    /// </summary>
    public double HeadDistanceFromRightLimitDeg { get; set; }

    /// <summary>
    /// Angular distance from Tail to left boundary of rotation zone, in degrees.
    /// Used to calculate minimum rotation needed.
    /// </summary>
    public double TailDistanceFromLeftLimitDeg { get; set; }

    /// <summary>
    /// Angular distance from Tail to right boundary of rotation zone, in degrees.
    /// Used to calculate minimum rotation needed.
    /// </summary>
    public double TailDistanceFromRightLimitDeg { get; set; }

    /// <summary>
    /// Intermediate angle A used in complex rotation cases.
    /// Used for detailed debugging of rotation algorithm.
    /// </summary>
    public double IntermediateAngleADeg { get; set; }

    /// <summary>
    /// Intermediate angle B used in complex rotation cases.
    /// Used for detailed debugging of rotation algorithm.
    /// </summary>
    public double IntermediateAngleBDeg { get; set; }

    /// <summary>
    /// Total spindle rotation applied to both components, in degrees.
    /// Positive value = counterclockwise rotation, negative = clockwise.
    /// </summary>
    public double SpindleRotationDeg { get; set; }

    // ========================================================
    // SECTION 7: ROTATED ANGLE RESULTS
    // Angles after spindle rotation applied
    // ========================================================

    /// <summary>
    /// Head angle after spindle rotation applied, in degrees (0-360).
    /// This is the final angle BEFORE optional correction offsets.
    /// </summary>
    public double RotatedHeadAngleDeg { get; set; }

    /// <summary>
    /// Tail angle after spindle rotation applied, in degrees (0-360).
    /// This is the final angle BEFORE optional correction offsets.
    /// </summary>
    public double RotatedTailAngleDeg { get; set; }

    // ========================================================
    // SECTION 8: CORRECTED POSITIONS (BEFORE OFFSETS)
    // XY positions after rotation but BEFORE optional correction offsets
    // ========================================================

    /// <summary>
    /// Head X position after rotation applied, before correction offsets, in millimeters.
    /// Calculated as: RadialDistance × cos(RotatedAngle)
    /// </summary>
    public double RotatedHeadXMM { get; set; }

    /// <summary>
    /// Head Y position after rotation applied, before correction offsets, in millimeters.
    /// Calculated as: RadialDistance × sin(RotatedAngle)
    /// </summary>
    public double RotatedHeadYMM { get; set; }

    /// <summary>
    /// Tail X position after rotation applied, before correction offsets, in millimeters.
    /// Calculated as: RadialDistance × cos(RotatedAngle)
    /// </summary>
    public double RotatedTailXMM { get; set; }

    /// <summary>
    /// Tail Y position after rotation applied, before correction offsets, in millimeters.
    /// Calculated as: RadialDistance × sin(RotatedAngle)
    /// </summary>
    public double RotatedTailYMM { get; set; }

    // ========================================================
    // SECTION 9: CORRECTION OFFSET CONFIGURATION
    // Whether machine calibration offsets were applied
    // ========================================================

    /// <summary>
    /// True if machine calibration correction offsets were applied to final positions.
    /// When false, raw rotation-corrected positions are used.
    /// </summary>
    public bool CorrectionOffsetsApplied { get; set; }

    /// <summary>
    /// Machine calibration offset applied to Head X, in millimeters.
    /// Only applied if CorrectionOffsetsApplied is true.
    /// Compensates for mechanical tolerances and calibration errors.
    /// </summary>
    public double HeadXCorrectionOffsetMM { get; set; }

    /// <summary>
    /// Machine calibration offset applied to Head Y, in millimeters.
    /// Only applied if CorrectionOffsetsApplied is true.
    /// Compensates for mechanical tolerances and calibration errors.
    /// </summary>
    public double HeadYCorrectionOffsetMM { get; set; }

    /// <summary>
    /// Machine calibration offset applied to Tail X, in millimeters.
    /// Only applied if CorrectionOffsetsApplied is true.
    /// Compensates for mechanical tolerances and calibration errors.
    /// </summary>
    public double TailXCorrectionOffsetMM { get; set; }

    /// <summary>
    /// Machine calibration offset applied to Tail Y, in millimeters.
    /// Only applied if CorrectionOffsetsApplied is true.
    /// Compensates for mechanical tolerances and calibration errors.
    /// </summary>
    public double TailYCorrectionOffsetMM { get; set; }

    // ========================================================
    // SECTION 10: FINAL CORRECTED POSITIONS
    // These are the final XY values that go to the PLC
    // This is the OUTPUT of the kinematic calculation
    // ========================================================

    /// <summary>
    /// Final corrected Head X position sent to PLC, in millimeters.
    /// This is after rotation and optional correction offsets applied.
    /// This value replaces the original Optimization Head X when Kinematic is selected.
    /// </summary>
    public double FinalHeadXMM { get; set; }

    /// <summary>
    /// Final corrected Head Y position sent to PLC, in millimeters.
    /// This is after rotation and optional correction offsets applied.
    /// This value replaces the original Optimization Head Y when Kinematic is selected.
    /// </summary>
    public double FinalHeadYMM { get; set; }

    /// <summary>
    /// Final corrected Tail X position sent to PLC, in millimeters.
    /// This is after rotation and optional correction offsets applied.
    /// This value replaces the original Optimization Tail X when Kinematic is selected.
    /// </summary>
    public double FinalTailXMM { get; set; }

    /// <summary>
    /// Final corrected Tail Y position sent to PLC, in millimeters.
    /// This is after rotation and optional correction offsets applied.
    /// This value replaces the original Optimization Tail Y when Kinematic is selected.
    /// </summary>
    public double FinalTailYMM { get; set; }

    // ========================================================
    // SECTION 11: VALIDATION & CALCULATION METADATA
    // ========================================================

    /// <summary>
    /// Timestamp when this kinematic calculation was performed.
    /// Used for audit trail and correlation with PLC logs.
    /// </summary>
    public DateTime CalculatedAtUtc { get; set; }

    /// <summary>
    /// True if the kinematic calculation completed successfully and all outputs are valid.
    /// False if an error occurred or any output value is NaN/Infinity.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Error message if calculation failed, empty string if successful.
    /// Contains specific details about validation failures for debugging.
    /// </summary>
    public string ValidationErrorMessage { get; set; } = string.Empty;

    /// <summary>
    /// Total difference between input and output Head position, in millimeters.
    /// Calculated as: sqrt((FinalHeadX - InputHeadX)² + (FinalHeadY - InputHeadY)²)
    /// Useful for understanding magnitude of rotation-induced position change.
    /// </summary>
    public double HeadPositionChangeMM { get; set; }

    /// <summary>
    /// Total difference between input and output Tail position, in millimeters.
    /// Calculated as: sqrt((FinalTailX - InputTailX)² + (FinalTailY - InputTailY)²)
    /// Useful for understanding magnitude of rotation-induced position change.
    /// </summary>
    public double TailPositionChangeMM { get; set; }

    /// <summary>
    /// Calculates and returns the Head position change magnitude.
    /// </summary>
    public double CalculateHeadPositionChange()
    {
        double deltaX = FinalHeadXMM - InputHeadXMM;
        double deltaY = FinalHeadYMM - InputHeadYMM;
        return Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
    }

    /// <summary>
    /// Calculates and returns the Tail position change magnitude.
    /// </summary>
    public double CalculateTailPositionChange()
    {
        double deltaX = FinalTailXMM - InputTailXMM;
        double deltaY = FinalTailYMM - InputTailYMM;
        return Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
    }

    /// <summary>
    /// Creates a human-readable summary of the kinematic state.
    /// Useful for diagnostics and logging.
    /// </summary>
    public override string ToString()
    {
        return $"KinematicVisualizationState: {RotationCase} | " +
               $"Head: ({InputHeadXMM:F2},{InputHeadYMM:F2})→({FinalHeadXMM:F2},{FinalHeadYMM:F2}) | " +
               $"Tail: ({InputTailXMM:F2},{InputTailYMM:F2})→({FinalTailXMM:F2},{FinalTailYMM:F2}) | " +
               $"Rotation: {SpindleRotationDeg:F2}° | Valid: {IsValid}";
    }
}
