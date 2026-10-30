using System.Drawing.Drawing2D;
using System.Text;
using System.Globalization;

namespace OptimizationXY.Charger.Kinematic;

/// <summary>
/// Engineering window for the kinematic (spindle-rotation) calculator. Not part
/// of the normal scan cycle: it is opened by hand for checking the kinematic
/// maths, and its logic is also used by KinematicPositionSelector when
/// Kinematic.EnableKinematicPositionSelection is switched on (default off).
///
/// This Windows Forms application provides a GUI for calculating actuator positions
/// based on input component locations (dog/head and tail coordinates).
///
/// KEY FEATURES:
/// - Real-time kinematics calculations using the KinematicsCalculator engine
/// - Visual representation of component positions and angles
/// - Rotation zone (danger zone) visualization and configuration
/// - Machine calibration offset configuration
/// - Detailed calculation state tracking for debugging and verification
/// - Multi-threaded calculation to keep UI responsive
///
/// LAYOUT:
/// - Input controls for dog X/Y and tail X/Y coordinates
/// - Real-time visualization of positions and angles
/// - Output display for calculated actuator positions (A1, B1, A2, B2)
/// - Debug panel showing detailed calculation state
/// - Configuration panel for rotation limits and offset settings
///
/// The form maintains a KinematicGraphState object that captures all intermediate
/// and final calculation results for visualization and debugging purposes.
/// </summary>
public partial class KinematicForm : Form
{


    /// <summary>
    /// Internal state class that captures all calculation results and intermediate values.
    ///
    /// This sealed class is used by this form (KinematicForm) to store the complete state
    /// of a kinematics calculation, including:
    /// - Input parameters (original dog/tail positions)
    /// - Intermediate results (after rotation, before offsets)
    /// - Final results (with all corrections applied)
    /// - Angle information (input and output angles in degrees)
    /// - Zone and limit information (which components are in danger zone)
    /// - Calculation metadata (which rotation case was applied)
    ///
    /// This data structure enables:
    /// - Accurate visualization of the kinematics state
    /// - Debug display of all calculation steps
    /// - Verification of calculation correctness
    /// </summary>
    private sealed class KinematicGraphState
    {
        // ========================================================
        // SECTION 1: ORIGINAL PLC INPUTS
        // These are the input parameters provided by the user
        // ========================================================

        /// <summary>X coordinate of the dog (head) component input position</summary>
        public double InputDogX { get; set; }

        /// <summary>Y coordinate of the dog (head) component input position</summary>
        public double InputDogY { get; set; }

        /// <summary>X coordinate of the tail component input position</summary>
        public double InputTailX { get; set; }

        /// <summary>Y coordinate of the tail component input position</summary>
        public double InputTailY { get; set; }


        // ========================================================
        // SECTION 2: ROTATION RESULTS
        // Results immediately after PLC rotation logic
        // BEFORE optional correction offsets are applied
        // ========================================================

        /// <summary>X coordinate of the dog after spindle rotation is applied</summary>
        public double RotatedDogX { get; set; }

        /// <summary>Y coordinate of the dog after spindle rotation is applied</summary>
        public double RotatedDogY { get; set; }

        /// <summary>X coordinate of the tail after spindle rotation is applied</summary>
        public double RotatedTailX { get; set; }

        /// <summary>Y coordinate of the tail after spindle rotation is applied</summary>
        public double RotatedTailY { get; set; }


        // ========================================================
        // SECTION 3: FINAL RESULTS
        // Final PLC XY after optional correction offsets are applied
        // This is what gets sent to the machine
        // ========================================================

        /// <summary>X coordinate of the dog after all corrections (rotation + offsets)</summary>
        public double FinalDogX { get; set; }

        /// <summary>Y coordinate of the dog after all corrections (rotation + offsets)</summary>
        public double FinalDogY { get; set; }

        /// <summary>X coordinate of the tail after all corrections (rotation + offsets)</summary>
        public double FinalTailX { get; set; }

        /// <summary>Y coordinate of the tail after all corrections (rotation + offsets)</summary>
        public double FinalTailY { get; set; }


        // ========================================================
        // SECTION 4: ANGLE INFORMATION
        // All angles are in degrees (0-360 range)
        // ========================================================

        /// <summary>Input angle of the dog component in degrees (0-360)</summary>
        public double InputDogAngleDeg { get; set; }

        /// <summary>Input angle of the tail component in degrees (0-360)</summary>
        public double InputTailAngleDeg { get; set; }

        /// <summary>Resulting angle of the dog component after rotation in degrees (0-360)</summary>
        public double ResultDogAngleDeg { get; set; }

        /// <summary>Resulting angle of the tail component after rotation in degrees (0-360)</summary>
        public double ResultTailAngleDeg { get; set; }


        // ========================================================
        // SECTION 5: ZONE CHECK FLAGS
        // Boolean flags indicating which components are in the danger zone
        // ========================================================

        /// <summary>True if dog angle is within the rotation zone (danger zone), false if safe</summary>
        public bool DogInRotationZone { get; set; }

        /// <summary>True if tail angle is within the rotation zone (danger zone), false if safe</summary>
        public bool TailInRotationZone { get; set; }


        // ========================================================
        // SECTION 6: ROTATION ZONE LIMITS
        // The configured boundaries of the danger zone in degrees
        // ========================================================

        /// <summary>Left boundary of the rotation zone (danger zone) in degrees</summary>
        public double LeftLimitDeg { get; set; }

        /// <summary>Right boundary of the rotation zone (danger zone) in degrees</summary>
        public double RightLimitDeg { get; set; }


        // ========================================================
        // SECTION 7: ROTATION CALCULATION METADATA
        // Information about which rotation case and branch was applied
        // ========================================================

        /// <summary>
        /// Rotation case applied (e.g., "Both Need Rotation", "Dog Only", "Tail Only", "Neither").
        /// This indicates which components required rotation to clear the danger zone.
        /// </summary>
        public string RotationCase { get; set; } = string.Empty;

        /// <summary>
        /// Branch of rotation applied (e.g., "Branch A", "Branch B", etc.).
        /// This indicates which specific algorithm path was taken within the rotation case.
        /// </summary>
        public string RotationBranch { get; set; } = string.Empty;


        // ========================================================
        // SECTION 8: SPINDLE ROTATION VALUE
        // The calculated amount to rotate the spindle
        // ========================================================

        /// <summary>Spindle rotation calculation result (amount to rotate spindle in degrees)</summary>
        public double SpindleRotateCalc { get; set; }


        // ========================================================
        // SECTION 9: CORRECTION OFFSET STATE
        // Whether final XY includes machine correction offsets
        // ========================================================

        /// <summary>True if machine correction offsets are enabled and applied, false otherwise</summary>
        public bool OffsetsEnabled { get; set; }
    }


    // ============================================================
    // PLC ASSUMPTIONS / CONSTANTS
    // These constants and values reflect the PLC configuration
    // and must match the actual machine settings
    // ============================================================

    /// <summary>
    /// Conversion constant from radians to degrees.
    /// The PLC uses this constant (57.2958 ≈ 180/π) in manual ATAN calculations.
    /// This value must match the PLC exactly for correct angle calculations.
    /// </summary>
    protected double RadToDeg = 57.2958;

    /// <summary>
    /// Left boundary of the rotation zone (danger zone) in degrees.
    /// This represents the starting angle of the restricted zone (typically in the 2nd-3rd quadrant).
    /// Default: 135 degrees.
    /// Components with angles between LeftLimit and RightLimit require rotation.
    /// </summary>
    protected double RotationNeededLeftLimit = 135;

    /// <summary>
    /// Right boundary of the rotation zone (danger zone) in degrees.
    /// This represents the ending angle of the restricted zone (typically in the 4th-1st quadrant).
    /// Default: 315 degrees.
    /// Components with angles between LeftLimit and RightLimit require rotation.
    /// </summary>
    protected double RotationNeededRightLimit = 315;

    /// <summary>
    /// Enable/disable machine calibration offset application.
    /// When true, applies predefined offset values to compensate for mechanical tolerances.
    /// When false, calculations return raw values without machine-specific corrections.
    /// References PLC line ~315.
    /// Default: false (offsets disabled).
    /// </summary>
    protected bool Enable_XY_Correction_Offset;

    /// <summary>
    /// PLC correction offset tags for machine calibration - DOG (HEAD) component.
    /// These predefined values compensate for mechanical tolerances and calibration errors.
    /// They are applied to the DOG (HEAD) component when EnableXYCorrectionOffset is true.
    ///
    /// HEAD_X_OFFSET: X-axis offset for head component (default: -1.0893002)
    /// HEAD_Y_OFFSET: Y-axis offset for head component (default: 0.34380007)
    ///
    /// These values must match the PLC exactly for correct compensation.
    /// </summary>
    protected double HEAD_X_OFFSET = -1.0893002;
    protected double HEAD_Y_OFFSET = 0.34380007;

    /// <summary>
    /// PLC correction offset tags for machine calibration - TAIL component.
    /// These predefined values compensate for mechanical tolerances and calibration errors.
    /// They are applied to the TAIL component when EnableXYCorrectionOffset is true.
    ///
    /// TAIL_X_OFFSET: X-axis offset for tail component (default: -0.11765)
    /// TAIL_Y_OFFSET: Y-axis offset for tail component (default: 0.500300)
    ///
    /// These values must match the PLC exactly for correct compensation.
    /// </summary>
    protected double TAIL_X_OFFSET = -0.11765;
    protected double TAIL_Y_OFFSET = 0.500300;

    /// <summary>
    /// Block ID number for tracking calculation blocks.
    /// This is included in the PLC result at array index 0.
    /// Currently unused in the UI as there is no Block ID input.
    /// TODO: Connect a Block ID input control when needed.
    /// </summary>
    protected double Block_ID_Number;


    // ============================================================
    // PLC HOME POSITION CYLINDER PIN-TO-PIN LENGTHS
    //
    // These values represent the base cylinder lengths in home position.
    // CRITICAL: These MUST match the PLC values exactly for correct calculations.
    // B1 and B2 control Y-axis movement (vertical)
    // A1 and A2 control X-axis movement (horizontal)
    //
    // NOTE: These values are assumed to be correctly measured from the hardware.
    // If calculations seem off, verify these values against actual machine measurements.
    //
    // PLC tag references:
    // - B1_Y_Pin2Pin_Length: Home position length for Y-axis, dog component
    // - A1_X_Pin2Pin_Length: Home position length for X-axis, dog component
    // - B2_Y_Pin2Pin_Length: Home position length for Y-axis, tail component
    // - A2_X_Pin2Pin_Length: Home position length for X-axis, tail component
    // ============================================================

    /// <summary>Home position cylinder pin-to-pin length for B1 (Y-axis, dog component)</summary>
    protected double B1_Y_Pin2Pin_Length = 25.35741;

    /// <summary>Home position cylinder pin-to-pin length for A1 (X-axis, dog component)</summary>
    protected double A1_X_Pin2Pin_Length = 23.68214;

    /// <summary>Home position cylinder pin-to-pin length for B2 (Y-axis, tail component)</summary>
    protected double B2_Y_Pin2Pin_Length = 24.493008;

    /// <summary>Home position cylinder pin-to-pin length for A2 (X-axis, tail component)</summary>
    protected double A2_X_Pin2Pin_Length = 24.138481;


    /// <summary>
    /// Current calculation state used for rendering the kinematic visualization.
    /// This object captures all input, intermediate, and final calculation results
    /// so they can be displayed and verified in the UI.
    /// Null when no valid calculation has been performed yet.
    /// </summary>
    private KinematicGraphState? _graphState;



    /// <summary>
    /// Initializes a new instance of the MainForm class.
    ///
    /// This constructor initializes all Windows Forms components (UI controls)
    /// by calling InitializeComponent(), which is auto-generated by the Forms designer.
    ///
    /// After initialization, the form is ready to accept user input and display calculations.
    /// </summary>
    public KinematicForm()
    {
        // Initializes all form components defined in the designer (buttons, text boxes, labels, etc.)
        InitializeComponent();
    }



    /// <summary>
    /// Event handler triggered when the Calculate button is clicked by the user.
    ///
    /// This method initiates the forward kinematics calculation based on the current
    /// input values (Head/Dog X and Y, Tail X and Y). The calculation is performed
    /// asynchronously to keep the UI responsive.
    ///
    /// The calculation updates:
    /// - Calculated actuator positions (A1, B1, A2, B2)
    /// - Component angles and positions
    /// - Visualization of the kinematic state
    /// - Debug information (rotation case, zone flags, etc.)
    /// </summary>
    private void ButtonCalculate_Click(object sender, EventArgs e)
    {

        // Perform the calculation and update all output displays
        CalculateAndUpdateOutputs();

    }

    // ============================================================
    // INPUT READING
    // ============================================================
    /// <summary>
    /// Attempts to read and parse the four input values from the user interface text boxes.
    ///
    /// This method extracts the Head X/Y and Tail X/Y coordinates from the input controls
    /// and validates that they are valid numeric values. If parsing fails for any input,
    /// an error message is displayed and false is returned.
    ///
    /// SUCCESS CONDITION:
    /// All four input values must be successfully parsed as doubles for this method to return true.
    ///
    /// FAILURE HANDLING:
    /// If any input cannot be parsed as a double, the method:
    /// 1. Clears the diagnostics display
    /// 2. Returns false without displaying error details
    /// The UI will remain unchanged if parsing fails, giving the user opportunity to correct input.
    /// </summary>
    /// <param name="dogX">OUTPUT: Parsed X coordinate of the dog (head) component from textBoxHeadX</param>
    /// <param name="dogY">OUTPUT: Parsed Y coordinate of the dog (head) component from textBoxHeadY</param>
    /// <param name="tailX">OUTPUT: Parsed X coordinate of the tail component from textBoxTailX</param>
    /// <param name="tailY">OUTPUT: Parsed Y coordinate of the tail component from textBoxTailY</param>
    /// <returns>True if all inputs were successfully parsed, false otherwise</returns>
    private bool TryReadInputs(
        out double dogX,
        out double dogY,
        out double tailX,
        out double tailY)
    {
        // Initialize outputs to zero (safe default values)
        dogX = 0;
        dogY = 0;
        tailX = 0;
        tailY = 0;

        // Attempt to parse all four input text boxes as double values
        // All must succeed for the method to return true
        bool parsed =
            double.TryParse(this.textBoxHeadX.Text, out dogX) &&
            double.TryParse(this.textBoxHeadY.Text, out dogY) &&
            double.TryParse(this.textBoxTailX.Text, out tailX) &&
            double.TryParse(this.textBoxTailY.Text, out tailY);

        if (!parsed)
        {
            // Clear diagnostics to show that previous results are no longer valid
            return false;
        }

        // Clear diagnostics text box in preparation for new calculation output
        this.richTextBoxDiagnostics.Clear();
        return true;
    }


