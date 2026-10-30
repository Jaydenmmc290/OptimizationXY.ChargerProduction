                                 using System;

namespace OptimizationXY.Charger.Kinematic;

/// <summary>
/// Thread-safe kinematics calculator that performs all mathematical calculations
/// without accessing UI controls. This class is safe to use from any thread,
/// including parallel worker threads.
    /// 
    /// This calculator implements forward kinematics logic that:
    /// 1. Takes input positions for the dog (head) and tail components
    /// 2. Calculates their angles from the spindle center
    /// 3. Determines if components are in the rotation zone (danger zone)
    /// 4. Applies rotation logic to move components out of the restricted zone
    /// 5. Converts final positions to actuator cylinder targets (A1, B1, A2, B2)
    /// 6. Optionally applies machine calibration correction offsets
    /// </summary>
    public class KinematicsCalculator
    {
        // ============================================================
        // PLC CONSTANTS
        // These constants are derived from the PLC code and must remain
        // synchronized with the hardware values.
        // ============================================================

        /// <summary>
        /// Constant used for converting radians to degrees.
        /// This approximates 180 / PI and is the same constant used by the PLC.
        /// </summary>
        private const double RadToDeg = 57.2958;

        /// <summary>
        /// PLC HOME POSITION CYLINDER PIN-TO-PIN LENGTHS (in appropriate units)
        /// These are the base lengths of the pneumatic cylinders in their home positions.
        /// The actual position is calculated as: home_length + offset_from_calculations
        /// B1 and B2 control Y-axis movement (vertical)
        /// A1 and A2 control X-axis movement (horizontal)
        /// </summary>
        private const double B1_Y_Pin2Pin_Length = 25.35741;
        private const double A1_X_Pin2Pin_Length = 23.68214;
        private const double B2_Y_Pin2Pin_Length = 24.493008;
        private const double A2_X_Pin2Pin_Length = 24.138481;

        /// <summary>
        /// Machine Calibration Correction Offsets
        /// These offsets are applied to the final calculated positions to compensate
        /// for mechanical tolerances and calibration errors in the physical system.
        /// They are only applied when EnableXYCorrectionOffset is true.
        /// HEAD_ offsets apply to the dog (first component)
        /// TAIL_ offsets apply to the tail (second component)
        /// </summary>
        private const double HEAD_X_OFFSET = -1.0893002;
        private const double HEAD_Y_OFFSET = 0.34380007;
        private const double TAIL_X_OFFSET = -0.11765;
        private const double TAIL_Y_OFFSET = 0.500300;

        // ============================================================
        // CONFIGURATION
        // This holds the runtime configuration for rotation zones and offsets.
        // ============================================================

        /// <summary>
        /// The configuration object that defines rotation zone limits and correction offset settings.
        /// Configuration is immutable after construction to ensure thread-safety.
        /// </summary>
        private readonly KinematicsConfig _config;

        /// <summary>
        /// Creates a new KinematicsCalculator with default configuration.
        /// Default rotation zone (danger zone): Left = 135°, Right = 315°
        /// Default correction offsets: disabled
        /// 
        /// This constructor is a convenience method that uses the parameterless
        /// KinematicsConfig constructor to create default settings.
        /// </summary>
        public KinematicsCalculator() : this(new KinematicsConfig())
        {
        }

        /// <summary>
        /// Creates a new KinematicsCalculator with custom configuration.
        /// 
        /// The configuration is validated immediately upon construction.
        /// If the configuration is invalid, an ArgumentException is thrown
        /// before the object is fully initialized.
        /// </summary>
        /// <param name="config">Configuration settings for the calculator</param>
        /// <exception cref="ArgumentNullException">If config is null</exception>
        /// <exception cref="ArgumentException">If config settings are invalid</exception>
        public KinematicsCalculator(KinematicsConfig config)
        {
        ArgumentNullException.ThrowIfNull(config);

        config.Validate();
            _config = config;
        }

        /// <summary>
        /// Gets the current configuration.
        /// This property provides read-only access to the calculator's configuration.
        /// </summary>
        public KinematicsConfig Config => _config;

        /// <summary>
        /// Performs forward kinematics calculation.
        /// 
        /// PROCESS OVERVIEW:
        /// Takes Head X/Y and Tail X/Y positions and calculates the corresponding actuator
        /// positions (A1, B1, A2, B2) required to achieve those end-effector positions.
        /// 
        /// The calculation includes:
        /// 1. Angle calculation for both dog (head) and tail components from spindle center
        /// 2. Distance calculation from spindle center to each component
        /// 3. Zone detection - determines if components are in the rotation zone (danger zone)
        /// 4. Rotation logic - calculates spindle rotation needed to move components out of danger zone
        /// 5. Position recalculation with rotation applied
        /// 6. Optional correction offset application for machine calibration
        /// 7. Cylinder target calculation by adding offsets to home positions
        /// 
        /// This method is thread-safe and can be called from any thread.
        /// </summary>
        /// <param name="headX">X coordinate of the dog (head) component input position</param>
        /// <param name="headY">Y coordinate of the dog (head) component input position</param>
        /// <param name="tailX">X coordinate of the tail component input position</param>
        /// <param name="tailY">Y coordinate of the tail component input position</param>
        /// <param name="a1">OUTPUT: Target position for A1 (X-axis head actuator)</param>
        /// <param name="b1">OUTPUT: Target position for B1 (Y-axis head actuator)</param>
        /// <param name="a2">OUTPUT: Target position for A2 (X-axis tail actuator)</param>
        /// <param name="b2">OUTPUT: Target position for B2 (Y-axis tail actuator)</param>
        public void CalculateForwardKinematics(
            double headX, double headY, double tailX, double tailY,
            out double a1, out double b1, out double a2, out double b2)
        {
            // Initialize outputs to NaN in case of error
            a1 = b1 = a2 = b2 = double.NaN;

            // Alias input parameters with more descriptive names for clarity
            // "dog" and "tail" are the component names used in the PLC
            double dogXInput = headX;
            double dogYInput = headY;
            double tailXInput = tailX;
            double tailYInput = tailY;

            // ========================================================
            // STEP 1: CALCULATE DOG ANGLE
            // ========================================================
            // The angle is calculated from the spindle center (0,0).
            // This uses manual quadrant checking to match the PLC algorithm exactly.
            // References PLC LINES ~101-119

            double dogLCAngle = CalculateAngle(dogXInput, dogYInput);

            // ========================================================
            // STEP 2: CALCULATE TAIL ANGLE
            // ========================================================
            // Same angle calculation logic as the dog.
            // References PLC LINES ~121-138

            double tailLCAngle = CalculateAngle(tailXInput, tailYInput);

            // ========================================================
            // STEP 3: CALCULATE DISTANCES FROM SPINDLE CENTER
            // ========================================================
            // These are the radial distances from the center (0,0) to each component.
            // Used later to reconstruct the position after rotation is applied.

            double dogRotSC2LC = Math.Sqrt((dogXInput * dogXInput) + (dogYInput * dogYInput));
            double tailRotSC2LC = Math.Sqrt((tailXInput * tailXInput) + (tailYInput * tailYInput));

            // ========================================================
            // STEP 4: DETERMINE IF DOG AND TAIL ARE IN ROTATION ZONE
            // ========================================================
            // The rotation zone is a restricted angular range [LeftLimit, RightLimit].
            // A component is IN the zone if its angle falls within this range.
            // inZone value: 0 = in zone (needs rotation), 1 = outside zone (safe)
            // This logic matches the PLC "IF Angle within left/right limit THEN 0 ELSE 1"

            int dogLCInZone = (dogLCAngle >= _config.RotationZoneLeftLimit && dogLCAngle <= _config.RotationZoneRightLimit) ? 0 : 1;
            int tailLCInZone = (tailLCAngle >= _config.RotationZoneLeftLimit && tailLCAngle <= _config.RotationZoneRightLimit) ? 0 : 1;

            // ========================================================
            // STEP 5: CALCULATE DISTANCES FROM ROTATION LIMITS
            // ========================================================
            // These values represent how close each component is to each boundary.
            // Used to determine the minimum rotation amount needed.

            double dogFromLeft = dogLCAngle - _config.RotationZoneLeftLimit;
            double dogFromRight = _config.RotationZoneRightLimit - dogLCAngle;
            double tailFromLeft = tailLCAngle - _config.RotationZoneLeftLimit;
            double tailFromRight = _config.RotationZoneRightLimit - tailLCAngle;

            // ========================================================
            // STEP 6: DETERMINE ROTATION CASE AND APPLY ROTATION LOGIC
            // ========================================================
            // The rotation logic is complex because we need to find the minimum spindle
            // rotation that moves BOTH components out of the danger zone.
            // There are 4 cases:
            //   A) Both need rotation
            //   B) Only dog needs rotation
            //   C) Only tail needs rotation
            //   D) Neither needs rotation

            double spindleRotateCalc;  // Amount to rotate the spindle (degrees)
            double newDogAngle = dogLCAngle;    // New angle after rotation
            double newTailAngle = tailLCAngle;  // New angle after rotation

            if (dogLCInZone == 0 && tailLCInZone == 0)
            {
                // CASE A: BOTH COMPONENTS NEED ROTATION
                // Find the minimum rotation that clears both components from the danger zone.

                double angleA, angleB;

                if (dogLCAngle > tailLCAngle)
                {
                    if (dogFromRight > tailFromLeft)
                    {
                        angleA = dogFromRight;
                        angleB = tailFromLeft;
                    }
                    else
                    {
                        angleA = tailFromLeft;
                        angleB = dogFromRight;
                    }
                }
                else
                {
                    if (tailFromRight > dogFromLeft)
                    {
                        angleA = tailFromRight;
                        angleB = dogFromLeft;
                    }
                    else
                    {
                        angleA = dogFromLeft;
                        angleB = tailFromRight;
                    }
                }

                spindleRotateCalc = angleA + angleB;
                newDogAngle = dogLCAngle + spindleRotateCalc;
                newTailAngle = tailLCAngle + spindleRotateCalc;
            }
            else if (dogLCInZone == 0 && tailLCInZone == 1)
            {
                // CASE B: ONLY DOG NEEDS ROTATION
                if (dogFromRight > dogFromLeft)
                {
                    spindleRotateCalc = dogFromRight;
                }
                else
                {
                    spindleRotateCalc = -dogFromLeft;
                }

                newDogAngle = dogLCAngle + spindleRotateCalc;
                newTailAngle = tailLCAngle + spindleRotateCalc;
            }
            else if (dogLCInZone == 1 && tailLCInZone == 0)
            {
                // CASE C: ONLY TAIL NEEDS ROTATION
                if (tailFromRight > tailFromLeft)
                {
                    spindleRotateCalc = tailFromRight;
                }
                else
                {
                    spindleRotateCalc = -tailFromLeft;
                }

                newDogAngle = dogLCAngle + spindleRotateCalc;
                newTailAngle = tailLCAngle + spindleRotateCalc;
            }
            // CASE D: Neither needs rotation - angles remain unchanged

            // ========================================================
            // STEP 7: CALCULATE FINAL DOG AND TAIL POSITIONS
            // ========================================================
            // Convert the new angles back to (X, Y) positions using the original
            // distances and the rotated angles.

            double newDogAngleRad = newDogAngle / RadToDeg;
            double newTailAngleRad = newTailAngle / RadToDeg;

            double dogXCalc = dogRotSC2LC * Math.Cos(newDogAngleRad);
            double dogYCalc = dogRotSC2LC * Math.Sin(newDogAngleRad);
            double tailXCalc = tailRotSC2LC * Math.Cos(newTailAngleRad);
            double tailYCalc = tailRotSC2LC * Math.Sin(newTailAngleRad);

            // ========================================================
            // STEP 8: APPLY OPTIONAL CORRECTION OFFSETS
            // ========================================================
            // These offsets compensate for mechanical tolerances and calibration errors.
            // They are only applied when machine correction is enabled.

            if (_config.EnableXYCorrectionOffset)
            {
                dogXCalc += HEAD_X_OFFSET;
                dogYCalc += HEAD_Y_OFFSET;
                tailXCalc += TAIL_X_OFFSET;
                tailYCalc += TAIL_Y_OFFSET;
            }

            // ========================================================
            // STEP 9: CALCULATE CYLINDER TARGETS (A1, B1, A2, B2)
            // ========================================================
            // The final step converts the calculated positions into actuator
            // (pneumatic cylinder) targets by adding the calculated offsets
            // to the home position lengths.
            // 
            // NOTE: B1 and B2 are INVERTED - we subtract the Y calculation
            // because the PLC cylinder logic is inverted on the Y-axis.

            a1 = A1_X_Pin2Pin_Length + dogXCalc;
            b1 = B1_Y_Pin2Pin_Length - dogYCalc;  // Note: inverted
            a2 = A2_X_Pin2Pin_Length + tailXCalc;
            b2 = B2_Y_Pin2Pin_Length - tailYCalc;  // Note: inverted
        }

        /// <summary>
        /// Calculates corrected XY positions for head (dog) and tail components
        /// after applying rotation logic to avoid the danger zone.
        /// 
        /// PRIMARY OUTPUT METHOD FOR OPTIMIZATION SOFTWARE INTEGRATION:
        /// This is the method to call when integrating with an external optimization software.
        /// 
        /// PROCESS:
        /// 1. Accepts input XY positions from optimization software
        /// 2. Applies kinematics rotation logic to move components out of danger zone
        /// 3. Applies optional machine calibration correction offsets
        /// 4. Returns the final corrected XY positions
        /// 
        /// The A1, B1, A2, B2 actuator positions are calculated internally but
        /// not returned by this method. Call CalculateForwardKinematics() if you need those.
        /// 
        /// This method is thread-safe and can be called from any thread.
        /// </summary>
        /// <param name="headX">X coordinate of the head (dog) component input position</param>
        /// <param name="headY">Y coordinate of the head (dog) component input position</param>
        /// <param name="tailX">X coordinate of the tail component input position</param>
        /// <param name="tailY">Y coordinate of the tail component input position</param>
        /// <param name="correctedHeadX">OUTPUT: Corrected X position of head after rotation and offsets</param>
        /// <param name="correctedHeadY">OUTPUT: Corrected Y position of head after rotation and offsets</param>
        /// <param name="correctedTailX">OUTPUT: Corrected X position of tail after rotation and offsets</param>
        /// <param name="correctedTailY">OUTPUT: Corrected Y position of tail after rotation and offsets</param>
        public void CalculateCorrectedPositions(
            double headX, double headY, double tailX, double tailY,
            out double correctedHeadX, out double correctedHeadY,
            out double correctedTailX, out double correctedTailY)
        {
            // Initialize outputs to NaN in case of error
            correctedHeadX = correctedHeadY = correctedTailX = correctedTailY = double.NaN;

            // Alias input parameters for clarity
            double dogXInput = headX;
            double dogYInput = headY;
            double tailXInput = tailX;
            double tailYInput = tailY;

            // STEP 1: CALCULATE DOG ANGLE
            double dogLCAngle = CalculateAngle(dogXInput, dogYInput);

            // STEP 2: CALCULATE TAIL ANGLE
            double tailLCAngle = CalculateAngle(tailXInput, tailYInput);

            // STEP 3: CALCULATE DISTANCES FROM SPINDLE CENTER
            double dogRotSC2LC = Math.Sqrt((dogXInput * dogXInput) + (dogYInput * dogYInput));
            double tailRotSC2LC = Math.Sqrt((tailXInput * tailXInput) + (tailYInput * tailYInput));

            // STEP 4: DETERMINE IF DOG AND TAIL ARE IN ROTATION ZONE
            int dogLCInZone = (dogLCAngle >= _config.RotationZoneLeftLimit && dogLCAngle <= _config.RotationZoneRightLimit) ? 0 : 1;
            int tailLCInZone = (tailLCAngle >= _config.RotationZoneLeftLimit && tailLCAngle <= _config.RotationZoneRightLimit) ? 0 : 1;

            // STEP 5: CALCULATE DISTANCES FROM ROTATION LIMITS
            double dogFromLeft = dogLCAngle - _config.RotationZoneLeftLimit;
            double dogFromRight = _config.RotationZoneRightLimit - dogLCAngle;
            double tailFromLeft = tailLCAngle - _config.RotationZoneLeftLimit;
            double tailFromRight = _config.RotationZoneRightLimit - tailLCAngle;

            // STEP 6: DETERMINE ROTATION CASE AND APPLY ROTATION LOGIC
            double spindleRotateCalc;
            double newDogAngle = dogLCAngle;
            double newTailAngle = tailLCAngle;

            if (dogLCInZone == 1 && tailLCInZone == 1)
            {
                // CASE A: BOTH COMPONENTS NEED ROTATION
                double angleA, angleB;

                if (dogLCAngle > tailLCAngle)
                {
                    if (dogFromRight > tailFromLeft)
                    {
                        angleA = dogFromRight;
                        angleB = tailFromLeft;
                    }
                    else
                    {
                        angleA = tailFromLeft;
                        angleB = dogFromRight;
                    }
                }
                else
                {
                    if (tailFromRight > dogFromLeft)
                    {
                        angleA = tailFromRight;
                        angleB = dogFromLeft;
                    }
                    else
                    {
                        angleA = dogFromLeft;
                        angleB = tailFromRight;
                    }
                }

                spindleRotateCalc = angleA + angleB;
                newDogAngle = dogLCAngle + spindleRotateCalc;
                newTailAngle = tailLCAngle + spindleRotateCalc;
            }
            else if (dogLCInZone == 0 && tailLCInZone == 1)
            {
                // CASE B: ONLY DOG NEEDS ROTATION
                if (dogFromRight > dogFromLeft)
                {
                    spindleRotateCalc = dogFromRight;
                }
                else
                {
                    spindleRotateCalc = -dogFromLeft;
                }

                newDogAngle = dogLCAngle + spindleRotateCalc;
                newTailAngle = tailLCAngle + spindleRotateCalc;
            }
            else if (dogLCInZone == 1 && tailLCInZone == 0)
            {
                // CASE C: ONLY TAIL NEEDS ROTATION
                if (tailFromRight > tailFromLeft)
                {
                    spindleRotateCalc = tailFromRight;
                }
                else
                {
                    spindleRotateCalc = -tailFromLeft;
                }

                newDogAngle = dogLCAngle + spindleRotateCalc;
                newTailAngle = tailLCAngle + spindleRotateCalc;
            }
            // CASE D: Neither needs rotation - angles remain unchanged

            // STEP 7: CALCULATE FINAL DOG AND TAIL POSITIONS (THE CORRECTED XY)
            double newDogAngleRad = newDogAngle / RadToDeg;
            double newTailAngleRad = newTailAngle / RadToDeg;

            double dogXCalc = dogRotSC2LC * Math.Cos(newDogAngleRad);
            double dogYCalc = dogRotSC2LC * Math.Sin(newDogAngleRad);
            double tailXCalc = tailRotSC2LC * Math.Cos(newTailAngleRad);
            double tailYCalc = tailRotSC2LC * Math.Sin(newTailAngleRad);

            // STEP 8: APPLY OPTIONAL CORRECTION OFFSETS
            if (_config.EnableXYCorrectionOffset)
            {
                dogXCalc += HEAD_X_OFFSET;
                dogYCalc += HEAD_Y_OFFSET;
                tailXCalc += TAIL_X_OFFSET;
                tailYCalc += TAIL_Y_OFFSET;
            }

            // Return the corrected XY positions
            correctedHeadX = dogXCalc;
            correctedHeadY = dogYCalc;
            correctedTailX = tailXCalc;
            correctedTailY = tailYCalc;
        }

        /// <summary>
        /// Calculates the angle in degrees for a given (x, y) coordinate.
        /// 
        /// This method uses the same PLC quadrant logic by manually checking each quadrant
        /// instead of using Math.Atan2(). This ensures exact compatibility with the PLC behavior.
        /// 
        /// QUADRANT BREAKDOWN:
        /// - Q1 (0-90°): X > 0, Y > 0 - upper right
        /// - Q2 (90-180°): X < 0, Y > 0 - upper left
        /// - Q3 (180-270°): X < 0, Y < 0 - lower left
        /// - Q4 (270-360°): X > 0, Y < 0 - lower right
        /// 
        /// SPECIAL CASES (on axes):
        /// - (0, Y+): 90° (positive Y-axis)
        /// - (X-, 0): 180° (negative X-axis)
        /// - (0, Y-): 270° (negative Y-axis)
        /// - (X+, 0): 0° (positive X-axis)
        /// - (0, 0): 0° (undefined, returns 0)
        /// </summary>
        /// <param name="x">X coordinate value</param>
        /// <param name="y">Y coordinate value</param>
        /// <returns>Angle in degrees (0-360 range)</returns>
        private static double CalculateAngle(double x, double y)
        {
            // Q1 - X positive, Y positive (upper right quadrant)
            if (x > 0 && y > 0)
            {
                return RadToDeg * Math.Atan(y / x);
            }

            // Q2 - X negative, Y positive (upper left quadrant)
            // Angle = 180 + arctan(y/x) because arctan returns negative in Q2
            if (x < 0 && y > 0)
            {
                return 180 + RadToDeg * Math.Atan(y / x);
            }

            // Q3 - X negative, Y negative (lower left quadrant)
            // Same formula as Q2 because arctan is symmetric
            if (x < 0 && y < 0)
            {
                return 180 + RadToDeg * Math.Atan(y / x);
            }

            // Q4 - X positive, Y negative (lower right quadrant)
            // Angle = 360 + arctan(y/x) because arctan returns negative in Q4
            if (x > 0 && y < 0)
            {
                return 360 + RadToDeg * Math.Atan(y / x);
            }

            // SPECIAL CASES - on coordinate axes

            // On positive Y-axis
            if (x == 0 && y > 0)
                return 90;

            // On negative X-axis
            if (x < 0 && y == 0)
                return 180;

            // On negative Y-axis
            if (x == 0 && y < 0)
                return 270;

            // On positive X-axis
            if (x > 0 && y == 0)
                return 0;

            // At origin (x == 0 && y == 0) - undefined angle, return 0
            return 0;
        }
    }

