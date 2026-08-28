using System;
using System.Collections.Generic;
using System.Text;

namespace OptimizationXY.Charger.Kinematic;

{
    /// <summary>
    /// Configuration settings for the kinematics calculator.
    /// 
    /// This class encapsulates all runtime configuration options for the KinematicsCalculator,
    /// allowing different systems or scenarios to have different rotation zone limits and
    /// calibration offset settings.
    /// 
    /// Configuration is validated immediately during calculator instantiation to catch
    /// invalid settings early and ensure the calculator always operates with valid parameters.
    /// </summary>
    public class KinematicsConfig
    {
        /// <summary>
        /// Left boundary of the rotation zone (danger zone) in degrees (0-360 range).
        /// 
        /// Components with angles in the range [LeftLimit, RightLimit] are considered "in zone"
        /// and require rotation to avoid collisions with the spindle or other fixed obstacles.
        /// 
        /// This typically represents an angle in the upper half of the circular range (e.g., 135°),
        /// which corresponds to the region where mechanical collisions would occur.
        /// 
        /// Default: 135 degrees
        /// Valid Range: 0-360 degrees
        /// Constraint: Must be less than RightLimit
        /// </summary>
        public double RotationZoneLeftLimit { get; set; } = 135;

        /// <summary>
        /// Right boundary of the rotation zone (danger zone) in degrees (0-360 range).
        /// 
        /// Components with angles in the range [LeftLimit, RightLimit] are considered "in zone"
        /// and require rotation to avoid collisions with the spindle or other fixed obstacles.
        /// 
        /// This typically represents an angle in the lower portion of the circular range (e.g., 315°),
        /// completing the restricted zone that began at the LeftLimit.
        /// 
        /// Default: 315 degrees
        /// Valid Range: 0-360 degrees
        /// Constraint: Must be greater than LeftLimit
        /// </summary>
        public double RotationZoneRightLimit { get; set; } = 315;

        /// <summary>
        /// Enable XY correction offsets for machine calibration.
        /// 
        /// When enabled (true), applies predefined machine-specific offset values to the final
        /// calculated X and Y positions to compensate for mechanical tolerances, measurement errors,
        /// wear, or other calibration issues in the physical equipment.
        /// 
        /// When disabled (false), the raw calculated positions are returned without adjustment.
        /// 
        /// The actual offset values are defined as constants in the KinematicsCalculator:
        /// - HEAD_X_OFFSET, HEAD_Y_OFFSET (for dog/head component)
        /// - TAIL_X_OFFSET, TAIL_Y_OFFSET (for tail component)
        /// 
        /// Default: false (offsets disabled, raw calculations used)
        /// </summary>
        public bool EnableXYCorrectionOffset { get; set; } = false;

        /// <summary>
        /// Validates the configuration settings.
        /// 
        /// This method is called automatically when creating a new KinematicsCalculator
        /// with this configuration. It ensures all settings are within valid ranges and
        /// consistent with each other before the calculator begins processing.
        /// 
        /// VALIDATION RULES:
        /// 1. RotationZoneLeftLimit must be between 0 and 360 degrees (inclusive)
        /// 2. RotationZoneRightLimit must be between 0 and 360 degrees (inclusive)
        /// 3. RotationZoneLeftLimit must be strictly less than RotationZoneRightLimit
        /// 
        /// These constraints ensure that a valid rotation zone can be defined.
        /// </summary>
        /// <exception cref="ArgumentException">
        /// Thrown if any configuration value is outside valid range or constraints are violated.
        /// Exception message will detail which constraint failed and the invalid values.
        /// </exception>
        public void Validate()
        {
            // Check LeftLimit is within valid degree range
            if (RotationZoneLeftLimit < 0 || RotationZoneLeftLimit > 360)
            {
                throw new ArgumentException(
                    $"RotationZoneLeftLimit must be between 0 and 360 degrees. Got: {RotationZoneLeftLimit}");
            }

            // Check RightLimit is within valid degree range
            if (RotationZoneRightLimit < 0 || RotationZoneRightLimit > 360)
            {
                throw new ArgumentException(
                    $"RotationZoneRightLimit must be between 0 and 360 degrees. Got: {RotationZoneRightLimit}");
            }

            // Check that LeftLimit is strictly less than RightLimit to form a valid zone
            if (RotationZoneLeftLimit >= RotationZoneRightLimit)
            {
                throw new ArgumentException(
                    $"RotationZoneLeftLimit ({RotationZoneLeftLimit}) must be less than RotationZoneRightLimit ({RotationZoneRightLimit})");
            }
        }
    }
}