    // ============================================================
    // INPUT CHANGE HANDLER
    // ============================================================
    /// <summary>
    /// Event handler triggered when any input text box value changes.
    ///
    /// This handler is connected to the TextChanged events of:
    /// - textBoxHeadX
    /// - textBoxHeadY
    /// - textBoxTailX
    /// - textBoxTailY
    ///
    /// Currently, this handler does not perform automatic recalculation on each keystroke.
    /// Users must click the Calculate button to update results.
    ///
    /// FUTURE ENHANCEMENT:
    /// This handler could be expanded to provide real-time calculation and visualization
    /// as the user types, providing immediate feedback.
    /// </summary>
    private void InputTextBox_TextChanged(object sender, EventArgs e)
    {




    }


    // ============================================================
    // CORE CALCULATION AND OUTPUT UPDATE
    // ============================================================
    /// <summary>
    /// Core calculation method that performs forward kinematics and updates all UI outputs.
    ///
    /// This method contains the complete PLC-compatible kinematics calculation logic:
    ///
    /// CALCULATION STEPS:
    /// 1. Read and validate user inputs (Head/Dog X and Y, Tail X and Y)
    /// 2. Calculate angles for both components (quadrant-aware manual ATAN logic)
    /// 3. Calculate distances from spindle center to each component
    /// 4. Determine if components are in the rotation zone (danger zone)
    /// 5. Calculate distances from rotation limits
    /// 6. Apply rotation logic to move components out of danger zone
    /// 7. Recalculate positions after rotation
    /// 8. Apply optional machine calibration correction offsets
    /// 9. Convert final positions to actuator cylinder targets (A1, B1, A2, B2)
    /// 10. Display all results and debug information in the UI
    /// 11. Render the kinematic visualization
    ///
    /// ERROR HANDLING:
    /// If input parsing fails, the method returns early without updating any outputs.
    ///
    /// THREADING:
    /// This method runs on the UI thread. Long calculations could be moved to a background
    /// thread using Task.Run() if performance becomes an issue.
    /// </summary>
    private void CalculateAndUpdateOutputs()
    {
        // ========================================================
        // INPUTS
        //
        // These represent the PLC values:
        //
        // DOGXCalcI  = X coordinate of the dog input
        // DOGYCalcI  = Y coordinate of the dog input
        // TailXCalcI = X coordinate of the tail input
        // TailYCalcI = Y coordinate of the tail input
        //
        // PLC TruLog override around lines 87-92:
        //
        // DOGXCalcI  := trulog_xy[0];
        // DOGYCalcI  := trulog_xy[1];
        // TailXCalcI := trulog_xy[2];
        // TailYCalcI := trulog_xy[3];
        // ========================================================

        // Attempt to read user inputs from the UI
        // If parsing fails, return early without updating outputs
        if (!this.TryReadInputs(
            out double DOGXCalcI,
            out double DOGYCalcI,
            out double TailXCalcI,
            out double TailYCalcI))
        {
            return;
        }


        // ========================================================
        // PLC WORKING VARIABLES
        // These variables mirror the PLC calculation flow exactly
        // ========================================================

        // Angle calculations (in degrees)
        double DogLCAngle = 0;   // Calculated angle from spindle center to dog component
        double TailLCAngle = 0;  // Calculated angle from spindle center to tail component

        // Zone check flags (1 = in zone, 0 = safe)
        int DogLCInZone;  // True if dog is in rotation zone (needs rotation)
        int TailLCInZone; // True if tail is in rotation zone (needs rotation)

        // Distance from spindle center to each component
        double DogRotSC2LC;   // Radial distance from center to dog
        double TailRotSC2LC;  // Radial distance from center to tail

        // Distances from each component to rotation zone boundaries
        double DogFromLeft = 0;   // How far dog angle is from left boundary
        double TailFromLeft = 0;  // How far tail angle is from left boundary
        double DogFromRight = 0;  // How far dog angle is from right boundary
        double TailFromRight = 0; // How far tail angle is from right boundary

        // Intermediate angle calculations used in rotation logic
        double AngleA = 0;  // Used in complex rotation case calculations
        double AngleB = 0;  // Used in complex rotation case calculations

        // Final angles after rotation applied
        double TailEndAngle;  // Final tail angle after rotation
        double DogEndAngle ;   // Final dog angle after rotation

        // New angles with rotation applied (before coordinate conversion)
        double NewDogAngle = 0;  // Dog angle after spindle rotation
        double NewTailAngle = 0; // Tail angle after spindle rotation

        // Final XY coordinates after rotation and optional offsets
        double DOGXCalc = 0;  // Final X position of dog
        double DOGYCalc = 0;  // Final Y position of dog
        double TailXCalc = 0; // Final X position of tail
        double TailYCalc = 0; // Final Y position of tail

        // Spindle rotation amount
        double SpindleRotateCalc = 0; // Amount to rotate spindle (degrees)


        // Rotation calculation metadata
        string rotationCase = string.Empty;   // Which case was applied (Both, Dog Only, Tail Only, Neither)
        string rotationBranch = string.Empty; // Which branch within the case was taken


        // ========================================================
        // PLC LINES ~101-119
        //
        // CHECK FIRST, SECOND, THIRD AND FOURTH QUADRANT
        // ON THE DOG END
        //
        // IMPORTANT:
        // This intentionally does NOT use Math.Atan2()
        // WHICH WOULD BE A GREAT CHANGE TO SIMPLIFY THE CODE!!!!!
        //
        // The PLC manually checks every quadrant, so this C#
        // manually checks every quadrant too.
        // ========================================================

        // Quadrant 1 - X positive, Y positive (upper right)
        if (DOGXCalcI > 0 && DOGYCalcI > 0)
        {
            DogLCAngle = 57.2958 * Math.Atan(DOGYCalcI / DOGXCalcI);
        }

        // Quadrant 2 - X negative, Y positive (upper left)
        if (DOGXCalcI < 0 && DOGYCalcI > 0)
        {
            DogLCAngle = 180 +
                57.2958 *
                Math.Atan(DOGYCalcI / DOGXCalcI);
        }

        // Quadrant 3 - X negative, Y negative (lower left)
        if (DOGXCalcI < 0 && DOGYCalcI < 0)
        {
            DogLCAngle =
                180 +
                57.2958 *
                Math.Atan(DOGYCalcI / DOGXCalcI);
        }

        // Quadrant 4 - X positive, Y negative (lower right)
        if (DOGXCalcI > 0 && DOGYCalcI < 0)
        {
            DogLCAngle =
                360 +
                57.2958 *
                Math.Atan(DOGYCalcI / DOGXCalcI);
        }

        // Check special cases
        //X=0 , Y > 0
        if (DOGXCalcI == 0 && DOGYCalcI > 0)
        {
            DogLCAngle = 90;
        }
        //X<0 , Y=0
        if (DOGXCalcI < 0 && DOGYCalcI == 0)
        {
            DogLCAngle = 180;
        }
        //X=0 , Y < 0
        if (DOGXCalcI == 0 && DOGYCalcI < 0)
        {
            DogLCAngle = 270;
        }
        //X>0 , Y=0
        if (DOGXCalcI > 0 && DOGYCalcI == 0)
        {
            DogLCAngle = 0;
        }


        // ========================================================
        // PLC LINES ~121-138
        //
        // CHECK FIRST, SECOND, THIRD AND FOURTH QUADRANT
        // ON THE TAIL END
        // ========================================================

        // Q1 - X positive, Y positive
        if (TailXCalcI > 0 && TailYCalcI > 0)
        {
            TailLCAngle =
                57.2958 *
                Math.Atan(TailYCalcI / TailXCalcI);
        }

        // Q2 - X negative, Y positive
        if (TailXCalcI < 0 && TailYCalcI > 0)
        {
            TailLCAngle =
                180 +
                57.2958 *
                Math.Atan(TailYCalcI / TailXCalcI);
        }

        // Q3 - X negative, Y negative
        if (TailXCalcI < 0 && TailYCalcI < 0)
        {
            TailLCAngle =
                180 +
                57.2958 *
                Math.Atan(TailYCalcI / TailXCalcI);
        }

        // Q4 - X positive, Y negative
        if (TailXCalcI > 0 && TailYCalcI < 0)
        {
            TailLCAngle =
                360 +
                57.2958 *
                Math.Atan(TailYCalcI / TailXCalcI);
        }

        // Check special cases
        //X=0 , Y > 0
        if (TailXCalcI == 0 && TailYCalcI > 0)
        {
            TailLCAngle = 90;
        }
        //  X<0 , Y=0
        if (TailXCalcI < 0 && TailYCalcI == 0)
        {
            TailLCAngle = 180;
        }
        //X=0 , Y < 0
        if (TailXCalcI == 0 && TailYCalcI < 0)
        {
            TailLCAngle = 270;
        }
        //X>0 , Y=0
        if (TailXCalcI > 0 && TailYCalcI == 0)
        {
            TailLCAngle = 0;
        }


        // ========================================================
        // PLC LINES ~140-146
        //
        // CHECK WHETHER DOG CENTER IS IN ZONE
        // THAT NEEDS ROTATION
        //
        // PLC:
        //
        // If DogLCAngle > Rotation_Needed_Left_Lim
        // And DogLCAngle < Rotation_Needed_Right_Lim
        // ========================================================

        if (DogLCAngle > RotationNeededLeftLimit &&
            DogLCAngle < RotationNeededRightLimit)
        {
            DogLCInZone = 1;
        }
        else
        {
            DogLCInZone = 0;
        }


        // ========================================================
        // PLC LINES ~148-153
        //
        // CHECK WHETHER TAIL CENTER IS IN ZONE
        // THAT NEEDS ROTATION
        // ========================================================

        if (TailLCAngle > RotationNeededLeftLimit &&
            TailLCAngle < RotationNeededRightLimit)
        {
            TailLCInZone = 1;
        }
        else
        {
            TailLCInZone = 0;
        }


        // ========================================================
        // PLC LINES ~155-158
        //
        // DISTANCE FROM SPINDLE CENTER TO LOG CENTER
        // ========================================================

        // Magnitude of the vector from the spindle center to the dog center
        DogRotSC2LC =
            Math.Sqrt(
                DOGXCalcI * DOGXCalcI +
                DOGYCalcI * DOGYCalcI);

        // Magnitude of the vector from the spindle center to the tail center.
        TailRotSC2LC =
            Math.Sqrt(
                TailXCalcI * TailXCalcI +
                TailYCalcI * TailYCalcI);



        // ========================================================
        // PLC LINES ~160-222
        //
        // BOTH DOG AND TAIL NEED ROTATION
        // ========================================================


        if (DogLCInZone == 1 &&
            TailLCInZone == 1)
        {
            DogFromLeft =
                DogLCAngle -
                RotationNeededLeftLimit;

            TailFromLeft =
                TailLCAngle -
                RotationNeededLeftLimit;

            DogFromRight =
                RotationNeededRightLimit -
                DogLCAngle;

            TailFromRight =
                RotationNeededRightLimit -
                TailLCAngle;


            rotationCase =
            "BOTH DOG/HEAD AND TAIL REQUIRE ROTATION";

            // ----------------------------------------------------
            // PLC ~168
            //
            // If DogLCAngle > TailLCAngle
            // AND DogFromRight > TailFromLeft
            // ----------------------------------------------------

            if (DogLCAngle > TailLCAngle &&
                DogFromRight > TailFromLeft)
            {
                AngleB =
                    TailLCAngle -
                    DogFromLeft;

                if (AngleB < 0)
                {
                    AngleB =
                        360 +
                        AngleB;
                }

                NewDogAngle =
                    RotationNeededLeftLimit;

                NewTailAngle =
                    AngleB;

                SpindleRotateCalc =
                    1 +
                    DogFromLeft / 360;

                rotationBranch =
            "Both Case 1: Dog > Tail and DogFromRight > TailFromLeft";
            }


            // ----------------------------------------------------
            // PLC ~177
            //
            // IMPORTANT:
            // This remains a separate IF because the PLC uses a
            // separate IF. It is intentionally not "else if".
            // ----------------------------------------------------

            if (DogLCAngle > TailLCAngle &&
                DogFromRight <= TailFromLeft)
            {
                AngleB =
                    DogLCAngle +
                    TailFromRight;

                if (AngleB > 360)
                {
                    AngleB -=
360;
                }

                NewDogAngle =
                    AngleB;

                NewTailAngle =
                    RotationNeededRightLimit;

                SpindleRotateCalc =
                    1 -
                    TailFromRight / 360;

                rotationBranch =
                    "Both Case 2: Dog > Tail and DogFromRight <= TailFromLeft";
            }


            // ----------------------------------------------------
            // PLC ~186
            // ----------------------------------------------------

            if (DogLCAngle < TailLCAngle &&
                DogFromLeft > TailFromRight)
            {
                AngleB =
                    TailLCAngle +
                    DogFromRight;

                if (AngleB > 360)
                {
                    AngleB -=
360;
                }

                NewDogAngle =
                    RotationNeededRightLimit;

                NewTailAngle =
                    AngleB;

                SpindleRotateCalc =
                    1 -
                    DogFromRight / 360;

                rotationBranch =
            "Both Case 3: Dog < Tail and DogFromLeft > TailFromRight";
            }


            // ----------------------------------------------------
            // PLC ~195
            // ----------------------------------------------------

            if (DogLCAngle < TailLCAngle &&
                DogFromLeft <= TailFromRight)
            {
                AngleB =
                    DogLCAngle -
                    TailFromLeft;

                if (AngleB < 0)
                {
                    AngleB =
                        360 +
                        AngleB;
                }

                NewDogAngle =
                    AngleB;

                NewTailAngle =
                    RotationNeededLeftLimit;

                SpindleRotateCalc =
                    1 +
                    TailFromLeft / 360;

                rotationBranch =
"Both Case 4: Dog < Tail and DogFromLeft <= TailFromRight";
            }


            // ----------------------------------------------------
            // PLC ~204
            //
            // Keep literal 240 exactly.
            // Do NOT derive it from the limits.
            // ----------------------------------------------------

            if (DogLCAngle == TailLCAngle &&
                DogLCAngle > 240)
            {
                NewDogAngle =
                    RotationNeededRightLimit;

                NewTailAngle =
                    RotationNeededRightLimit;

                SpindleRotateCalc =
                    1 -
                    DogFromRight / 360;

                rotationBranch =
"Both Equal-Angle Case: Dog angle > 240";
            }


            // ----------------------------------------------------
            // PLC ~210
            //
            // Keep literal 240 exactly.
            // ----------------------------------------------------

            if (DogLCAngle == TailLCAngle &&
                DogLCAngle <= 240)
            {
                NewDogAngle =
                    RotationNeededLeftLimit;

                NewTailAngle =
                    RotationNeededLeftLimit;

                SpindleRotateCalc =
                    1 +
                    DogFromLeft / 360;

                rotationBranch =
"Both Equal-Angle Case: Dog angle <= 240";
            }


            // ----------------------------------------------------
            // PLC ~216-220
            //
            // Keep exact PLC trig divisor:
            // 57.2957795
            // ----------------------------------------------------

            TailXCalc =
                TailRotSC2LC *
                Math.Cos(
                    NewTailAngle / 57.2957795);

            TailYCalc =
                TailRotSC2LC *
                Math.Sin(
                    NewTailAngle / 57.2957795);

            DOGXCalc =
                DogRotSC2LC *
                Math.Cos(
                    NewDogAngle / 57.2957795);

            DOGYCalc =
                DogRotSC2LC *
                Math.Sin(
                    NewDogAngle / 57.2957795);
        }


        // ========================================================
        // PLC LINES ~224-261
        //
        // DOG NEEDS ROTATION, TAIL DOESN'T
        // ========================================================

        if (DogLCInZone == 1 &&
            TailLCInZone == 0)
        {

            rotationCase =
"DOG/HEAD REQUIRES ROTATION; TAIL DOES NOT";

            // AngleA = angle from left limit to dog angle
            AngleA =
                DogLCAngle -
                RotationNeededLeftLimit;


            // AngleB = angle from right limit to tail angle
            if (TailLCAngle >= RotationNeededRightLimit)
            {
                AngleB =
                    TailLCAngle -
                    RotationNeededRightLimit;
            }
            else
            {
                AngleB =
                    TailLCAngle +
                    360 -
                    RotationNeededRightLimit;
            }


            if (AngleA <= AngleB)
            {

                rotationBranch =
"Dog-Only Case A: rotate Dog to Left Limit";
                TailEndAngle =
                    TailLCAngle -
                    (
                        DogLCAngle -
                        RotationNeededLeftLimit
                    );

                if (TailEndAngle < 0)
                {
                    NewTailAngle =
                        360 +
                        TailEndAngle;
                }
                else
                {
                    NewTailAngle =
                        TailEndAngle;

                    rotationBranch =
                "Dog-Only Case B: rotate Dog to Right Limit";
                }

                NewDogAngle =
                    RotationNeededLeftLimit;


                TailXCalc =
                    TailRotSC2LC *
                    Math.Cos(
                        NewTailAngle / 57.2957795);

                TailYCalc =
                    TailRotSC2LC *
                    Math.Sin(
                        NewTailAngle / 57.2957795);

                DOGXCalc =
                    DogRotSC2LC *
                    Math.Cos(
                        NewDogAngle / 57.2957795);

                DOGYCalc =
                    DogRotSC2LC *
                    Math.Sin(
                        NewDogAngle / 57.2957795);


                SpindleRotateCalc =
                    1 +
                    AngleA / 360;
            }
            else
            {
                TailEndAngle =
                    TailLCAngle +
                    (
                        180 -
                        AngleA
                    );

                if (TailEndAngle >= 360)
                {
                    NewTailAngle =
                        TailEndAngle -
                        360;
                }
                else
                {
                    NewTailAngle =
                        TailEndAngle;
                }

                NewDogAngle =
                    RotationNeededRightLimit;


                TailXCalc =
                    TailRotSC2LC *
                    Math.Cos(
                        NewTailAngle / 57.2957795);

                TailYCalc =
                    TailRotSC2LC *
                    Math.Sin(
                        NewTailAngle / 57.2957795);

                DOGXCalc =
                    DogRotSC2LC *
                    Math.Cos(
                        NewDogAngle / 57.2957795);

                DOGYCalc =
                    DogRotSC2LC *
                    Math.Sin(
                        NewDogAngle / 57.2957795);


                SpindleRotateCalc =
                    1 -
                    (
                        180 -
                        AngleA
                    ) / 360;
            }
        }


        // ========================================================
        // PLC LINES ~263-304
        //
        // DOG DOESN'T NEED ROTATION, TAIL DOES
        // ========================================================

        if (DogLCInZone == 0 &&
            TailLCInZone == 1)
        {

            rotationCase =
            "TAIL REQUIRES ROTATION; DOG/HEAD DOES NOT";


            // AngleA = minimum distance tail needs to rotate
            AngleA =
                TailLCAngle +
                180 -
                RotationNeededRightLimit;

            if (AngleA > 360)
            {
                AngleA -=
360;
            }


            // AngleB = angle from dog to edge of illegal zone
            AngleB =
                DogLCAngle +
                180 -
                RotationNeededLeftLimit;

            if (AngleB > 360)
            {
                AngleB -=
360;
            }


            if (AngleA <= AngleB)
            {

                rotationBranch =
"Tail-Only Case A: rotate Tail to Left Limit";

                DogEndAngle =
                    DogLCAngle -
                    (
                        TailLCAngle -
                        RotationNeededLeftLimit
                    );

                if (DogEndAngle < 0)
                {
                    NewDogAngle =
                        360 +
                        DogEndAngle;
                }
                else
                {

                    rotationBranch =
"Tail-Only Case B: rotate Tail to Right Limit";

                    NewDogAngle =
                        DogEndAngle;
                }

                NewTailAngle =
                    RotationNeededLeftLimit;


                TailXCalc =
                    TailRotSC2LC *
                    Math.Cos(
                        NewTailAngle / 57.2957795);

                TailYCalc =
                    TailRotSC2LC *
                    Math.Sin(
                        NewTailAngle / 57.2957795);

                DOGXCalc =
                    DogRotSC2LC *
                    Math.Cos(
                        NewDogAngle / 57.2957795);

                DOGYCalc =
                    DogRotSC2LC *
                    Math.Sin(
                        NewDogAngle / 57.2957795);


                SpindleRotateCalc =
                    1 +
                    AngleA / 360;
            }
            else
            {
                DogEndAngle =
                    DogLCAngle +
                    (
                        180 -
                        AngleA
                    );

                if (DogEndAngle >= 360)
                {
                    NewDogAngle =
                        DogEndAngle -
                        360;
                }
                else
                {
                    NewDogAngle =
                        DogEndAngle;
                }

                NewTailAngle =
                    RotationNeededRightLimit;


                TailXCalc =
                    TailRotSC2LC *
                    Math.Cos(
                        NewTailAngle / 57.2957795);

                TailYCalc =
                    TailRotSC2LC *
                    Math.Sin(
                        NewTailAngle / 57.2957795);

                DOGXCalc =
                    DogRotSC2LC *
                    Math.Cos(
                        NewDogAngle / 57.2957795);

                DOGYCalc =
                    DogRotSC2LC *
                    Math.Sin(
                        NewDogAngle / 57.2957795);


                SpindleRotateCalc =
                    1 -
                    (
                        180 -
                        AngleA
                    ) / 360;
            }
        }


        // ========================================================
        // PLC LINES ~306-313
        //
        // NEITHER NEED ROTATION
        // ========================================================

        if (DogLCInZone == 0 &&
            TailLCInZone == 0)
        {


            rotationCase =
                "NO ROTATION REQUIRED";

            rotationBranch =
                "PLC direct-copy path";

            DOGXCalc =
                DOGXCalcI;

            DOGYCalc =
                DOGYCalcI;

            TailXCalc =
                TailXCalcI;

            TailYCalc =
                TailYCalcI;

            SpindleRotateCalc =
                1;
        }


        // ============================================================
        // DIAGNOSTIC CAPTURE
        //
        // Save the exact PLC XY result after rotation logic,
        // but before optional machine correction offsets.
        //
        // Does not change PLC math.
        // ============================================================



        double DOGXCalcBeforeOffset =
            DOGXCalc;

        double DOGYCalcBeforeOffset =
            DOGYCalc;

        double TailXCalcBeforeOffset =
            TailXCalc;

        double TailYCalcBeforeOffset =
            TailYCalc;

        // ========================================================
        // PLC LINES ~315-320
        //
        // XY CORRECTION OFFSETS
        //
        // Keep this AFTER rotation, exactly like the PLC.
        // ========================================================

        if (Enable_XY_Correction_Offset)
        {
            DOGXCalc +=
HEAD_X_OFFSET;

            DOGYCalc +=
HEAD_Y_OFFSET;

            TailXCalc +=
TAIL_X_OFFSET;

            TailYCalc +=
TAIL_Y_OFFSET;
        }


        //Determine the result angles only for graph labeling

        double ResultDogAngleDeg;
        double ResultTailAngleDeg;

        if (DogLCInZone == 0 &&
            TailLCInZone == 0)
        {
            ResultDogAngleDeg =
                DogLCAngle;

            ResultTailAngleDeg =
                TailLCAngle;
        }
        else
        {
            ResultDogAngleDeg =
                NewDogAngle;

            ResultTailAngleDeg =
                NewTailAngle;
        }

        // ========================================================
        // PLC LINES ~322-326
        //
        // FINAL ACTUAL XY SOLUTION ARRAY
        // ========================================================

        double[] ActualXYSolution_XY =
            [
                Block_ID_Number,
                    DOGXCalc,
                    DOGYCalc,
                    TailXCalc,
                    TailYCalc,
                ];

        // ============================================================
        // PLC LINES ~330-649
        //
        // POSITIONING CYLINDER KINEMATIC SOLUTION
        //
        // IMPORTANT:
        // This translation intentionally follows the PLC structure,
        // variable naming, constants, iteration counts, formulas,
        // and execution order.
        // ============================================================


        // ============================================================
        // PLC LINES ~330-348
        //
        // **************** DOG SIDE ****************
        //
        // The first kinematic solution solves for the current angle
        // of the positioner based on the initial "home" position of
        // the positioning cylinders.
        // ============================================================

        double deg_to_rad =
                3.1415926536 / 180;

        int end_iteration =
            5;


        // ------------------------------------------------------------
        // Knowns
        // ------------------------------------------------------------

        // The distance between x and y cylinder pins
        double dog_r_1 =
            41.25;

        // PLC:
        // dog_theta_1 := 180*deg_to_rad;
        double dog_theta_1 =
            180 * deg_to_rad;

        // PLC:
        // dog_theta_2 := 270*deg_to_rad;
        double dog_theta_2 =
            270 * deg_to_rad;

        //distance between x and y cylinder bottom pins
        double dog_r_3 =
            41.25;


        //distance from pin to pin for y cylinder at "home"
        // PLC line ~343:
        // dog_r_2 := B1_Y_Pin2Pin_Length;
        double dog_r_2 =
            B1_Y_Pin2Pin_Length;

        //distance from pin to pin for x cylinder at "home"
        // PLC line ~344:
        // dog_r_4 := A1_X_Pin2Pin_Length;
        double dog_r_4 =
            A1_X_Pin2Pin_Length;


        // ------------------------------------------------------------
        // Unknowns line 347, 358 PLC
        // ------------------------------------------------------------

        // estimated initial value of positioner angle
        double dog_theta_3 =
            0 * deg_to_rad;

        // estimated initial value of x cylinder angle
        double dog_theta_4 =
            90 * deg_to_rad;


        // ------------------------------------------------------------
        // Working variables for Dog J1 solution
        // ------------------------------------------------------------

        double dog_sum_x_1 ;
        double dog_sum_y_1 ;

        double dog_J1_11 ;
        double dog_J1_12 ;
        double dog_J1_21 ;
        double dog_J1_22 ;

        double det_J1;

        double dog_J1_inv_11 ;
        double dog_J1_inv_12 ;
        double dog_J1_inv_21 ;
        double dog_J1_inv_22 ;


        // ============================================================
        // PLC LINES ~349-368
        //
        // First Dog Newton iteration
        // ============================================================

        for (int iteration_1 = 1;
             iteration_1 <= end_iteration;
             iteration_1++)
        {
            dog_sum_x_1 =
                dog_r_1 * Math.Cos(dog_theta_1)
                +
                dog_r_2 * Math.Cos(dog_theta_2)
                +
                dog_r_3 * Math.Cos(dog_theta_3)
                +
                dog_r_4 * Math.Cos(dog_theta_4);

            dog_sum_y_1 =
                dog_r_2 * Math.Sin(dog_theta_2)
                +
                dog_r_3 * Math.Sin(dog_theta_3)
                +
                dog_r_4 * Math.Sin(dog_theta_4);


            dog_J1_11 =
                -dog_r_3 *
                Math.Sin(dog_theta_3);

            dog_J1_12 =
                -dog_r_4 *
                Math.Sin(dog_theta_4);

            dog_J1_21 =
                dog_r_3 *
                Math.Cos(dog_theta_3);

            dog_J1_22 =
                dog_r_4 *
                Math.Cos(dog_theta_4);


            det_J1 =
                dog_J1_11 * dog_J1_22
                -
                dog_J1_21 * dog_J1_12;


            dog_J1_inv_11 =
                dog_J1_22 / det_J1;

            dog_J1_inv_12 =
                -dog_J1_12 / det_J1;

            dog_J1_inv_21 =
                -dog_J1_21 / det_J1;

            dog_J1_inv_22 =
                dog_J1_11 / det_J1;


            dog_theta_3 -=
(
                    dog_J1_inv_11 * dog_sum_x_1
                    +
                    dog_J1_inv_12 * dog_sum_y_1
                );

            dog_theta_4 -=
(
                    dog_J1_inv_21 * dog_sum_x_1
                    +
                    dog_J1_inv_22 * dog_sum_y_1
                );
        }


        // ============================================================
        // PLC LINES ~372-407
        //
        // The second kinematic solution solves for the global x and y
        // positions at the "home" position based on the angle of the
        // positioner and the y cylinder position.
        // ============================================================

        // PLC exact value:
        double dog_alpha =
            50.5805 * deg_to_rad;


        // ------------------------------------------------------------
        // Knowns
        //
        // dog_r_2 is known from above
        // dog_theta_2 is known from above
        // ------------------------------------------------------------

        // PLC:
        // dog_r_5 := 23.62;
        double dog_r_5 =
            23.62;

        // PLC:
        // dog_theta_5 := dog_theta_3 - dog_alpha;
        double dog_theta_5 =
            dog_theta_3 -
            dog_alpha;

        // PLC:
        // dog_theta_6 := 0*deg_to_rad;
        double dog_theta_6 =
            0 * deg_to_rad;

        // PLC:
        // dog_theta_7 := 270*deg_to_rad;
        double dog_theta_7 =
            270 * deg_to_rad;


        // ------------------------------------------------------------
        // Unknowns
        // ------------------------------------------------------------

        // PLC:
        // dog_r_6 := 15;
        double dog_r_6 =
            15;

        // PLC:
        // dog_r_7 := 42.75;
        double dog_r_7 =
            42.75;


        // ------------------------------------------------------------
        // Dog J2 working variables
        // ------------------------------------------------------------

        double dog_sum_x_2 ;
        double dog_sum_y_2 ;

        double dog_J2_11 ;
        double dog_J2_12 ;
        double dog_J2_21 ;
        double dog_J2_22 ;

        double det_J2 ;

        double dog_J2_inv_11 ;
        double dog_J2_inv_12 ;
        double dog_J2_inv_21 ;
        double dog_J2_inv_22 ;


        // ============================================================
        // PLC second Dog iteration
        // ============================================================

        for (int iteration_2 = 1;
             iteration_2 <= end_iteration;
             iteration_2++)
        {
            dog_sum_x_2 =
                dog_r_5 * Math.Cos(dog_theta_5)
                -
                dog_r_6 * Math.Cos(dog_theta_6);

            dog_sum_y_2 =
                dog_r_2 * Math.Sin(dog_theta_2)
                +
                dog_r_5 * Math.Sin(dog_theta_5)
                -
                dog_r_7 * Math.Sin(dog_theta_7);


            dog_J2_11 =
                -Math.Cos(dog_theta_6);

            dog_J2_12 =
                0;

            dog_J2_21 =
                0;

            dog_J2_22 =
                -Math.Sin(dog_theta_7);


            det_J2 =
                dog_J2_11 * dog_J2_22
                -
                dog_J2_21 * dog_J2_12;


            dog_J2_inv_11 =
                dog_J2_22 / det_J2;

            dog_J2_inv_12 =
                -dog_J2_12 / det_J2;

            dog_J2_inv_21 =
                -dog_J2_21 / det_J2;

            dog_J2_inv_22 =
                dog_J2_11 / det_J2;


            dog_r_6 -=
(
                    dog_J2_inv_11 * dog_sum_x_2
                    +
                    dog_J2_inv_12 * dog_sum_y_2
                );

            dog_r_7 -=
(
                    dog_J2_inv_21 * dog_sum_x_2
                    +
                    dog_J2_inv_22 * dog_sum_y_2
                );
        }


        // ============================================================
        // PLC LINES ~411-445
        //
        // The third kinematic solution solves for the new positioner
        // angle and y cylinder position based on the previous
        // calculation and the x and y target positions.
        // ============================================================

        // ------------------------------------------------------------
        // Knowns
        //
        // dog_theta_2 is known from above
        // dog_r_5 is known from above
        // dog_theta_6 is known from above
        // dog_theta_7 is known from above
        // ------------------------------------------------------------

        // PLC:
        // dog_r_6_new := dog_r_6 + DOGXCalc;
        double dog_r_6_new =
            dog_r_6 +
            DOGXCalc;

        // PLC:
        // dog_r_7_new := dog_r_7 + DOGYCalc;
        double dog_r_7_new =
            dog_r_7 +
            DOGYCalc;


        // ------------------------------------------------------------
        // Unknowns
        // ------------------------------------------------------------

        double dog_theta_5_new =
            dog_theta_5;

        double dog_r_2_new =
            dog_r_2;


        // ------------------------------------------------------------
        // Dog J3 working variables
        // ------------------------------------------------------------

        double dog_sum_x_3 ;
        double dog_sum_y_3 ;

        double dog_J3_11 ;
        double dog_J3_12 ;
        double dog_J3_21 ;
        double dog_J3_22 ;

        double det_J3 ;

        double dog_J3_inv_11 ;
        double dog_J3_inv_12 ;
        double dog_J3_inv_21 ;
        double dog_J3_inv_22 ;


        // ============================================================
        // PLC third Dog iteration
        // ============================================================

        for (int iteration_3 = 1;
             iteration_3 <= end_iteration;
             iteration_3++)
        {
            dog_sum_x_3 =
                dog_r_5 *
                Math.Cos(dog_theta_5_new)
                -
                dog_r_6_new;

            dog_sum_y_3 =
                -dog_r_2_new
                +
                dog_r_5 *
                Math.Sin(dog_theta_5_new)
                +
                dog_r_7_new;


            dog_J3_11 =
                -dog_r_5 *
                Math.Sin(dog_theta_5_new);

            dog_J3_12 =
                0;

            dog_J3_21 =
                dog_r_5 *
                Math.Cos(dog_theta_5_new);

            dog_J3_22 =
                -1;


            det_J3 =
                dog_J3_11 * dog_J3_22
                -
                dog_J3_21 * dog_J3_12;


            dog_J3_inv_11 =
                dog_J3_22 / det_J3;

            dog_J3_inv_12 =
                -dog_J3_12 / det_J3;

            dog_J3_inv_21 =
                -dog_J3_21 / det_J3;

            dog_J3_inv_22 =
                dog_J3_11 / det_J3;


            dog_theta_5_new -=
(
                    dog_J3_inv_11 * dog_sum_x_3
                    +
                    dog_J3_inv_12 * dog_sum_y_3
                );

            dog_r_2_new -=
(
                    dog_J3_inv_21 * dog_sum_x_3
                    +
                    dog_J3_inv_22 * dog_sum_y_3
                );
        }


        // ============================================================
        // PLC LINES ~449-482
        //
        // The fourth kinematic solution solves for the new x cylinder
        // position and angle based on the positioner angle and the
        // y cylinder position.
        // ============================================================

        // ------------------------------------------------------------
        // Knowns
        //
        // dog_r_2_new from above
        // dog_theta_2 from above
        // dog_r_3 from above
        // dog_r_1 from above
        // dog_theta_1 from above
        // ------------------------------------------------------------

        // PLC:
        // dog_theta_3_new := dog_theta_5_new + dog_alpha;
        double dog_theta_3_new =
            dog_theta_5_new
            +
            dog_alpha;


        // ------------------------------------------------------------
        // Unknowns
        // ------------------------------------------------------------

        double dog_r_4_new =
            dog_r_4;

        double dog_theta_4_new =
            90 * deg_to_rad;


        // ------------------------------------------------------------
        // Dog J4 working variables
        // ------------------------------------------------------------

        double dog_sum_x_4 ;
        double dog_sum_y_4 ;

        double dog_J4_11 ;
        double dog_J4_12 ;
        double dog_J4_21 ;
        double dog_J4_22 ;

        double det_J4 ;

        double dog_J4_inv_11 ;
        double dog_J4_inv_12 ;
        double dog_J4_inv_21 ;
        double dog_J4_inv_22 ;


        // ============================================================
        // PLC fourth Dog iteration
        // ============================================================

        for (int iteration_4 = 1;
             iteration_4 <= end_iteration;
             iteration_4++)
        {
            dog_sum_x_4 =
                -dog_r_1
                +
                dog_r_3 *
                Math.Cos(dog_theta_3_new)
                +
                dog_r_4_new *
                Math.Cos(dog_theta_4_new);

            dog_sum_y_4 =
                -dog_r_2_new
                +
                dog_r_3 *
                Math.Sin(dog_theta_3_new)
                +
                dog_r_4_new *
                Math.Sin(dog_theta_4_new);


            dog_J4_11 =
                Math.Cos(dog_theta_4_new);

            dog_J4_12 =
                -dog_r_4_new *
                Math.Sin(dog_theta_4_new);

            dog_J4_21 =
                Math.Sin(dog_theta_4_new);

            dog_J4_22 =
                dog_r_4_new *
                Math.Cos(dog_theta_4_new);


            det_J4 =
                dog_J4_11 * dog_J4_22
                -
                dog_J4_21 * dog_J4_12;


            dog_J4_inv_11 =
                dog_J4_22 / det_J4;

            dog_J4_inv_12 =
                -dog_J4_12 / det_J4;

            dog_J4_inv_21 =
                -dog_J4_21 / det_J4;

            dog_J4_inv_22 =
                dog_J4_11 / det_J4;


            dog_r_4_new -=
(
                    dog_J4_inv_11 * dog_sum_x_4
                    +
                    dog_J4_inv_12 * dog_sum_y_4
                );

            dog_theta_4_new -=
(
                    dog_J4_inv_21 * dog_sum_x_4
                    +
                    dog_J4_inv_22 * dog_sum_y_4
                );
        }


        // ============================================================
        // PLC LINES ~485-500
        //
        // **************** TAIL SIDE ****************
        //
        // The first kinematic solution solves for the current angle
        // of the positioner based on the initial "home" position of
        // the positioning cylinders.
        // ============================================================

        // ------------------------------------------------------------
        // Knowns
        // ------------------------------------------------------------

        double tail_r_1 =
            41.25;

        double tail_theta_1 =
            180 * deg_to_rad;

        double tail_theta_2 =
            270 * deg_to_rad;

        double tail_r_3 =
            41.25;


        // PLC:
        double tail_r_2 =
            B2_Y_Pin2Pin_Length;

        // PLC:
        double tail_r_4 =
            A2_X_Pin2Pin_Length;


        // ------------------------------------------------------------
        // Unknowns
        // ------------------------------------------------------------

        double tail_theta_3 =
            0 * deg_to_rad;

        double tail_theta_4 =
            90 * deg_to_rad;


        // ------------------------------------------------------------
        // Tail J1 working variables
        // ------------------------------------------------------------

        double tail_sum_x_1 ;
        double tail_sum_y_1 ;

        double tail_J1_11 ;
        double tail_J1_12 ;
        double tail_J1_21 ;
        double tail_J1_22 ;

        double tail_det_J1 ;

        double tail_J1_inv_11 ;
        double tail_J1_inv_12 ;
        double tail_J1_inv_21 ;
        double tail_J1_inv_22 ;


        // ============================================================
        // PLC LINES ~502-520
        //
        // First Tail Newton iteration
        // ============================================================

        for (int iteration_5 = 1;
             iteration_5 <= end_iteration;
             iteration_5++)
        {
            // IMPORTANT:
            // The printed PLC uses dog_theta_1 here.
            // I am preserving it exactly and NOT changing it to
            // tail_theta_1.
            tail_sum_x_1 =
                tail_r_1 * Math.Cos(dog_theta_1)
                +
                tail_r_2 * Math.Cos(tail_theta_2)
                +
                tail_r_3 * Math.Cos(tail_theta_3)
                +
                tail_r_4 * Math.Cos(tail_theta_4);

            // IMPORTANT:
            // The printed PLC uses dog_theta_2 here.
            // Preserved exactly.
            tail_sum_y_1 =
                tail_r_2 * Math.Sin(dog_theta_2)
                +
                tail_r_3 * Math.Sin(tail_theta_3)
                +
                tail_r_4 * Math.Sin(tail_theta_4);


            tail_J1_11 =
                -tail_r_3 *
                Math.Sin(tail_theta_3);

            tail_J1_12 =
                -tail_r_4 *
                Math.Sin(tail_theta_4);

            tail_J1_21 =
                tail_r_3 *
                Math.Cos(tail_theta_3);

            tail_J1_22 =
                tail_r_4 *
                Math.Cos(tail_theta_4);


            tail_det_J1 =
                tail_J1_11 * tail_J1_22
                -
                tail_J1_21 * tail_J1_12;


            tail_J1_inv_11 =
                tail_J1_22 / tail_det_J1;

            tail_J1_inv_12 =
                -tail_J1_12 / tail_det_J1;

            tail_J1_inv_21 =
                -tail_J1_21 / tail_det_J1;

            tail_J1_inv_22 =
                tail_J1_11 / tail_det_J1;


            tail_theta_3 -=
(
                    tail_J1_inv_11 * tail_sum_x_1
                    +
                    tail_J1_inv_12 * tail_sum_y_1
                );

            tail_theta_4 -=
(
                    tail_J1_inv_21 * tail_sum_x_1
                    +
                    tail_J1_inv_22 * tail_sum_y_1
                );
        }


        // ============================================================
        // PLC LINES ~523-559
        //
        // The second Tail kinematic solution solves for global x and y
        // positions at the "home" position based on the angle of the
        // positioner and the y cylinder position.
        // ============================================================

        // PLC exact printed value:
        double tail_alpha =
            50.5805 * deg_to_rad;


        // ------------------------------------------------------------
        // Knowns
        //
        // tail_r_2 is known from above
        // tail_theta_2 is known from above
        // ------------------------------------------------------------

        // PLC:
        // tail_r_5 := 23.62;
        double tail_r_5 =
            23.62;

        // PLC:
        // tail_theta_5 := tail_theta_3 - tail_alpha;
        double tail_theta_5 =
            tail_theta_3
            -
            tail_alpha;

        // PLC:
        double tail_theta_6 =
            0 * deg_to_rad;

        // PLC:
        double tail_theta_7 =
            270 * deg_to_rad;


        // ------------------------------------------------------------
        // Unknowns
        // ------------------------------------------------------------

        double tail_r_6 =
            15;

        double tail_r_7 =
            42.75;


        // ------------------------------------------------------------
        // Tail J2 working variables
        // ------------------------------------------------------------

        double tail_sum_x_2 ;
        double tail_sum_y_2 ;

        double tail_J2_11 ;
        double tail_J2_12 ;
        double tail_J2_21 ;
        double tail_J2_22 ;

        double tail_det_J2 ;

        double tail_J2_inv_11 ;
        double tail_J2_inv_12 ;
        double tail_J2_inv_21 ;
        double tail_J2_inv_22 ;


        // ============================================================
        // PLC second Tail iteration
        // ============================================================

        for (int iteration_6 = 1;
             iteration_6 <= end_iteration;
             iteration_6++)
        {
            tail_sum_x_2 =
                tail_r_5 *
                Math.Cos(tail_theta_5)
                -
                tail_r_6 *
                Math.Cos(tail_theta_6);

            tail_sum_y_2 =
                tail_r_2 *
                Math.Sin(tail_theta_2)
                +
                tail_r_5 *
                Math.Sin(tail_theta_5)
                -
                tail_r_7 *
                Math.Sin(tail_theta_7);


            tail_J2_11 =
                -Math.Cos(tail_theta_6);

            tail_J2_12 =
                0;

            tail_J2_21 =
                0;

            tail_J2_22 =
                -Math.Sin(tail_theta_7);


            tail_det_J2 =
                tail_J2_11 * tail_J2_22
                -
                tail_J2_21 * tail_J2_12;


            tail_J2_inv_11 =
                tail_J2_22 / tail_det_J2;

            tail_J2_inv_12 =
                -tail_J2_12 / tail_det_J2;

            tail_J2_inv_21 =
                -tail_J2_21 / tail_det_J2;

            tail_J2_inv_22 =
                tail_J2_11 / tail_det_J2;


            tail_r_6 -=
(
                    tail_J2_inv_11 * tail_sum_x_2
                    +
                    tail_J2_inv_12 * tail_sum_y_2
                );

            tail_r_7 -=
(
                    tail_J2_inv_21 * tail_sum_x_2
                    +
                    tail_J2_inv_22 * tail_sum_y_2
                );
        }


        // ============================================================
        // PLC LINES ~563-597
        //
        // The third Tail kinematic solution solves for the new
        // positioner angle and y cylinder position based on the
        // previous calculation and x and y target positions.
        // ============================================================

        // ------------------------------------------------------------
        // Knowns
        //
        // tail_theta_2 is known from above
        // tail_r_5 is known from above
        // tail_theta_6 is known from above
        // tail_theta_7 is known from above
        // ------------------------------------------------------------

        // PLC:
        double tail_r_6_new =
            tail_r_6 +
            TailXCalc;

        // PLC:
        double tail_r_7_new =
            tail_r_7 +
            TailYCalc;


        // ------------------------------------------------------------
        // Unknowns
        // ------------------------------------------------------------

        double tail_theta_5_new =
            tail_theta_5;

        double tail_r_2_new =
            tail_r_2;


        // ------------------------------------------------------------
        // Tail J3 working variables
        // ------------------------------------------------------------

        double tail_sum_x_3;
        double tail_sum_y_3;

        double tail_J3_11 ;
        double tail_J3_12 ;
        double tail_J3_21 ;
        double tail_J3_22 ;

        double tail_det_J3 ;

        double tail_J3_inv_11 ;
        double tail_J3_inv_12 ;
        double tail_J3_inv_21 ;
        double tail_J3_inv_22 ;


        // ============================================================
        // PLC third Tail iteration
        // ============================================================

        for (int iteration_7 = 1;
             iteration_7 <= end_iteration;
             iteration_7++)
        {
            tail_sum_x_3 =
                tail_r_5 *
                Math.Cos(tail_theta_5_new)
                -
                tail_r_6_new;

            tail_sum_y_3 =
                -tail_r_2_new
                +
                tail_r_5 *
                Math.Sin(tail_theta_5_new)
                +
                tail_r_7_new;


            tail_J3_11 =
                -tail_r_5 *
                Math.Sin(tail_theta_5_new);

            tail_J3_12 =
                0;

            tail_J3_21 =
                tail_r_5 *
                Math.Cos(tail_theta_5_new);

            tail_J3_22 =
                -1;


            tail_det_J3 =
                tail_J3_11 * tail_J3_22
                -
                tail_J3_21 * tail_J3_12;


            tail_J3_inv_11 =
                tail_J3_22 / tail_det_J3;

            tail_J3_inv_12 =
                -tail_J3_12 / tail_det_J3;

            tail_J3_inv_21 =
                -tail_J3_21 / tail_det_J3;

            tail_J3_inv_22 =
                tail_J3_11 / tail_det_J3;


            tail_theta_5_new -=
(
                    tail_J3_inv_11 * tail_sum_x_3
                    +
                    tail_J3_inv_12 * tail_sum_y_3
                );

            tail_r_2_new -=
(
                    tail_J3_inv_21 * tail_sum_x_3
                    +
                    tail_J3_inv_22 * tail_sum_y_3
                );
        }


        // ============================================================
        // PLC LINES ~601-634
        //
        // The fourth Tail kinematic solution solves for the new
        // x cylinder position and angle based on the positioner angle
        // and the y cylinder position.
        // ============================================================

        // ------------------------------------------------------------
        // Knowns
        //
        // tail_r_2_new from above
        // tail_theta_2 from above
        // tail_r_3 from above
        // tail_r_1 from above
        // tail_theta_1 from above
        // ------------------------------------------------------------

        // PLC:
        double tail_theta_3_new =
            tail_theta_5_new
            +
            tail_alpha;


        // ------------------------------------------------------------
        // Unknowns
        // ------------------------------------------------------------

        double tail_r_4_new =
            tail_r_4;

        double tail_theta_4_new =
            90 * deg_to_rad;


        // ------------------------------------------------------------
        // Tail J4 working variables
        // ------------------------------------------------------------

        double tail_sum_x_4;
        double tail_sum_y_4;

        double tail_J4_11 ;
        double tail_J4_12 ;
        double tail_J4_21 ;
        double tail_J4_22 ;

        double tail_det_J4 ;

        double tail_J4_inv_11 ;
        double tail_J4_inv_12 ;
        double tail_J4_inv_21 ;
        double tail_J4_inv_22 ;


        // ============================================================
        // PLC fourth Tail iteration
        // ============================================================

        for (int iteration_8 = 1;
             iteration_8 <= end_iteration;
             iteration_8++)
        {
            tail_sum_x_4 =
                -tail_r_1
                +
                tail_r_3 *
                Math.Cos(tail_theta_3_new)
                +
                tail_r_4_new *
                Math.Cos(tail_theta_4_new);

            tail_sum_y_4 =
                -tail_r_2_new
                +
                tail_r_3 *
                Math.Sin(tail_theta_3_new)
                +
                tail_r_4_new *
                Math.Sin(tail_theta_4_new);


            tail_J4_11 =
                Math.Cos(tail_theta_4_new);

            tail_J4_12 =
                -tail_r_4_new *
                Math.Sin(tail_theta_4_new);

            tail_J4_21 =
                Math.Sin(tail_theta_4_new);

            tail_J4_22 =
                tail_r_4_new *
                Math.Cos(tail_theta_4_new);


            tail_det_J4 =
                tail_J4_11 * tail_J4_22
                -
                tail_J4_21 * tail_J4_12;


            tail_J4_inv_11 =
                tail_J4_22 / tail_det_J4;

            tail_J4_inv_12 =
                -tail_J4_12 / tail_det_J4;

            tail_J4_inv_21 =
                -tail_J4_21 / tail_det_J4;

            tail_J4_inv_22 =
                tail_J4_11 / tail_det_J4;


            tail_r_4_new -=
(
                    tail_J4_inv_11 * tail_sum_x_4
                    +
                    tail_J4_inv_12 * tail_sum_y_4
                );

            tail_theta_4_new -=
(
                    tail_J4_inv_21 * tail_sum_x_4
                    +
                    tail_J4_inv_22 * tail_sum_y_4
                );
        }


        // ============================================================
        // PLC LINES ~638-642
        //
        // FINAL POSITIONING CYLINDER TARGETS
        // ============================================================

        // PLC:
        // New_A1_X_Target := -dog_r_4_new + dog_r_4;
        double New_A1_X_Target =
            -dog_r_4_new
            +
            dog_r_4;


        // PLC:
        // New_B1_Y_Target := -dog_r_2_new + dog_r_2;
        double New_B1_Y_Target =
            -dog_r_2_new
            +
            dog_r_2;


        // PLC:
        // New_A2_X_Target := -tail_r_4_new + tail_r_4;
        double New_A2_X_Target =
            -tail_r_4_new
            +
            tail_r_4;


        // PLC:
        // New_B2_Y_Target := -tail_r_2_new + tail_r_2;
        double New_B2_Y_Target =
            -tail_r_2_new
            +
            tail_r_2;




        // ============================================================
        // Update the numeric up-down controls with the new target values
        // ============================================================

        this.textBoxA1.Text =
Convert.ToString(
    New_A1_X_Target);

        this.textBoxB1.Text =
            Convert.ToString(
                New_B1_Y_Target);

        this.textBoxA2.Text =
            Convert.ToString(
                New_A2_X_Target);

        this.textBoxB2.Text =
            Convert.ToString(
                New_B2_Y_Target);



        // ============================================================
        // Build the diagnostics report and display it in the rich text box
        // ============================================================
        richTextBoxDiagnostics.Text =
BuildDiagnosticsReport(
    DOGXCalcI,
    DOGYCalcI,
    TailXCalcI,
    TailYCalcI,

    DogLCAngle,
    TailLCAngle,

    DogLCInZone,
    TailLCInZone,

    DogRotSC2LC,
    TailRotSC2LC,

    rotationCase,
    rotationBranch,

    DogFromLeft,
    TailFromLeft,
    DogFromRight,
    TailFromRight,

    AngleA,
    AngleB,

    NewDogAngle,
    NewTailAngle,



    DOGXCalcBeforeOffset,
    DOGYCalcBeforeOffset,
    TailXCalcBeforeOffset,
    TailYCalcBeforeOffset,

    DOGXCalc,
    DOGYCalc,
    TailXCalc,
    TailYCalc,

    SpindleRotateCalc,

    ActualXYSolution_XY,

    New_A1_X_Target,
    New_B1_Y_Target,
    New_A2_X_Target,
    New_B2_Y_Target,

    // Dog Newton variables
    dog_r_1,
    dog_r_2,
    dog_r_3,
    dog_r_4,
    dog_r_5,
    dog_r_6,
    dog_r_7,

    dog_theta_1,
    dog_theta_2,
    dog_theta_3,
    dog_theta_4,
    dog_theta_5,
    dog_theta_6,
    dog_theta_7,

    dog_r_6_new,
    dog_r_7_new,
    dog_theta_5_new,
    dog_r_2_new,
    dog_theta_3_new,
    dog_r_4_new,
    dog_theta_4_new,

    // Tail Newton variables
    tail_r_1,
    tail_r_2,
    tail_r_3,
    tail_r_4,
    tail_r_5,
    tail_r_6,
    tail_r_7,

    tail_theta_1,
    tail_theta_2,
    tail_theta_3,
    tail_theta_4,
    tail_theta_5,
    tail_theta_6,
    tail_theta_7,

    tail_r_6_new,
    tail_r_7_new,
    tail_theta_5_new,
    tail_r_2_new,
    tail_theta_3_new,
    tail_r_4_new,
    tail_theta_4_new);



        // ============================================================
        // PLC LINES ~645-649
        //
        // THESE LINES ARE COMMENTED OUT IN THE PLC.

        // printed PLC.
        // ============================================================

        // if (Enable_Pos_Rotation_Offset && !Rotate_Rescan)
        // {
        //     double B1_A1_Diff =
        //         New_B1_Y_Target - New_A1_X_Target;
        //
        //     double Pos_Rot_Angle =
        //         Math.Asin(B1_A1_Diff / 23) / 360;
        //
        //     SpindleRotateCalc =
        //         SpindleRotateCalc - Pos_Rot_Angle;
        // }



        // ============================================================
        //Create the graph state at the end of Calculate
        // ============================================================

        _graphState = new KinematicGraphState
        {
            //orginal outputs
            InputDogX = DOGXCalcI,
            InputDogY = DOGYCalcI,

            InputTailX = TailXCalcI,
            InputTailY = TailYCalcI,

            //After PLC rotaion Logic
            //Before machine offset logic


            RotatedDogX = DOGXCalcBeforeOffset,
            RotatedDogY = DOGYCalcBeforeOffset,

            RotatedTailX = TailXCalcBeforeOffset,
            RotatedTailY = TailYCalcBeforeOffset,

            //Final PLC XY
            FinalDogX = DOGXCalc,
            FinalDogY = DOGYCalc,

            FinalTailX = TailXCalc,
            FinalTailY = TailYCalc,

            //Angles
            InputDogAngleDeg = DogLCAngle,
            InputTailAngleDeg = TailLCAngle,

            ResultDogAngleDeg = ResultDogAngleDeg,
            ResultTailAngleDeg = ResultTailAngleDeg,



            //Flags
            DogInRotationZone =
        DogLCInZone == 1,

            TailInRotationZone =
        TailLCInZone == 1,

            // PLC limits
            LeftLimitDeg =
        RotationNeededLeftLimit,

            RightLimitDeg =
        RotationNeededRightLimit,


            // Branch diagnostics
            RotationCase =
        rotationCase,

            RotationBranch =
        rotationBranch,


            // Spindle
            SpindleRotateCalc =
        SpindleRotateCalc,


            // Offset state
            OffsetsEnabled =
        Enable_XY_Correction_Offset


        };

        // Draw the graph
        RenderKinematicGraph();
    }



    //========================================================
    /// <summary>
    /// RENDER KINEMATIC GRAPH
    ///
    /// Renders the visual representation of the current kinematics calculation state.
    /// This method creates a 2D graph showing:
    /// - The spindle center point
    /// - The rotation zone (danger zone) boundaries as radial lines
    /// - The safe zone (where no rotation is needed)
    /// - Current dog (head) and tail component positions
    /// - Their angles from the spindle center
    /// - Rotation zone indicators and labels
    ///
    /// The graph provides visual feedback to verify correct kinematics behavior.
    /// If no valid calculation state exists, the method returns early without rendering.
    /// </summary>
    // ========================================================
    /// <summary>Draws the kinematic graph picture (circles, zones, head/tail points) from the last calculation.</summary>
    private void RenderKinematicGraph()
    {

        if (_graphState == null)
            return;

        //------------------------------------------------------------
        // Create a bitmap to draw the graph on
        //------------------------------------------------------------
        int width = Math.Max(
            400,
            pictureBoxKinematicGraph.Width);

        int height = Math.Max(
            400,
            pictureBoxKinematicGraph.Height);


        Bitmap bitmap = new(width, height);

        Graphics g = Graphics.FromImage(bitmap);


        g.SmoothingMode = SmoothingMode.AntiAlias;

        g.Clear(Color.White);


        //------------------------------------------------------------
        //Graph Layout
        //------------------------------------------------------------

        const int marginLeft = 65;
        const int marginRight = 35;
        const int marginTop = 70;
        const int marginBottom = 65;


        RectangleF plotRect =
            new(
                marginLeft,
                marginTop,
                width -
                marginLeft -
                marginRight,
                height -
                marginTop -
                marginBottom);

        float centerX =
            plotRect.Left +
            plotRect.Width / 2f;

        float centerY =
            plotRect.Top +
            plotRect.Height / 2f;

        //------------------------------------------------------------
        //Auto Scale
        // use all orgal, rotated,and final points to determine the scale of the graph
        //------------------------------------------------------------

        double maxRadius = 0;


        // Find the maximum radius among all points to determine the scale
        maxRadius =
            Math.Max(
                maxRadius,
                CalcGraphRadius(
                    _graphState.InputDogX,
                    _graphState.InputDogY));

        maxRadius =
            Math.Max(
                maxRadius,
                CalcGraphRadius(
                    _graphState.InputTailX,
                    _graphState.InputTailY));


        maxRadius =
            Math.Max(
                maxRadius,
                CalcGraphRadius(
                    _graphState.RotatedDogX,
                    _graphState.RotatedDogY));

        maxRadius =
            Math.Max(
                maxRadius,
                CalcGraphRadius(
                    _graphState.RotatedTailX,
                    _graphState.RotatedTailY));

        maxRadius =
            Math.Max(
                maxRadius,
                CalcGraphRadius(
                    _graphState.FinalDogX,
                    _graphState.FinalDogY));

        maxRadius =
            Math.Max(
                maxRadius,
                CalcGraphRadius(
                    _graphState.FinalTailX,
                    _graphState.FinalTailY));

        if (maxRadius < 0.000001)
        {
            maxRadius = 1;
        }



        //add 20% margin to the max radius

        maxRadius *= 1.20;


        // Calculate the plot radius in pixels based on the smaller dimension of the plot rectangle
        float plotRadiusPixels =
            Math.Min(
                plotRect.Width,
                plotRect.Height) * 0.45f;


        // Calculate the pixels per unit based on the maximum radius and plot radius
        double pixelsPerUnit =
        plotRadiusPixels /
        maxRadius;


        //-----------------------------------------------------
        //Title
        //------------------------------------------------

        using Font titleFont = new("Arial", 14, FontStyle.Bold);

        using Font normalFont = new("Arial", 10, FontStyle.Regular);

        using Font smallFont = new("Arial", 8, FontStyle.Regular);

        // Draw the title centered at the top of the graph
        g.DrawString(
            "PLC XY Rotation / Kinematic Input Visualization",
            titleFont,
            Brushes.Black,
            centerX,
            marginTop / 2f,
            new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            });



        // ========================================================
        // DRAW PERMITTED / NON-PERMITTED SECTORS
        //
        // PLC says rotation required when:
        //
        // LeftLimit < angle < RightLimit
        //
        // With current values:
        // Chosen° < angle < Chosen°
        // ========================================================


        // Draw the rotation required and permitted sectors using semi-transparent brushes
        using Brush rotationRequiredBrush =
            new SolidBrush(
           Color.FromArgb(
          35,
          220,
          60,
          60));

        using Brush permittedBrush =
            new SolidBrush(
           Color.FromArgb(
           28,
           40,
           180,
           80));

        // Rotation required zone
        FillMathSector(
            g,
            centerX,
            centerY,
            plotRadiusPixels,
            _graphState.LeftLimitDeg,
            _graphState.RightLimitDeg,
            rotationRequiredBrush);

        // Allowed zone wraps around 360 / 0.
        FillMathSector(
            g,
            centerX,
            centerY,
            plotRadiusPixels,
            _graphState.RightLimitDeg,
            _graphState.LeftLimitDeg + 360.0,
            permittedBrush);

        // ========================================================
        // GRID CIRCLES
        // ========================================================
        using Pen gridPen =
   new(
       Color.LightGray,
       1f);

        gridPen.DashStyle =
            DashStyle.Dot;


        for (int i = 1; i <= 4; i++)
        {
            float r =
                plotRadiusPixels *
                i / 4f;

            g.DrawEllipse(
                gridPen,
                centerX - r,
                centerY - r,
                2f * r,
                2f * r);


            double worldRadius =
                maxRadius *
                i / 4.0;

            g.DrawString(
                worldRadius.ToString("F3"),
                smallFont,
                Brushes.DimGray,
                centerX + 4,
                centerY - r);
        }

        // ========================================================
        // X/Y AXES
        // ========================================================
        using Pen axisPen =
  new(
      Color.Black,
      1.5f);

        //X axis
        g.DrawLine(
            axisPen,
            centerX - plotRadiusPixels,
            centerY,
            centerX + plotRadiusPixels,
            centerY);

        //Y axis
        g.DrawLine(
            axisPen,
            centerX,
            centerY - plotRadiusPixels,
            centerX,
            centerY + plotRadiusPixels);

        g.DrawString(
    "+X / 0°",
    normalFont,
    Brushes.Black,
    centerX + plotRadiusPixels - 52,
    centerY + 4);

        g.DrawString(
            "-X / 180°",
            normalFont,
            Brushes.Black,
            centerX - plotRadiusPixels,
            centerY + 4);

        g.DrawString(
            "+Y / 90°",
            normalFont,
            Brushes.Black,
            centerX + 5,
            centerY - plotRadiusPixels);

        g.DrawString(
            "-Y / 270°",
            normalFont,
            Brushes.Black,
            centerX + 5,
            centerY + plotRadiusPixels - 18);



        // ========================================================
        // PLC LEFT / RIGHT LIMITS
        // ========================================================

        using Pen limitPen =
  new(
      Color.DarkRed,
      2f);

        limitPen.DashStyle =
            DashStyle.Dash;

        // Draw the left and right limit lines with labels
        DrawAngleLimit(
            g,
            centerX,
            centerY,
            plotRadiusPixels,
            _graphState.LeftLimitDeg,
            limitPen,
            normalFont,
            $"Left Limit {_graphState.LeftLimitDeg:F1}°");


        DrawAngleLimit(
            g,
            centerX,
            centerY,
            plotRadiusPixels,
            _graphState.RightLimitDeg,
            limitPen,
            normalFont,
            $"Right Limit {_graphState.RightLimitDeg:F1}°");


        // ========================================================
        // ZONE LABELS
        // ========================================================

        // Draw the labels for the rotation required and permitted zones
        DrawZoneLabel(
            g,
            centerX,
            centerY,
            plotRadiusPixels * 0.67f,
            240.0,
            "ROTATION REQUIRED",
            Color.DarkRed,
            normalFont);


        DrawZoneLabel(
            g,
            centerX,
            centerY,
            plotRadiusPixels * 0.67f,
            60.0,
            "PERMITTED",
            Color.DarkGreen,
            normalFont);

        // ========================================================
        // TRANSFORM POINTS
        // ========================================================

        // Transform the original, rotated, and final points from world coordinates to screen coordinates

        // Transform the original dog point
        PointF inputDog =
            WorldToScreen(
                _graphState.InputDogX,
                _graphState.InputDogY,
                centerX,
                centerY,
                pixelsPerUnit);

        // Transform the original tail point
        PointF inputTail =
            WorldToScreen(
                  _graphState.InputTailX,
                  _graphState.InputTailY,
                  centerX,
                  centerY,
                  pixelsPerUnit);

        // Transform the rotated dog point
        PointF rotatedDog =
            WorldToScreen(
                _graphState.RotatedDogX,
                _graphState.RotatedDogY,
                centerX,
                centerY,
                pixelsPerUnit);

        // Transform the rotated tail point
        PointF rotatedTail =
            WorldToScreen(
                _graphState.RotatedTailX,
                _graphState.RotatedTailY,
                centerX,
                centerY,
                pixelsPerUnit);

        // Transform the final dog point
        PointF finalDog =
            WorldToScreen(
                _graphState.FinalDogX,
                _graphState.FinalDogY,
                centerX,
                centerY,
                pixelsPerUnit);


        // Transform the final tail point
        PointF finalTail =
            WorldToScreen(
                _graphState.FinalTailX,
                _graphState.FinalTailY,
                centerX,
                centerY,
                pixelsPerUnit);

        // ========================================================
        // ORIGINAL AXIS / POINT PAIR
        // ========================================================
        using Pen originalAxisPen =
            new(
                Color.SteelBlue,
                2f);

        originalAxisPen.DashStyle =
            DashStyle.Dot;


        g.DrawLine(
            originalAxisPen,
            inputDog,
            inputTail);


        // Original vectors from center
        g.DrawLine(
            originalAxisPen,
            new PointF(centerX, centerY),
            inputDog);

        g.DrawLine(
            originalAxisPen,
            new PointF(centerX, centerY),
            inputTail);

        // ========================================================
        // ROTATED AXIS / POINT PAIR
        // ========================================================


        // Draw the rotated axis and points using a different color
        using Pen rotatedAxisPen =
            new(
                Color.DarkOrange,
                2.5f);


        g.DrawLine(
            rotatedAxisPen,
            rotatedDog,
            rotatedTail);


        g.DrawLine(
            rotatedAxisPen,
            new PointF(centerX, centerY),
            rotatedDog);

        g.DrawLine(
            rotatedAxisPen,
            new PointF(centerX, centerY),
            rotatedTail);


        // ========================================================
        // ORIGINAL POINTS
        // ========================================================

        // Draw the original dog and tail points using circles
        DrawCirclePoint(
            g,
            inputDog,
            Color.RoyalBlue,
            7f);

        DrawCirclePoint(
            g,
            inputTail,
            Color.MediumVioletRed,
            7f);


        DrawPointLabel(
            g,
            inputDog,
            $"Original Head/Dog\n" +
            $"({_graphState.InputDogX:F3}, {_graphState.InputDogY:F3})\n" +
            $"{_graphState.InputDogAngleDeg:F2}°",
            smallFont,
            Color.RoyalBlue);


        DrawPointLabel(
            g,
            inputTail,
            $"Original Tail\n" +
            $"({_graphState.InputTailX:F3}, {_graphState.InputTailY:F3})\n" +
            $"{_graphState.InputTailAngleDeg:F2}°",
            smallFont,
            Color.MediumVioletRed);


        // ========================================================
        // NEW PLC ROTATED POINTS
        // ========================================================
        // Draw the rotated dog and tail points using squares

        DrawSquarePoint(
            g,
            rotatedDog,
            Color.Navy,
            8f);

        DrawSquarePoint(
            g,
            rotatedTail,
            Color.DarkRed,
            8f);


        DrawPointLabel(
            g,
            rotatedDog,
            $"New Head/Dog\n" +
            $"({_graphState.RotatedDogX:F3}, {_graphState.RotatedDogY:F3})\n" +
            $"{_graphState.ResultDogAngleDeg:F2}°",
            smallFont,
            Color.Navy);


        DrawPointLabel(
            g,
            rotatedTail,
            $"New Tail\n" +
            $"({_graphState.RotatedTailX:F3}, {_graphState.RotatedTailY:F3})\n" +
            $"{_graphState.ResultTailAngleDeg:F2}°",
            smallFont,
            Color.DarkRed);

        // ========================================================
        // OPTIONAL OFFSET-ADJUSTED FINAL POINTS
        //
        // Only draw separately when offsets are enabled.
        // ========================================================

        if (_graphState.OffsetsEnabled)
        {
            using Pen offsetPen =
                new(
                    Color.Purple,
                    1.5f);

            offsetPen.DashStyle =
                DashStyle.DashDot;


            g.DrawLine(
                offsetPen,
                finalDog,
                finalTail);


            DrawDiamondPoint(
                g,
                finalDog,
                Color.Purple,
                8f);

            DrawDiamondPoint(
                g,
                finalTail,
                Color.Purple,
                8f);


            DrawPointLabel(
                g,
                finalDog,
                $"Final Head w/ Offset\n" +
                $"({_graphState.FinalDogX:F3}, {_graphState.FinalDogY:F3})",
                smallFont,
                Color.Purple);


            DrawPointLabel(
                g,
                finalTail,
                $"Final Tail w/ Offset\n" +
                $"({_graphState.FinalTailX:F3}, {_graphState.FinalTailY:F3})",
                smallFont,
                Color.Purple);
        }

        // ========================================================
        // ORIGIN
        // ========================================================

        DrawCirclePoint(
            g,
            new PointF(
                centerX,
                centerY),
            Color.Black,
            4f);

        g.DrawString(
            "Origin",
            smallFont,
            Brushes.Black,
            centerX + 5,
            centerY + 5);


        // ========================================================
        // BOTTOM STATUS
        // ========================================================

        string status =
            $"{_graphState.RotationCase}\n" +
            $"{_graphState.RotationBranch}\n" +
            $"SpindleRotateCalc = {_graphState.SpindleRotateCalc:F6}";


        g.DrawString(
            status,
            smallFont,
            Brushes.Black,
            10,
            height - 52);
        // ========================================================
        // UPDATE PICTUREBOX
        // ========================================================

        Image? oldImage =
            pictureBoxKinematicGraph.Image;

        pictureBoxKinematicGraph.Image =
            bitmap;

        oldImage?.Dispose();

    }















    // ============================================================
    // Build a detailed diagnostics report for the current kinematic solution
    // ============================================================


    /// <summary>Builds the step-by-step text report of one kinematic calculation (inputs, angles, zone checks, rotation, outputs).</summary>
    private string BuildDiagnosticsReport(
double dogXInput,
double dogYInput,
double tailXInput,
double tailYInput,

double dogAngle,
double tailAngle,

int dogInZone,
int tailInZone,

double dogRadius,
double tailRadius,

string rotationCase,
string rotationBranch,

double dogFromLeft,
double tailFromLeft,
double dogFromRight,
double tailFromRight,

double angleA,
double angleB,

double newDogAngle,
double newTailAngle,

double rotatedDogX,
double rotatedDogY,
double rotatedTailX,
double rotatedTailY,

double finalDogX,
double finalDogY,
double finalTailX,
double finalTailY,

double spindleRotateCalc,

double[] actualXy,

double newA1,
double newB1,
double newA2,
double newB2,

// Dog
double dog_r_1,
double dog_r_2,
double dog_r_3,
double dog_r_4,
double dog_r_5,
double dog_r_6,
double dog_r_7,

double dog_theta_1,
double dog_theta_2,
double dog_theta_3,
double dog_theta_4,
double dog_theta_5,
double dog_theta_6,
double dog_theta_7,

double dog_r_6_new,
double dog_r_7_new,
double dog_theta_5_new,
double dog_r_2_new,
double dog_theta_3_new,
double dog_r_4_new,
double dog_theta_4_new,

// Tail
double tail_r_1,
double tail_r_2,
double tail_r_3,
double tail_r_4,
double tail_r_5,
double tail_r_6,
double tail_r_7,

double tail_theta_1,
double tail_theta_2,
double tail_theta_3,
double tail_theta_4,
double tail_theta_5,
double tail_theta_6,
double tail_theta_7,

double tail_r_6_new,
double tail_r_7_new,
double tail_theta_5_new,
double tail_r_2_new,
double tail_theta_3_new,
double tail_r_4_new,
double tail_theta_4_new)
    {
        StringBuilder sb =
            new();


        sb.AppendLine(
            "============================================================");

        sb.AppendLine(
            "PLC KINEMATIC SOLUTION DIAGNOSTICS");

        sb.AppendLine(
            "============================================================");

        sb.AppendLine();


        // ========================================================
        // INPUTS
        // ========================================================

        sb.AppendLine(
            "=== INPUT XY ===");

        sb.AppendLine(
            $"Dog/Head Input X: {dogXInput:F6}");

        sb.AppendLine(
            $"Dog/Head Input Y: {dogYInput:F6}");

        sb.AppendLine(
            $"Tail Input X:     {tailXInput:F6}");

        sb.AppendLine(
            $"Tail Input Y:     {tailYInput:F6}");

        sb.AppendLine();


        // ========================================================
        // ANGLE CALCULATIONS
        // ========================================================

        sb.AppendLine(
            "=== PLC ANGLE CALCULATIONS ===");

        sb.AppendLine(
            DescribePlcAngleCalculation(
                "DOG/HEAD",
                dogXInput,
                dogYInput,
                dogAngle));

        sb.AppendLine();

        sb.AppendLine(
            DescribePlcAngleCalculation(
                "TAIL",
                tailXInput,
                tailYInput,
                tailAngle));

        sb.AppendLine();


        // ========================================================
        // ZONE
        // ========================================================

        sb.AppendLine(
            "=== ROTATION ZONE CHECK ===");

        sb.AppendLine(
            $"Left Limit:  {RotationNeededLeftLimit:F6} deg");

        sb.AppendLine(
            $"Right Limit: {RotationNeededRightLimit:F6} deg");

        sb.AppendLine();

        sb.AppendLine(
            "PLC condition:");

        sb.AppendLine(
            "LeftLimit < Angle < RightLimit");

        sb.AppendLine();

        sb.AppendLine(
            $"Dog:  {RotationNeededLeftLimit:F3} < " +
            $"{dogAngle:F6} < " +
            $"{RotationNeededRightLimit:F3}");

        sb.AppendLine(
            $"DogLCInZone = {dogInZone}");

        sb.AppendLine();

        sb.AppendLine(
            $"Tail: {RotationNeededLeftLimit:F3} < " +
            $"{tailAngle:F6} < " +
            $"{RotationNeededRightLimit:F3}");

        sb.AppendLine(
            $"TailLCInZone = {tailInZone}");

        sb.AppendLine();


        // ========================================================
        // MAGNITUDES
        // ========================================================

        sb.AppendLine(
            "=== VECTOR MAGNITUDES ===");

        sb.AppendLine(
            "DogRotSC2LC = sqrt(DogX^2 + DogY^2)");

        sb.AppendLine(
            $"= sqrt(({dogXInput:F6})^2 + ({dogYInput:F6})^2)");

        sb.AppendLine(
            $"= {dogRadius:F9}");

        sb.AppendLine();

        sb.AppendLine(
            "TailRotSC2LC = sqrt(TailX^2 + TailY^2)");

        sb.AppendLine(
            $"= sqrt(({tailXInput:F6})^2 + ({tailYInput:F6})^2)");

        sb.AppendLine(
            $"= {tailRadius:F9}");

        sb.AppendLine();


        // ========================================================
        // BRANCH
        // ========================================================

        sb.AppendLine(
            "=== PLC ROTATION BRANCH ===");

        sb.AppendLine(
            $"Case:   {rotationCase}");

        sb.AppendLine(
            $"Branch: {rotationBranch}");

        sb.AppendLine();


        sb.AppendLine(
            $"DogFromLeft:   {dogFromLeft:F9}");

        sb.AppendLine(
            $"TailFromLeft:  {tailFromLeft:F9}");

        sb.AppendLine(
            $"DogFromRight:  {dogFromRight:F9}");

        sb.AppendLine(
            $"TailFromRight: {tailFromRight:F9}");

        sb.AppendLine();

        sb.AppendLine(
            $"AngleA: {angleA:F9}");

        sb.AppendLine(
            $"AngleB: {angleB:F9}");

        sb.AppendLine();


        // ========================================================
        // RESULT ANGLES
        // ========================================================

        sb.AppendLine(
            "=== RESULT ANGLES ===");

        sb.AppendLine(
            $"Original Dog Angle:  {dogAngle:F9} deg");

        sb.AppendLine(
            $"Original Tail Angle: {tailAngle:F9} deg");

        sb.AppendLine(
            $"New Dog Angle:       {newDogAngle:F9} deg");

        sb.AppendLine(
            $"New Tail Angle:      {newTailAngle:F9} deg");

        sb.AppendLine();


        double dogDelta =
            NormalizeSignedDegrees(
                newDogAngle -
                dogAngle);

        double tailDelta =
            NormalizeSignedDegrees(
                newTailAngle -
                tailAngle);


        sb.AppendLine(
            $"Dog angular change:  {dogDelta:F9} deg");

        sb.AppendLine(
            $"Tail angular change: {tailDelta:F9} deg");

        sb.AppendLine(
            $"Shared rotation mismatch: " +
            $"{Math.Abs(dogDelta - tailDelta):F12} deg");

        sb.AppendLine();


        // ========================================================
        // ROTATED XY
        // ========================================================

        sb.AppendLine(
            "=== XY AFTER PLC ROTATION LOGIC ===");

        sb.AppendLine(
            $"DOGXCalc:  {rotatedDogX:F9}");

        sb.AppendLine(
            $"DOGYCalc:  {rotatedDogY:F9}");

        sb.AppendLine(
            $"TailXCalc: {rotatedTailX:F9}");

        sb.AppendLine(
            $"TailYCalc: {rotatedTailY:F9}");

        sb.AppendLine();


        // ========================================================
        // RADIUS PRESERVATION CHECK
        // ========================================================

        double rotatedDogRadius =
            Math.Sqrt(
                rotatedDogX * rotatedDogX +
                rotatedDogY * rotatedDogY);

        double rotatedTailRadius =
            Math.Sqrt(
                rotatedTailX * rotatedTailX +
                rotatedTailY * rotatedTailY);


        sb.AppendLine(
            "=== RADIUS PRESERVATION CHECK ===");

        sb.AppendLine(
            $"Dog original radius: {dogRadius:F12}");

        sb.AppendLine(
            $"Dog rotated radius:  {rotatedDogRadius:F12}");

        sb.AppendLine(
            $"Dog radius error:    " +
            $"{Math.Abs(dogRadius - rotatedDogRadius):F12}");

        sb.AppendLine();

        sb.AppendLine(
            $"Tail original radius: {tailRadius:F12}");

        sb.AppendLine(
            $"Tail rotated radius:  {rotatedTailRadius:F12}");

        sb.AppendLine(
            $"Tail radius error:    " +
            $"{Math.Abs(tailRadius - rotatedTailRadius):F12}");

        sb.AppendLine();


        // ========================================================
        // FINAL XY
        // ========================================================

        sb.AppendLine(
            "=== FINAL XY AFTER OPTIONAL CORRECTION OFFSETS ===");

        sb.AppendLine(
            $"Final Dog X:  {finalDogX:F9}");

        sb.AppendLine(
            $"Final Dog Y:  {finalDogY:F9}");

        sb.AppendLine(
            $"Final Tail X: {finalTailX:F9}");

        sb.AppendLine(
            $"Final Tail Y: {finalTailY:F9}");

        sb.AppendLine();


        // ========================================================
        // ACTUAL XY ARRAY
        // ========================================================

        sb.AppendLine(
            "=== ActualXYSolution_XY ===");

        for (int i = 0;
             i < actualXy.Length;
             i++)
        {
            sb.AppendLine(
                $"ActualXYSolution_XY[{i}] = " +
                $"{actualXy[i]:F9}");
        }

        sb.AppendLine();


        // ========================================================
        // SPINDLE
        // ========================================================

        sb.AppendLine(
            "=== SPINDLE ROTATION ===");

        sb.AppendLine(
            $"SpindleRotateCalc = {spindleRotateCalc:F12}");

        sb.AppendLine();


        // ========================================================
        // NEWTON RESIDUALS
        //
        // Recompute residuals AFTER final iteration.
        // This does not alter PLC math.
        // ========================================================

        sb.AppendLine(
            "=== NEWTON SOLVER FINAL RESIDUALS ===");


        // DOG J1
        double dogJ1ResidualX =
            dog_r_1 * Math.Cos(dog_theta_1)
            +
            dog_r_2 * Math.Cos(dog_theta_2)
            +
            dog_r_3 * Math.Cos(dog_theta_3)
            +
            dog_r_4 * Math.Cos(dog_theta_4);

        double dogJ1ResidualY =
            dog_r_2 * Math.Sin(dog_theta_2)
            +
            dog_r_3 * Math.Sin(dog_theta_3)
            +
            dog_r_4 * Math.Sin(dog_theta_4);


        AppendResidual(
            sb,
            "Dog J1",
            dogJ1ResidualX,
            dogJ1ResidualY);


        // DOG J2
        double dogJ2ResidualX =
            dog_r_5 * Math.Cos(dog_theta_5)
            -
            dog_r_6 * Math.Cos(dog_theta_6);

        double dogJ2ResidualY =
            dog_r_2 * Math.Sin(dog_theta_2)
            +
            dog_r_5 * Math.Sin(dog_theta_5)
            -
            dog_r_7 * Math.Sin(dog_theta_7);


        AppendResidual(
            sb,
            "Dog J2",
            dogJ2ResidualX,
            dogJ2ResidualY);


        // DOG J3
        double dogJ3ResidualX =
            dog_r_5 * Math.Cos(dog_theta_5_new)
            -
            dog_r_6_new;

        double dogJ3ResidualY =
            -dog_r_2_new
            +
            dog_r_5 * Math.Sin(dog_theta_5_new)
            +
            dog_r_7_new;


        AppendResidual(
            sb,
            "Dog J3",
            dogJ3ResidualX,
            dogJ3ResidualY);


        // DOG J4
        double dogJ4ResidualX =
            -dog_r_1
            +
            dog_r_3 * Math.Cos(dog_theta_3_new)
            +
            dog_r_4_new * Math.Cos(dog_theta_4_new);

        double dogJ4ResidualY =
            -dog_r_2_new
            +
            dog_r_3 * Math.Sin(dog_theta_3_new)
            +
            dog_r_4_new * Math.Sin(dog_theta_4_new);


        AppendResidual(
            sb,
            "Dog J4",
            dogJ4ResidualX,
            dogJ4ResidualY);


        // TAIL J1
        //
        // IMPORTANT:
        // Preserve the printed PLC reference to dog_theta_1
        // and dog_theta_2 in the Tail J1 equations.
        double tailJ1ResidualX =
            tail_r_1 * Math.Cos(dog_theta_1)
            +
            tail_r_2 * Math.Cos(tail_theta_2)
            +
            tail_r_3 * Math.Cos(tail_theta_3)
            +
            tail_r_4 * Math.Cos(tail_theta_4);

        double tailJ1ResidualY =
            tail_r_2 * Math.Sin(dog_theta_2)
            +
            tail_r_3 * Math.Sin(tail_theta_3)
            +
            tail_r_4 * Math.Sin(tail_theta_4);


        AppendResidual(
            sb,
            "Tail J1",
            tailJ1ResidualX,
            tailJ1ResidualY);


        // TAIL J2
        double tailJ2ResidualX =
            tail_r_5 * Math.Cos(tail_theta_5)
            -
            tail_r_6 * Math.Cos(tail_theta_6);

        double tailJ2ResidualY =
            tail_r_2 * Math.Sin(tail_theta_2)
            +
            tail_r_5 * Math.Sin(tail_theta_5)
            -
            tail_r_7 * Math.Sin(tail_theta_7);


        AppendResidual(
            sb,
            "Tail J2",
            tailJ2ResidualX,
            tailJ2ResidualY);


        // TAIL J3
        double tailJ3ResidualX =
            tail_r_5 * Math.Cos(tail_theta_5_new)
            -
            tail_r_6_new;

        double tailJ3ResidualY =
            -tail_r_2_new
            +
            tail_r_5 * Math.Sin(tail_theta_5_new)
            +
            tail_r_7_new;


        AppendResidual(
            sb,
            "Tail J3",
            tailJ3ResidualX,
            tailJ3ResidualY);


        // TAIL J4
        double tailJ4ResidualX =
            -tail_r_1
            +
            tail_r_3 * Math.Cos(tail_theta_3_new)
            +
            tail_r_4_new * Math.Cos(tail_theta_4_new);

        double tailJ4ResidualY =
            -tail_r_2_new
            +
            tail_r_3 * Math.Sin(tail_theta_3_new)
            +
            tail_r_4_new * Math.Sin(tail_theta_4_new);


        AppendResidual(
            sb,
            "Tail J4",
            tailJ4ResidualX,
            tailJ4ResidualY);


        // ========================================================
        // FINAL CYLINDER TARGETS
        // ========================================================

        sb.AppendLine(
            "=== FINAL POSITIONING CYLINDER TARGETS ===");

        sb.AppendLine(
            $"A1 / New_A1_X_Target = {newA1:F9}");

        sb.AppendLine(
            $"B1 / New_B1_Y_Target = {newB1:F9}");

        sb.AppendLine(
            $"A2 / New_A2_X_Target = {newA2:F9}");

        sb.AppendLine(
            $"B2 / New_B2_Y_Target = {newB2:F9}");

        sb.AppendLine();


        // ========================================================
        // DIAGNOSTIC WARNINGS
        // ========================================================

        sb.AppendLine(
            "=== AUTOMATIC CHECKS ===");


        double resultDogAngle =
            GetDisplayAngleDeg(
                rotatedDogX,
                rotatedDogY);

        double resultTailAngle =
            GetDisplayAngleDeg(
                rotatedTailX,
                rotatedTailY);


        bool resultDogStillInZone =
            resultDogAngle >
            RotationNeededLeftLimit
            &&
            resultDogAngle <
            RotationNeededRightLimit;

        bool resultTailStillInZone =
            resultTailAngle >
            RotationNeededLeftLimit
            &&
            resultTailAngle <
            RotationNeededRightLimit;


        sb.AppendLine(
            $"Result Dog Angle from XY: " +
            $"{resultDogAngle:F9} deg");

        sb.AppendLine(
            $"Result Tail Angle from XY: " +
            $"{resultTailAngle:F9} deg");

        sb.AppendLine();


        if (resultDogStillInZone)
        {
            sb.AppendLine(
                "[WARNING] Result Dog/Head point is still inside " +
                "the PLC rotation-required zone.");
        }
        else
        {
            sb.AppendLine(
                "[OK] Result Dog/Head point is outside the " +
                "rotation-required zone.");
        }


        if (resultTailStillInZone)
        {
            sb.AppendLine(
                "[WARNING] Result Tail point is still inside " +
                "the PLC rotation-required zone.");
        }
        else
        {
            sb.AppendLine(
                "[OK] Result Tail point is outside the " +
                "rotation-required zone.");
        }


        return sb.ToString();
    }


    //--------------------------------------------------------------
    // Helper methods for diagnostics report
    //--------------------------------------------------------------
    /// <summary>Explains, as text, how the PLC-style angle for one point was worked out (quadrant rules).</summary>
    private static string DescribePlcAngleCalculation(
string name,
double x,
double y,
double resultAngle)
    {
        StringBuilder sb =
            new();


        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"{name}:");

        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"X = {x:F9}");

        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"Y = {y:F9}");


        if (x > 0 && y > 0)
        {
            sb.AppendLine(
                "Quadrant 1");

            sb.AppendLine(
                $"Angle = 57.2958 * ATAN(Y / X)");

            sb.AppendLine(
                CultureInfo.InvariantCulture,
                $"      = 57.2958 * ATAN({y:F9} / {x:F9})");
        }


        if (x < 0 && y > 0)
        {
            sb.AppendLine(
                "Quadrant 2");

            sb.AppendLine(
                "Angle = 180 + 57.2958 * ATAN(Y / X)");
        }


        if (x < 0 && y < 0)
        {
            sb.AppendLine(
                "Quadrant 3");

            sb.AppendLine(
                "Angle = 180 + 57.2958 * ATAN(Y / X)");
        }


        if (x > 0 && y < 0)
        {
            sb.AppendLine(
                "Quadrant 4");

            sb.AppendLine(
                "Angle = 360 + 57.2958 * ATAN(Y / X)");
        }


        if (x == 0 && y > 0)
        {
            sb.AppendLine(
                "Positive Y axis special case");

            sb.AppendLine(
                "Angle = 90");
        }


        if (x < 0 && y == 0)
        {
            sb.AppendLine(
                "Negative X axis special case");

            sb.AppendLine(
                "Angle = 180");
        }


        if (x == 0 && y < 0)
        {
            sb.AppendLine(
                "Negative Y axis special case");

            sb.AppendLine(
                "Angle = 270");
        }


        if (x > 0 && y == 0)
        {
            sb.AppendLine(
                "Positive X axis special case");

            sb.AppendLine(
                "Angle = 0");
        }
        //Saftey check that should be put in PLC code to protect againt a 0,0 Position (it could happen)

        if (x == 0 && y == 0)
        {
            sb.AppendLine(
                "[WARNING] X = 0 and Y = 0.");

            sb.AppendLine(
                "The printed PLC quadrant logic has no explicit " +
                "(0,0) case.");
        }


        sb.AppendLine(
            CultureInfo.InvariantCulture,
            $"Final PLC Angle = {resultAngle:F9} deg");


        return sb.ToString();
    }


    //--------------------------------------------------------------
    // Helper methods for angle calculations
    //--------------------------------------------------------------
    /// <summary>Wraps an angle into the range (-180, 180] degrees.</summary>
    private static double NormalizeSignedDegrees(double angle)
    {
        while (angle > 180.0)
        {
            angle -= 360.0;
        }

        while (angle <= -180.0)
        {
            angle += 360.0;
        }

        return angle;
    }


    //--------------------------------------------------------------
    // Helper methods for diagnostics report
    //--------------------------------------------------------------
    /// <summary>Angle of (x, y) in degrees for display (0 for the origin).</summary>
    private static double GetDisplayAngleDeg(
double x,
double y)
    {
        if (x == 0.0 &&
            y == 0.0)
        {
            return 0.0;
        }

        double angle =
            Math.Atan2(
                y,
                x)
            *
            180.0 /
            Math.PI;

        if (angle < 0.0)
        {
            angle += 360.0;
        }

        return angle;
    }

    // --------------------------------------------------------------
    // Helper method to append residual information to the diagnostics report.
    // --------------------------------------------------------------
    /// <summary>Adds one "residual" line (difference between expected and calculated X/Y and its size) to the report.</summary>
    private static void AppendResidual(
StringBuilder sb,
string name,
double residualX,
double residualY)
    {
        double magnitude =
            Math.Sqrt(
                residualX * residualX +
                residualY * residualY);


        sb.AppendLine(
                $"{name}:");

        sb.AppendLine(
            $"  Residual X = {residualX:E12}");

        sb.AppendLine(
            $"  Residual Y = {residualY:E12}");

        sb.AppendLine(
            $"  Magnitude  = {magnitude:E12}");

        sb.AppendLine();
    }


    //--------------------------------------------------------------
    // Helper methods for graph rendering
    //--------------------------------------------------------------

    // Calculate the radius of a point in the graph based on its x and y coordinates.
    /// <summary>Distance of (x, y) from the origin.</summary>
    private static double CalcGraphRadius(
        double x,
        double y)
    {
        return Math.Sqrt(
            x * x +
            y * y);
    }


    // Convert world coordinates (x, y) to screen coordinates (PointF) based on the center of the plot and the scaling factor (pixelsPerUnit).
    /// <summary>Converts machine X/Y to picture pixels (screen Y points down, so Y is flipped).</summary>
    private static PointF WorldToScreen(
double x,
double y,
float centerX,
float centerY,
double pixelsPerUnit)
    {
        return new PointF(
            centerX +
            (float)(x * pixelsPerUnit),

            centerY -
            (float)(y * pixelsPerUnit));
    }


    // Fill a sector of a circle on the graphics object (g) with a specified brush, given the center coordinates, radius, and start/end angles in degrees.
    /// <summary>Fills a pie slice between two maths-convention angles (counter-clockwise from +X).</summary>
    private static void FillMathSector(
Graphics g,
float centerX,
float centerY,
float radius,
double startAngleDeg,
double endAngleDeg,
Brush brush)
    {
        using GraphicsPath path =
            new();

        path.StartFigure();

        path.AddLine(
            centerX,
            centerY,
            centerX +
            radius *
            (float)Math.Cos(
                startAngleDeg *
                Math.PI / 180.0),

            centerY -
            radius *
            (float)Math.Sin(
                startAngleDeg *
                Math.PI / 180.0));


        const double stepDeg = 2.0;

        for (double angle = startAngleDeg;
             angle <= endAngleDeg;
             angle += stepDeg)
        {
            double radians =
                angle *
                Math.PI / 180.0;

            float x =
                centerX +
                radius *
                (float)Math.Cos(radians);

            float y =
                centerY -
                radius *
                (float)Math.Sin(radians);

            path.AddLine(
                path.GetLastPoint(),
                new PointF(x, y));
        }


        // Guarantee exact endpoint.
        double endRadians =
            endAngleDeg *
            Math.PI / 180.0;

        path.AddLine(
            path.GetLastPoint(),
            new PointF(
                centerX +
                radius *
                (float)Math.Cos(endRadians),

                centerY -
                radius *
                (float)Math.Sin(endRadians)));


        path.AddLine(
            path.GetLastPoint(),
            new PointF(
                centerX,
                centerY));

        path.CloseFigure();

        g.FillPath(
            brush,
            path);
    }


    /// <summary>
    /// Draws a line representing an angle limit (boundary) on the graph with a label.
    ///
    /// This method draws a radial line from the spindle center at a specified angle,
    /// extending to the edge of the graph area. A label showing the angle in degrees
    /// is placed near the line for identification.
    ///
    /// Used to visualize the left and right boundaries of the rotation zone.
    /// </summary>
    /// <param name="g">Graphics object to draw with</param>
    /// <param name="centerX">X coordinate of the spindle center in pixels</param>
    /// <param name="centerY">Y coordinate of the spindle center in pixels</param>
    /// <param name="radius">Radius of the graph area in pixels</param>
    /// <param name="angleDegrees">Angle of the limit line in degrees (0-360)</param>
    /// <param name="pen">Pen to use for drawing the line</param>
    /// <param name="label">Text label to display at the line (e.g., "135°")</param>
    private static void DrawAngleLimit(
Graphics g,
float centerX,
float centerY,
float radius,
double angleDeg,
Pen pen,
Font font,
string label)
    {
        double radians =
            angleDeg *
            Math.PI / 180.0;

        float endX =
            centerX +
            radius *
            (float)Math.Cos(radians);

        float endY =
            centerY -
            radius *
            (float)Math.Sin(radians);


        g.DrawLine(
            pen,
            centerX,
            centerY,
            endX,
            endY);


        float labelX =
            centerX +
            radius * 0.72f *
            (float)Math.Cos(radians);

        float labelY =
            centerY -
            radius * 0.72f *
            (float)Math.Sin(radians);


        g.DrawString(
            label,
            font,
            Brushes.DarkRed,
            labelX,
            labelY);
    }


    /// <summary>
    /// Draws a text label for a zone (rotaton zone or safe zone) on the radial graph.
    ///
    /// This method positions text at a specified angle and radius from the graph center,
    /// displayed in a specified color. Used to label the danger zone (which requires rotation)
    /// and the safe zones (which do not require rotation).
    ///
    /// The text is positioned at the specified polar coordinates and centered at that point.
    /// </summary>
    /// <param name="g">Graphics object to draw with</param>
    /// <param name="centerX">X coordinate of the spindle center in pixels</param>
    /// <param name="centerY">Y coordinate of the spindle center in pixels</param>
    /// <param name="radius">Radius of the graph area in pixels</param>
    /// <param name="angleDeg">Angle where the label should be positioned in degrees (0-360)</param>
    /// <param name="text">The text label to display</param>
    /// <param name="color">Color to draw the text in</param>
    /// <param name="font">Font to use for the text</param>
    private static void DrawZoneLabel(
Graphics g,
float centerX,
float centerY,
float radius,
double angleDeg,
string text,
Color color,
Font font)
    {
        double radians =
            angleDeg *
            Math.PI / 180.0;

        float x =
            centerX +
            radius *
            (float)Math.Cos(radians);

        float y =
            centerY -
            radius *
            (float)Math.Sin(radians);


        using Brush brush =
            new SolidBrush(color);


        SizeF size =
            g.MeasureString(
                text,
                font);


        g.DrawString(
            text,
            font,
            brush,
            x - size.Width / 2f,
            y - size.Height / 2f);
    }



    //--------------------------------------------------------------
    // Helper methods for drawing points and labels
    //--------------------------------------------------------------


    //  Draw a circular point on the graph at a specified location, with a given color and radius.
    /// <summary>Draws a filled circle marker with a black outline.</summary>
    private static void DrawCirclePoint(
Graphics g,
PointF point,
Color color,
float radius)
    {
        using Brush brush =
            new SolidBrush(color);

        using Pen pen =
            new(
                Color.Black,
                1f);


        g.FillEllipse(
            brush,
            point.X - radius,
            point.Y - radius,
            radius * 2f,
            radius * 2f);

        g.DrawEllipse(
            pen,
            point.X - radius,
            point.Y - radius,
            radius * 2f,
            radius * 2f);
    }


    // Draw a square point on the graph at a specified location, with a given color and half-size (distance from center to edge).
    /// <summary>Draws a filled square marker with a black outline.</summary>
    private static void DrawSquarePoint(
Graphics g,
PointF point,
Color color,
float halfSize)
    {
        RectangleF rect =
            new(
                point.X - halfSize,
                point.Y - halfSize,
                halfSize * 2f,
                halfSize * 2f);


        using Brush brush =
            new SolidBrush(color);

        using Pen pen =
            new(
                Color.Black,
                1f);


        g.FillRectangle(
            brush,
            rect);

        g.DrawRectangle(
            pen,
            rect.X,
            rect.Y,
            rect.Width,
            rect.Height);
    }


    // Draw a diamond-shaped point on the graph at a specified location, with a given color and radius (distance from center to vertex).
    /// <summary>Draws a filled diamond marker with a black outline.</summary>
    private static void DrawDiamondPoint(
Graphics g,
PointF point,
Color color,
float radius)
    {
        PointF[] points =
        [
    new(
            point.X,
            point.Y - radius),

        new(
            point.X + radius,
            point.Y),

        new(
            point.X,
            point.Y + radius),

        new(
            point.X - radius,
            point.Y)
];


        using Brush brush =
            new SolidBrush(color);

        using Pen pen =
            new(
                Color.Black,
                1f);


        g.FillPolygon(
            brush,
            points);

        g.DrawPolygon(
            pen,
            points);
    }

    /// <summary>
    /// Draws a text label for a point on the graph at a specified pixel location.
    ///
    /// This method displays a label next to a point (such as a component position)
    /// on the kinematics graph. The text is drawn in the specified color and font.
    /// Used to label component positions, angles, and other key points on the graph.
    /// </summary>
    /// <param name="g">Graphics object to draw with</param>
    /// <param name="point">The pixel coordinates where the label should be drawn</param>
    /// <param name="text">The text label to display</param>
    /// <param name="font">Font to use for the text</param>
    /// <param name="color">Color to draw the text in</param>
    private static void DrawPointLabel(
Graphics g,
PointF point,
string text,
Font font,
Color color)
    {
        using Brush brush =
            new SolidBrush(color);


        g.DrawString(
            text,
            font,
            brush,
            point.X + 9f,
            point.Y + 5f);
    }


}

