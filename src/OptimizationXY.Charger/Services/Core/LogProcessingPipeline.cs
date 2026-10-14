using System.Globalization;
using Emgu.CV;
using OptimizationXY.Charger.Models.Calibration;
using OptimizationXY.Charger.Models.Diagnostics;
using OptimizationXY.Charger.Models.Geometry;
using OptimizationXY.Charger.Models.Optimization;
using OptimizationXY.Charger.Models.Optimization.Scoring;
using OptimizationXY.Charger.Models.Optimization.Geometry;
using OptimizationXY.Charger.Models.Optimization.Results;
using OptimizationXY.Charger.Models.Optimization.Recovery;
using OptimizationXY.Charger.Models.Optimization.Spindle;
using OptimizationXY.Charger.Models.Settings;
using OptimizationXY.Charger.Services.Analysis;
using OptimizationXY.Charger.Services.Defects;
using OptimizationXY.Charger.Services.Geometry;
using OptimizationXY.Charger.Services.Input;
using OptimizationXY.Charger.Services.Optimization.Scoring;
using OptimizationXY.Charger.Services.Optimization.Geometry;
using OptimizationXY.Charger.Services.Optimization.Recovery;
using OptimizationXY.Charger.Services.Optimization.Search;
using OptimizationXY.Charger.Services.Optimization.Wane;
using OptimizationXY.Charger.Services.PointCloud;
using OptimizationXY.Charger.Services.Preprocessing;
using OptimizationXY.Charger.Services.Profile;
using OptimizationXY.Charger.Visualization;
using System.Diagnostics;
using System.Text;
using OptimizationXY.Charger.Models.Core;
using OptimizationXY.Charger.Models.Analysis;

namespace OptimizationXY.Charger.Services.Core;

/// <summary>
/// Represents the main processing pipeline for log scanning and analysis. This class is responsible for loading
/// scan frames, processing them to build profiles, constructing point clouds, fitting centerlines, building
/// radius fields, binning radius data, analyzing ovality, and detecting defects.
/// It maintains the state of the pipeline and provides methods to execute each step in the processing workflow.
/// </summary>
public sealed class LogProcessingPipeline
{
    private static readonly Lazy<LogProcessingPipeline> _instance =
        new(() => new LogProcessingPipeline());

    /// <summary>
    /// Filters out bad profile ends from the given log profile based on a minimum valid end diameter fraction.
    /// </summary>
    /// <param name="profile">The log profile to filter.</param>
    /// <param name="MinValidEndDiameterFraction">The minimum valid end diameter fraction.</param>
    /// <returns>The filtered log profile.</returns>
    private static List<LogProfileColumn> FilterBadProfileEnds(List<LogProfileColumn> profile, double MinValidEndDiameterFraction)
    {
        if (profile == null || profile.Count < 20)
            return profile ?? [];

        var ordered = profile
            .Where(p => p.DiameterMM > 0)
            .Where(p => !double.IsNaN(p.DiameterMM))
            .Where(p => !double.IsInfinity(p.DiameterMM))
            .OrderBy(p => p.X)
            .ToList();

        if (ordered.Count < 20)
            return ordered;

        double maxDiameter = ordered.Max(p => p.DiameterMM);

        // Anything smaller than this is probably fixture/glare/collapsed laser,
        // not usable log body.
        double minValidDiameter =
            maxDiameter * MinValidEndDiameterFraction;

        int start = FindStableStart(ordered, minValidDiameter);
        int end = FindStableEnd(ordered, minValidDiameter);

        if (end <= start)
            return ordered;

        return [.. ordered
            .Skip(start)
            .Take(end - start + 1)];
    }

    /// <summary>
    /// Builds a log profile from a given scan frame using the provided scanner lookup table.
    /// </summary>
    /// <param name="frame">The scan frame to process.</param>
    /// <param name="lookupTable">The scanner lookup table.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The built log profile.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the profile cannot be built due to invalid data or calibration.</exception>
    private List<LogProfileColumn> BuildProfileFromFrame(
        ScanFrame frame,
        ScannerLookupTable lookupTable,
        CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(lookupTable);
        token.ThrowIfCancellationRequested();

        string stepPrefix = string.IsNullOrWhiteSpace(frame.ImagePath)
            ? $"Frame {frame.FrameIndex}"
            : Path.GetFileName(frame.ImagePath);

        Mat gray;
        if (frame.GrayImage is not null && !frame.GrayImage.IsEmpty)
        {
            gray = frame.GrayImage;
        }
        else
        {
            gray = RunTimed(
                $"{stepPrefix} - Load Gray Image",
                () => ImageInputService.LoadGrayImage(frame.ImagePath));

            frame.GrayImage?.Dispose();
            frame.GrayImage = gray;
        }

        frame.BinaryImage?.Dispose();
        frame.BinaryImage = RunTimed(
            $"{stepPrefix} - Build Binary Image",
            () => ImagePreprocessingService.BuildBinary(gray));

        if (frame.BinaryImage.IsEmpty)
            throw new InvalidOperationException("Could not build binary image.");

        List<LaserProfileColumn> laserProfile = RunTimed(
            $"{stepPrefix} - Detect Laser Stripe",
            () => LaserStripeDetector.Detect(
                frame.BinaryImage,
                gray,
                keepAllAlignedSegments: _state.Settings.RotatingScan.KeepAllStripeSegments,
                subPixelCenter: _state.Settings.RotatingScan.SubPixelStripeCenter));

        if (laserProfile.Count == 0)
            throw new InvalidOperationException("No laser stripe points detected.");

        if (!lookupTable.IsValid)
            throw new InvalidOperationException("Scanner calibration is invalid.");

        List<LogProfileColumn> profile = RunTimed(
            $"{stepPrefix} - Build Radius Profile",
            () => LaserRadiusProfileBuilder.BuildFromLookupTable(
                laserProfile,
                lookupTable,
                smoothingWindow: 9,
                maximumLargeRadiusExtrapolationPixels:
                    _state.Settings.RotatingScan.MaximumCalibrationExtrapolationPixels));

        if (profile.Count == 0)
            throw new InvalidOperationException("Profile build failed.");

        return RunTimed(
            $"{stepPrefix} - Filter Bad Profile Ends",
            () => FilterBadProfileEnds(
                profile,
                _state.Settings.RotatingScan.MinValidEndDiameterFraction));
    }

    /// <summary>
    /// Trims the ends of the local centerline slices to remove unreliable data at the log ends.
    /// </summary>
    /// <param name="slices">The list of local centerline slices to trim.</param>
    /// <param name="trimFraction">The fraction of slices to trim from each end.</param>
    /// <returns>The trimmed list of local centerline slices.</returns>
    private static List<LocalCenterlineSlice> TrimCenterlineEnds(List<LocalCenterlineSlice> slices, double trimFraction = 0.03)
    {
        if (slices == null || slices.Count < 10)
            return slices ?? [];

        var ordered = slices
            .OrderBy(s => s.ZMM)
            .ToList();

        int trimCount = (int)Math.Round(ordered.Count * trimFraction);

        trimCount = Math.Clamp(trimCount, 1, Math.Max(1, ordered.Count / 10));

        if (ordered.Count <= trimCount * 2 + 3)
            return ordered;

        return [.. ordered
            .Skip(trimCount)
            .Take(ordered.Count - trimCount * 2)];
    }

    /// <summary>
    /// Finds the first index where a stable run of valid diameters occurs.
    /// </summary>
    /// <param name="profile">The list of log profile columns to search.</param>
    /// <param name="minValidDiameter">The minimum valid diameter.</param>
    /// <returns>The index of the first stable start.</returns>
    private static int FindStableStart(List<LogProfileColumn> profile, double minValidDiameter)
    {
        int requiredRun = 5;

        for (int i = 0; i < profile.Count - requiredRun; i++)
        {
            bool stable = true;

            for (int j = 0; j < requiredRun; j++)
            {
                if (profile[i + j].DiameterMM < minValidDiameter)
                {
                    stable = false;
                    break;
                }
            }

            if (stable)
                return i;
        }

        return 0;
    }

    /// <summary>
    /// Finds the last index where a stable run of valid diameters occurs.
    /// </summary>
    /// <param name="profile">The list of log profile columns to search.</param>
    /// <param name="minValidDiameter">The minimum valid diameter.</param>
    /// <returns>The index of the last stable end.</returns>
    private static int FindStableEnd(List<LogProfileColumn> profile, double minValidDiameter)
    {
        int requiredRun = 5;

        for (int i = profile.Count - requiredRun - 1; i >= 0; i--)
        {
            bool stable = true;

            for (int j = 0; j < requiredRun; j++)
            {
                if (profile[i + j].DiameterMM < minValidDiameter)
                {
                    stable = false;
                    break;
                }
            }

            if (stable)
                return i + requiredRun - 1;
        }

        return profile.Count - 1;
    }

    /// <summary>
    /// Validates the proposed tail/head spindle movements against machine limits and warnings.
    /// </summary>
    /// <param name="tailXMM">The tail X movement in millimeters.</param>
    /// <param name="tailYMM">The tail Y movement in millimeters.</param>
    /// <param name="headXMM">The head X movement in millimeters.</param>
    /// <param name="headYMM">The head Y movement in millimeters.</param>
    /// <param name="limits">The machine limit settings.</param>
    /// <returns>A list of warning messages.</returns>
    private static List<string> ValidateMachineMovement(double tailXMM, double tailYMM, double headXMM, double headYMM,
OptimizationXY.Charger.Models.Settings.MachineLimitSettings limits)
    {
        var warnings = new List<string>();

        CheckAxis("Tail X", tailXMM, limits.MaxTailMoveXMM, limits.WarningMoveMM, warnings);
        CheckAxis("Tail Y", tailYMM, limits.MaxTailMoveYMM, limits.WarningMoveMM, warnings);
        CheckAxis("Head X", headXMM, limits.MaxHeadMoveXMM, limits.WarningMoveMM, warnings);
        CheckAxis("Head Y", headYMM, limits.MaxHeadMoveYMM, limits.WarningMoveMM, warnings);

        return warnings;
    }

    /// <summary>
    /// Checks a single axis movement against its maximum allowed and warning thresholds, adding messages to the warnings list as appropriate.
    /// </summary>
    /// <param name="name"></param>
    /// <param name="valueMM"></param>
    /// <param name="maxAllowedMM"></param>
    /// <param name="warningMM"></param>
    /// <param name="warnings"></param>
    private static void CheckAxis(
        string name,
        double valueMM,
        double maxAllowedMM,
        double warningMM,
        List<string> warnings)
    {
        double absMM = Math.Abs(valueMM);

        if (absMM > maxAllowedMM)
        {
            warnings.Add(
                $"BLOCKED: {name} movement {FormatLength(valueMM)} exceeds machine limit {FormatLength(maxAllowedMM)}.");
            return;
        }

        if (absMM > warningMM)
        {
            warnings.Add(
                $"WARNING: {name} movement {FormatLength(valueMM)} is above warning level {FormatLength(warningMM)}.");
        }
    }

    /// <summary>
    /// Gets the singleton instance of the LogProcessingPipeline.
    /// </summary>
    public static LogProcessingPipeline Instance => _instance.Value;

    /// <summary>
    /// Singleton instance of the PipelineState.
    /// </summary>
    private readonly PipelineState _state = PipelineState.Instance;

    /// <summary>
    /// Private constructor to prevent external instantiation.
    /// </summary>
    private LogProcessingPipeline()
    {
    }

    /// <summary>
    /// Public property to access the current state of the pipeline.
    /// </summary>




    // ------------------------------------------------------------
    // PROCESS ROTATING SCAN FRAMES
    // Processes each frame in the rotating scan folder, building a profile for each.
    // ------------------------------------------------------------

    /// <summary>
    /// Allowed amount, in millimetres, by which one frame's stripe may run past
    /// the end of the block agreed by most frames. A block end that is not cut
    /// square moves the visible end by a few millimetres as the block turns;
    /// a reflection or image artifact past the end runs much further.
    /// </summary>
    private const double FrameLengthConsensusToleranceMM = 40.0;

    /// <summary>
    /// The block does not change length as it turns, so every frame should see
    /// the stripe end at about the same image column. A frame whose stripe runs
    /// well past the end agreed by the median frame has picked up something that
    /// is not wood (for example a flat band that continues the stripe to the
    /// image edge past the tail end). Those columns are removed from that frame
    /// only; the rest of the frame is kept.
    /// </summary>
    /// <returns>Report lines describing what was trimmed (empty when nothing was).</returns>
    private string TrimFramesBeyondConsensusLength(ScannerLookupTable lookupTable)
    {
        var framesWithProfiles = _state.ScanFrames
            .Where(frame => frame.IsValid && frame.Profile is { Count: > 0 })
            .ToList();

        if (framesWithProfiles.Count < 5)
            return string.Empty;

        double pixelsPerMM =
            double.IsFinite(lookupTable.LengthPixelsPerMM) && lookupTable.LengthPixelsPerMM > 0.0
                ? lookupTable.LengthPixelsPerMM
                : 0.5;
        int tolerancePixels = Math.Max(
            5,
            (int)Math.Round(FrameLengthConsensusToleranceMM * pixelsPerMM));

        static int MedianOf(List<int> values)
        {
            values.Sort();
            return values[values.Count / 2];
        }

        int consensusFirstX = MedianOf([.. framesWithProfiles.Select(frame => frame.Profile.Min(column => column.X))]);
        int consensusLastX = MedianOf([.. framesWithProfiles.Select(frame => frame.Profile.Max(column => column.X))]);

        int trimmedFrames = 0;
        int trimmedColumns = 0;

        foreach (ScanFrame frame in framesWithProfiles)
        {
            int before = frame.Profile.Count;

            if (frame.Profile.Max(column => column.X) <= consensusLastX + tolerancePixels &&
                frame.Profile.Min(column => column.X) >= consensusFirstX - tolerancePixels)
            {
                continue;
            }

            List<LogProfileColumn> kept = [.. frame.Profile
                .Where(column =>
                    column.X <= consensusLastX &&
                    column.X >= consensusFirstX)];

            if (kept.Count == 0)
                continue;

            frame.Profile = kept;
            trimmedFrames++;
            trimmedColumns += before - kept.Count;
        }

        if (trimmedFrames == 0)
            return string.Empty;

        return
            $"Frames trimmed to the agreed block length: {trimmedFrames} " +
            $"({trimmedColumns} stripe columns past image X {consensusFirstX}-{consensusLastX} removed; " +
            $"tolerance {tolerancePixels} px)\r\n";
    }

    internal void ProcessRotatingScanFrames(ScannerLookupTable lookupTable, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        if (_state.ScanFrames == null || _state.ScanFrames.Count == 0)
            throw new InvalidOperationException("Load rotating scan folder first.");

        int validFrames = 0;
        int invalidFrames = 0;
        int totalProfilePoints = 0;
        object lockObj = new();  // Lock for thread-safe counter updates

        // Parallel processing of frames - each frame's profile is independent
        Parallel.ForEach(
            _state.ScanFrames,
            new ParallelOptions { CancellationToken = token, MaxDegreeOfParallelism = Environment.ProcessorCount },
            frame =>
            {
                token.ThrowIfCancellationRequested();

                try
                {
                    frame.Profile = BuildProfileFromFrame(
                        frame,
                        lookupTable,
                        token);

                    frame.IsValid = true;
                    frame.StatusMessage = "OK";

                    lock (lockObj)
                    {
                        validFrames++;
                        totalProfilePoints += frame.Profile.Count;  // Accumulate total profile points for valid frames
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    frame.IsValid = false;
                    frame.StatusMessage = ex.Message;
                    lock (lockObj)
                    {
                        invalidFrames++;
                    }
                }
            });

        string lengthConsensusReport = TrimFramesBeyondConsensusLength(lookupTable);

        totalProfilePoints = _state.ScanFrames
            .Where(frame => frame.IsValid && frame.Profile is not null)
            .Sum(frame => frame.Profile.Count);

        _state.DiagnosticsReport +=
            "ROTATING FRAME PROCESSING\r\n" +
            $"Total frames: {_state.ScanFrames.Count}\r\n" +
            $"Valid frames: {validFrames}\r\n" +
            $"Invalid frames: {invalidFrames}\r\n" +
            $"Total profile points: {totalProfilePoints}\r\n" +
            $"Average profile points/frame: {(validFrames > 0 ? totalProfilePoints / (double)validFrames : 0):F1}\r\n" +
            lengthConsensusReport + "\r\n";

        // ------------------------------------------------------------
        // CALIBRATION RANGE USE
        // Reports how much of the scan lies beyond the largest measured
        // calibration reference (values there are extrapolated).
        // ------------------------------------------------------------
        double largestCalibratedRadiusMM = lookupTable.MaximumCalibratedRadiusMM;
        int extrapolatedPoints = 0;
        int measuredPoints = 0;
        double largestMeasuredRadiusMM = 0.0;

        foreach (var processedFrame in _state.ScanFrames)
        {
            if (!processedFrame.IsValid || processedFrame.Profile is null)
                continue;

            foreach (var column in processedFrame.Profile)
            {
                measuredPoints++;

                if (column.RadiusMM > largestMeasuredRadiusMM)
                    largestMeasuredRadiusMM = column.RadiusMM;

                if (column.RadiusMM > largestCalibratedRadiusMM + 0.001)
                    extrapolatedPoints++;
            }
        }

        double extrapolatedPercent = measuredPoints > 0
            ? 100.0 * extrapolatedPoints / measuredPoints
            : 0.0;
        double beyondReferenceMM = Math.Max(
            0.0,
            largestMeasuredRadiusMM - largestCalibratedRadiusMM);

        _state.DiagnosticsReport +=
            "CALIBRATION RANGE USE\r\n" +
            $"Largest Calibrated Radius: {largestCalibratedRadiusMM:F2} mm\r\n" +
            $"Extrapolation Allowance: {_state.Settings.RotatingScan.MaximumCalibrationExtrapolationPixels:F1} px\r\n" +
            $"Largest Measured Radius: {largestMeasuredRadiusMM:F2} mm ({beyondReferenceMM:F2} mm beyond the largest reference)\r\n" +
            $"Points Beyond Largest Reference (extrapolated): {extrapolatedPoints} of {measuredPoints} ({extrapolatedPercent:F1}%)\r\n\r\n";

        // ------------------------------------------------------------
        // ROTATION ANGLES
        // The cameras are not triggered by log rotation, so the angle of
        // each frame is taken from the scan itself: resting frames before
        // and after the revolution are excluded and the frames in between
        // are divided evenly over 360 degrees.
        // ------------------------------------------------------------
        var rotatingScanSettings = _state.Settings.RotatingScan;

        var revolution = rotatingScanSettings.UseMeasuredRevolutionAngles
            ? OptimizationXY.Charger.Services.Acquisition.RevolutionAngleAssigner.Assign(
                _state.ScanFrames,
                rotatingScanSettings)
            : OptimizationXY.Charger.Services.Acquisition.RevolutionAngleAssigner.AssignFixedStep(
                _state.ScanFrames,
                _state.Settings.Acquisition.GetAngularStepDegrees());

        _state.RevolutionAngles = revolution;

        int framesUsedForGeometry = _state.ScanFrames.Count(f => f.IsValid);
        string revolutionVerifiedText = revolution.RevolutionVerified ? "Yes" : "No";
        string closureErrorText = double.IsFinite(revolution.ClosureErrorMM)
            ? $"{revolution.ClosureErrorMM:F2} mm RMS"
            : "n/a";

        // Time for one revolution, when the cameras run at a fixed frame rate.
        var headCamera = _state.Settings.Acquisition.HeadCamera;
        string revolutionTimeText =
            headCamera.Mode == OptimizationXY.Charger.Models.Acquisition.AcquisitionMode.FrameRate &&
            headCamera.AcquisitionFrameRate > 0 &&
            revolution.RevolutionFrames > 0
                ? $" ({revolution.RevolutionFrames / headCamera.AcquisitionFrameRate:F2} s at {headCamera.AcquisitionFrameRate:0.##} frames/s)"
                : string.Empty;

        _state.DiagnosticsReport +=
            "ROTATION ANGLE ASSIGNMENT\r\n" +
            $"Method: {revolution.Method}\r\n" +
            $"Full Revolution Verified: {revolutionVerifiedText}\r\n" +
            $"Revolution Frames: {revolution.FirstRotatingFrame} to {revolution.EndFrameExclusive - 1}\r\n" +
            $"Frames Per Revolution: {revolution.FramesPerRevolution}\r\n" +
            $"Revolution Length: {revolution.RevolutionFrames:0.00} frames{revolutionTimeText}\r\n" +
            $"Angle Step: {revolution.AngleStepDeg:F3} deg\r\n" +
            $"Closure Error: {closureErrorText}\r\n" +
            $"Resting Frames Excluded Before Rotation: {revolution.FramesExcludedBeforeRotation}\r\n" +
            $"Resting Frames Excluded After Rotation: {revolution.FramesExcludedAfterRotation}\r\n" +
            $"Frames Used For Geometry: {framesUsedForGeometry}\r\n" +
            $"{revolution.Message}\r\n\r\n";

        foreach (var bad in _state.ScanFrames.Where(f => !f.IsValid && !f.ExcludedFromRevolution).Take(10))
        {
            _state.DiagnosticsReport +=
                $"Bad frame {bad.FrameIndex} | {Path.GetFileName(bad.ImagePath)} | {bad.StatusMessage}\r\n";  // Log up to 10 bad frames for diagnostics
        }

        _state.DiagnosticsReport += "\r\n";
    }

    // ------------------------------------------------------------
    // BUILD ROTATING SCAN POINT CLOUD
    // Converts the processed rotating scan frames into 3D point clouds.
    //
    // IMPORTANT:
    // _state.PointCloud        = raw measured scanner data
    // _state.DisplayPointCloud = filled / averaged display-only data
    // ------------------------------------------------------------
    /// <summary>
    /// Builds the rotating scan point cloud from the processed scan frames, using the calibration data and application settings.
    /// </summary>
    /// <param name="token">A cancellation token to observe while waiting for the task to complete.</param>
    /// <exception cref="InvalidOperationException">Thrown when the scan frames or calibration data are missing or invalid.</exception>
    public void BuildRotatingScanPointCloud(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        if (_state.ScanFrames == null || _state.ScanFrames.Count == 0)
            throw new InvalidOperationException("Load rotating scan folder first.");  // Ensure that scan frames have been loaded before building the point cloud

        if (_state.Calibration == null)
            throw new InvalidOperationException("Calibration is missing.");

        if (_state.Calibration.LengthPixelsPerMM <= 0)
            throw new InvalidOperationException("LengthPixelsPerMM must be greater than zero.");  // Ensure that the calibration data is valid before proceeding

        int usableFrames = _state.ScanFrames
            .Count(f => f.IsValid && f.Profile != null && f.Profile.Count > 0);  // Count the number of usable frames that have valid profiles

        if (usableFrames == 0)
            throw new InvalidOperationException("No valid processed scan frames.");

        // ------------------------------------------------------------
        // RAW POINT CLOUD
        // This is the real measured scanner data.
        // Use this for all real math:
        // centerline, sweep, bow, ovality, defects, optimizer, final spindle setup.
        // ------------------------------------------------------------
        _state.PointCloud = RunTimed("Build Raw Rotating Scan Point Cloud", () =>
            RotatingScanPointCloudBuilder.BuildFromFrames(
                _state.ScanFrames,
                _state.Calibration.LengthPixelsPerMM,
                _state.Settings,
                fillMissingPoints: false));

        if (_state.PointCloud == null || _state.PointCloud.Count == 0)
            throw new InvalidOperationException("Rotating scan point cloud is empty.");

        // ------------------------------------------------------------
        // DISPLAY POINT CLOUD
        // This is allowed to use estimated / averaged fill points.
        // Use this only for overlays and visual previews.
        // ------------------------------------------------------------
        _state.DisplayPointCloud = RunTimed("Build Display Rotating Scan Point Cloud", () =>
            RotatingScanPointCloudBuilder.BuildFromFrames(
                _state.ScanFrames,
                _state.Calibration.LengthPixelsPerMM,
                _state.Settings,
                fillMissingPoints: true));

        int rawCount = _state.PointCloud.Count;
        int displayCount = _state.DisplayPointCloud?.Count ?? 0;
        int estimatedDisplayPoints = Math.Max(0, displayCount - rawCount);

        double displayFillPercent = rawCount > 0
            ? 100.0 * estimatedDisplayPoints / rawCount
            : 0.0;

        _state.DiagnosticsReport +=
            "ROTATING 3D POINT CLOUD\r\n" +
            $"Usable frames: {usableFrames}\r\n" +
            BuildPointCloudValidationReportInches(_state.PointCloud) +
            "\r\n" +
            "POINT CLOUD DISPLAY FILL\r\n" +
            $"Raw measured points: {rawCount}\r\n" +
            $"Display points after fill: {displayCount}\r\n" +
            $"Estimated display-only points: {estimatedDisplayPoints}\r\n" +
            $"Display fill amount: {displayFillPercent:F1}%\r\n" +
            "Fill Usage: Display overlay only. Final measurements and spindle optimization use raw measured data.\r\n\r\n";
    }

    // ------------------------------------------------------------
    // FIT ROTATING SCAN CENTERLINE
    // ------------------------------------------------------------

    /// <summary>
    /// Fits the centerline of the log from the rotating scan point cloud using PCA (Principal Component Analysis) and local centerline fitting.
    /// </summary>
    /// <param name="token">A cancellation token to observe while waiting for the task to complete.</param>
    /// <exception cref="InvalidOperationException">Thrown when the point cloud is missing or the centerline fit fails.</exception>
    public void FitRotatingScanCenterline(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        if (_state.PointCloud == null || _state.PointCloud.Count == 0)  // Ensure that the point cloud has been built before fitting the centerline
            throw new InvalidOperationException("Build rotating scan point cloud first.");

        _state.Centerline = CenterlineMathService.FitPcaAxis(_state.PointCloud) ?? throw new InvalidOperationException("Centerline fit failed.");  // Fit the centerline using PCA (Principal Component Analysis) on the raw point cloud

        var settings = _state.Settings;  // Retrieve the application settings for geometry and other parameters

        _state.LocalCenterlines = CenterlineMathService.FitLocalCenterlines(
            _state.PointCloud,
            settings.Geometry.LocalCenterlineSliceThicknessMM,
            settings.Geometry.MinPointsPerLocalCenterlineSlice);  // Use the geometry settings for local centerline fitting

        if (_state.LocalCenterlines.Count < 2)
            throw new InvalidOperationException("Not enough reliable rotating-scan centerline slices.");

        var sweepSlices = TrimCenterlineEnds(  // Trim the ends of the local centerline slices to avoid bad data at the log ends, using a trim fraction of 3%
            _state.LocalCenterlines,
            trimFraction: settings.Sweep.EndTrimFraction);

        _state.CenterlineSweep =
            CenterlineSweepAnalyzer.Analyze(  // Analyze the centerline sweep using the trimmed local centerline slices to
                sweepSlices, settings.Sweep);                              // determine tail-head changes, bow, and other sweep characteristics

        // The PCA axis is refined to pass through the fitted slice centres. All
        // shape analysis is built on this centreline. Spindle moves are NOT
        // measured from it: they are offsets from the machine origin (the
        // current spindle axis at X = 0, Y = 0) to the selected axis.
        var rawPcaAxis = _state.Centerline;

        _state.Centerline =
            CenterlineMathService.RefineAxisThroughSliceCenters(
                rawPcaAxis,
                sweepSlices);

        _state.DiagnosticsReport +=
            $"\r\nCENTERLINE ANALYSIS\r\n" +
            $"Raw PCA Origin (point average): ({Inches(rawPcaAxis.OriginX):F3}, {Inches(rawPcaAxis.OriginY):F3}, {Inches(rawPcaAxis.OriginZ):F3}) in\r\n" +
            $"Global PCA Origin: ({Inches(_state.Centerline.OriginX):F3}, {Inches(_state.Centerline.OriginY):F3}, {Inches(_state.Centerline.OriginZ):F3}) in\r\n" +
            $"Global PCA Direction: ({_state.Centerline.DirectionX:F4}, {_state.Centerline.DirectionY:F4}, {_state.Centerline.DirectionZ:F4})\r\n" +
            $"Local Centerline Slices: {_state.LocalCenterlines.Count}\r\n" +
            $"Sweep Slices After End Trim: {sweepSlices.Count}\r\n";

        double minZ = _state.LocalCenterlines.Min(s => s.ZMM);   // Calculate the minimum Z value from the local centerline slices
        double maxZ = _state.LocalCenterlines.Max(s => s.ZMM);  // Calculate the maximum Z value from the local centerline slices
        double avgRadius = _state.LocalCenterlines.Average(s => s.MeanRadiusMM);  // Calculate the average radius from the local centerline slices

        double sweepMinZ = sweepSlices.Min(s => s.ZMM);  // Calculate the minimum Z value from the trimmed sweep slices
        double sweepMaxZ = sweepSlices.Max(s => s.ZMM);  // Calculate the maximum Z value from the trimmed sweep slices

        _state.DiagnosticsReport +=
            $"Local Z Range: {FormatLengthRange(minZ, maxZ)}\r\n" +
            $"Sweep Z Range After Trim: {FormatLengthRange(sweepMinZ, sweepMaxZ)}\r\n" +
            $"Local Avg Radius: {FormatLength(avgRadius)}\r\n";

        // Where the block centreline sits relative to the machine origin
        // (current spindle axis = 0, 0) at each end of the scanned length.
        double centerlineDirectionZ =
            Math.Abs(_state.Centerline.DirectionZ) > 1e-9
                ? _state.Centerline.DirectionZ
                : 1.0;
        double centerlineSlopeX = _state.Centerline.DirectionX / centerlineDirectionZ;
        double centerlineSlopeY = _state.Centerline.DirectionY / centerlineDirectionZ;
        double centerlineTailX = _state.Centerline.OriginX + centerlineSlopeX * (minZ - _state.Centerline.OriginZ);
        double centerlineTailY = _state.Centerline.OriginY + centerlineSlopeY * (minZ - _state.Centerline.OriginZ);
        double centerlineHeadX = _state.Centerline.OriginX + centerlineSlopeX * (maxZ - _state.Centerline.OriginZ);
        double centerlineHeadY = _state.Centerline.OriginY + centerlineSlopeY * (maxZ - _state.Centerline.OriginZ);

        _state.DiagnosticsReport +=
            "Block Centerline Relative To Machine Origin (spindle axis = 0, 0):\r\n" +
            $"- Tail End X: {FormatLength(centerlineTailX)}\r\n" +
            $"- Tail End Y: {FormatLength(centerlineTailY)}\r\n" +
            $"- Head End X: {FormatLength(centerlineHeadX)}\r\n" +
            $"- Head End Y: {FormatLength(centerlineHeadY)}\r\n";

        _state.DiagnosticsReport +=
            $"\r\nCENTERLINE END-TO-END SWEEP\r\n" +
            $"Tail-Head X Change: {FormatLength(_state.CenterlineSweep.XSweepMM)}\r\n" +
            $"Tail-Head Y Change: {FormatLength(_state.CenterlineSweep.YSweepMM)}\r\n" +
            $"End-to-End Change: {FormatLength(_state.CenterlineSweep.TotalSweepMM)}\r\n" +
            $"Bow From Tail-Head Line: {FormatLength(_state.CenterlineSweep.MaxBowSweepMM)}\r\n" +
            $"Average Bow From Line: {FormatLength(_state.CenterlineSweep.AverageBowSweepMM)}\r\n" +
            $"Worst Bow Z: {FormatLength(_state.CenterlineSweep.WorstBowZMM)}\r\n";
    }

    // ------------------------------------------------------------
    // BUILD RADIUS FIELD
    // Converts point cloud into centerline-relative radius/angle data.
    // Output is raw R(z, theta) field for ovality, wane, and rotation.
    // ------------------------------------------------------------

    /// <summary>
    /// Builds the radius field from the raw point cloud and fitted centerline, producing a list of radius field points with Z and angle information.
    /// </summary>
    /// <param name="token">A cancellation token to observe while waiting for the task to complete.</param>
    /// <exception cref="InvalidOperationException">Thrown when the point cloud is missing or the centerline fit fails.</exception>
    public void BuildRadiusField(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        if (_state.PointCloud == null || _state.PointCloud.Count == 0)
            throw new InvalidOperationException("Build point cloud first.");

        if (_state.Centerline == null)
            throw new InvalidOperationException("Fit centerline first.");

        _state.RadiusField = RadiusFieldGenerator.Build(   // Build the radius field from the raw point cloud and fitted centerline,
            _state.PointCloud,                               // producing a list of radius field points with Z and angle information
            _state.Centerline);

        if (_state.RadiusField.Count == 0)
            throw new InvalidOperationException("Radius field build returned no points.");

        // Keep machine-position math in the scanner/current-spindle frame.
        // RadiusField is deliberately PCA-centerline-relative for diagnostics;
        // this second field preserves PointCloud X/Y, where (0,0) is the
        // current spindle axis used by the peel simulator.
        _state.OptimizationRadiusField =
            RotatingScanPointCloudBuilder.BuildMachineFrameRadiusField(
                _state.PointCloud);

        if (_state.OptimizationRadiusField.Count == 0)
        {
            throw new InvalidOperationException(
                "Machine-frame optimization radius field returned no points.");
        }

        double minRadius = _state.RadiusField.Min(p => p.RadiusMM);  // Calculate the minimum radius from the radius field points
        double maxRadius = _state.RadiusField.Max(p => p.RadiusMM);  // Calculate the maximum radius from the radius field points
        double avgRadius = _state.RadiusField.Average(p => p.RadiusMM);  // Calculate the average radius from the radius field points

        double avgDiameter = avgRadius * 2.0;  // Calculate the average diameter from the average radius
        double minDiameter = minRadius * 2.0;  // Calculate the minimum diameter from the minimum radius
        double maxDiameter = maxRadius * 2.0;  // Calculate the maximum diameter from the maximum radius

        _state.Model.MinDiameterMM = minDiameter;  // Store the minimum diameter in the model for later use
        _state.Model.AverageDiameterMM = avgDiameter;  // Store the average diameter in the model for later use
        _state.Model.MaxDiameterMM = maxDiameter;  // Store the maximum diameter in the model for later use

        _state.DiagnosticsReport +=
            $"\r\nRADIUS FIELD\r\n" +
            $"Points: {_state.RadiusField.Count}\r\n" +
            $"Z Range: {FormatLengthRange(_state.RadiusField.Min(p => p.Z), _state.RadiusField.Max(p => p.Z))}\r\n" +
            $"Angle Range: {_state.RadiusField.Min(p => p.AngleDeg):F2} to {_state.RadiusField.Max(p => p.AngleDeg):F2} deg\r\n" +
            $"Radius Range: {FormatLengthRange(minRadius, maxRadius)}\r\n" +
            $"Avg Radius: {FormatLength(avgRadius)}\r\n" +
            $"Min Diameter: {FormatLength(minDiameter)}\r\n" +
            $"Avg Diameter: {FormatLength(avgDiameter)}\r\n" +
            $"Max Diameter: {FormatLength(maxDiameter)}\r\n";

        _state.DiagnosticsReport +=
            "Coordinate Frames:\r\n" +
            "- Diagnostic radius field: fitted PCA-centerline-relative.\r\n" +
            "- Optimization radius field: scanner/current-spindle-relative; current axis = (0,0).\r\n" +
            $"- Optimization points: {_state.OptimizationRadiusField.Count}\r\n";

    }

    // ------------------------------------------------------------
    // BIN ROTATING SCAN RADIUS FIELD
    // Bins the R(z, theta) field from the rotating scan point cloud.
    // ------------------------------------------------------------

    /// <summary>
    /// Bins the radius field points into discrete Z and angle bins based on the application settings, producing a binned radius field for further analysis.
    /// </summary>
    /// <param name="token">A cancellation token to observe while waiting for the task to complete.</param>
    /// <exception cref="InvalidOperationException">Thrown when the radius field is missing or empty.</exception>
    public void BinRotatingScanRadiusField(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        if (_state.RadiusField == null || _state.RadiusField.Count == 0)
            throw new InvalidOperationException("Build radius field first.");

        var settings = _state.Settings;  // Retrieve the application settings for geometry and other parameters. 

        _state.BinnedRadiusField = RadiusFieldBinner.Bin(
            _state.RadiusField,
            zBinSizeMM: settings.Geometry.RadiusFieldZBinSizeMM,  // Use the Z bin size from the settings for radius field binning
            angleBinSizeDeg: settings.Geometry.RadiusFieldAngleBinSizeDeg,  // Use the angle bin size from the settings for radius field binning
            minPointsPerBin: settings.Geometry.MinPointsPerRadiusBin);  // Use the minimum points per bin from the settings

        _state.OptimizationBinnedRadiusField = RadiusFieldBinner.Bin(
            _state.OptimizationRadiusField,
            zBinSizeMM: settings.Geometry.RadiusFieldZBinSizeMM,
            angleBinSizeDeg: settings.Geometry.RadiusFieldAngleBinSizeDeg,
            minPointsPerBin: settings.Geometry.MinPointsPerRadiusBin,
            ignoreLowestReadings: settings.Geometry.OptimizationCellIgnoreLowestReadings);

        _state.PointCloudOverlay?.Dispose();
        _state.PointCloudOverlay =
RotatingRadiusSectionOverlayBuilder.BuildTailMiddleHeadFromBinnedField(  // Build a visual overlay of the tail, middle, and head sections of the log from the binned radius field
    _state.BinnedRadiusField,
    centerline: _state.Centerline);

        // Single head-end view for the overlay box on the main form. Drawing must
        // never fail a scan, so a problem here only loses the picture.
        _state.LogHeadEndViewOverlay?.Dispose();
        _state.LogHeadEndViewOverlay = null;

        try
        {
            _state.LogHeadEndViewOverlay = LogHeadEndViewOverlayBuilder.Build(
                _state.BinnedRadiusField,
                _state.Centerline);
        }
        catch (Exception)
        {
            _state.LogHeadEndViewOverlay = null;
        }

        if (_state.BinnedRadiusField.Count == 0)
            throw new InvalidOperationException("Binned radius field returned no points.");

        if (_state.OptimizationBinnedRadiusField.Count == 0)
        {
            throw new InvalidOperationException(
                "Binned machine-frame optimization field returned no points.");
        }

        var optimizationZBins = _state.OptimizationBinnedRadiusField
            .GroupBy(point => point.ZBinMM)
            .ToList();

        _state.DiagnosticsReport +=
            "\r\nMACHINE-FRAME OPTIMIZATION FIELD\r\n" +
            $"Bins: {_state.OptimizationBinnedRadiusField.Count}\r\n" +
            $"Z Slices: {optimizationZBins.Count}\r\n" +
            $"Distinct Angle Bins: {_state.OptimizationBinnedRadiusField.Select(point => point.AngleBinDeg).Distinct().Count()}\r\n" +
            $"Minimum Angle Bins Per Slice: {optimizationZBins.Min(group => group.Count())}\r\n" +
            $"Average Angle Bins Per Slice: {optimizationZBins.Average(group => group.Count()):F1}\r\n" +
            $"Maximum Angle Bins Per Slice: {optimizationZBins.Max(group => group.Count())}\r\n";

        var zBins = _state.BinnedRadiusField  // Group the binned radius field points by their Z bin values to analyze coverage and distribution
            .GroupBy(p => p.ZBinMM)  // Group the binned radius field points by their Z bin values
            .OrderBy(g => g.Key) // Order the groups by their Z bin values for analysis
            .ToList();  // Convert the grouped result to a list for further processing

        double avgAngleBinsPerSlice = zBins.Average(g => g.Count());  // Calculate the average number of angle bins per Z slice by averaging the counts of each group
        double minAngleBinsPerSlice = zBins.Min(g => g.Count());  // Calculate the minimum number of angle bins per Z slice by finding the minimum count among the groups
        double maxAngleBinsPerSlice = zBins.Max(g => g.Count());  // Calculate the maximum number of angle bins per Z slice by finding the maximum count among the groups

        _state.DiagnosticsReport +=
            $"\r\nRADIUS FIELD COVERAGE\r\n" +
            $"Z Slice Count: {zBins.Count}\r\n" +
            $"Avg Angle Bins Per Slice: {avgAngleBinsPerSlice:F1}\r\n" +
            $"Min Angle Bins Per Slice: {minAngleBinsPerSlice:F0}\r\n" +
            $"Max Angle Bins Per Slice: {maxAngleBinsPerSlice:F0}\r\n";

        _state.DiagnosticsReport +=
            $"\r\nBINNED RADIUS FIELD\r\n" +
            $"Bins: {_state.BinnedRadiusField.Count}\r\n" +
            $"Z Bins: {_state.BinnedRadiusField.Select(p => p.ZBinMM).Distinct().Count()}\r\n" +
            $"Angle Bins: {_state.BinnedRadiusField.Select(p => p.AngleBinDeg).Distinct().Count()}\r\n" +
            $"Z Range: {FormatLengthRange(_state.BinnedRadiusField.Min(p => p.ZBinMM), _state.BinnedRadiusField.Max(p => p.ZBinMM))}\r\n" +
            $"Angle Range: {_state.BinnedRadiusField.Min(p => p.AngleBinDeg):F2} to {_state.BinnedRadiusField.Max(p => p.AngleBinDeg):F2} deg\r\n" +
            $"Mean Radius: {FormatLength(_state.BinnedRadiusField.Average(p => p.MeanRadiusMM))}\r\n" +
            $"Median Radius: {FormatLength(_state.BinnedRadiusField.Average(p => p.MedianRadiusMM))}\r\n" +
            $"Avg StdDev Per Bin: {FormatLength(_state.BinnedRadiusField.Average(p => p.StdDevRadiusMM), 4)}\r\n";
    }

    // ------------------------------------------------------------
    // ANALYZE OVALITY
    // Analyzes true R(z, theta) field for ovality patterns.
    // Supports folders with fewer than 36 rotating images.
    // ------------------------------------------------------------
    /// <summary>
    /// Analyzes the binned radius field for ovality patterns.
    /// </summary>
    /// <param name="token">A cancellation token to observe while waiting for the task to complete.</param>
    /// <exception cref="InvalidOperationException">Thrown when the binned radius field is missing or empty.</exception>
    public void AnalyzeOvality(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        if (_state.BinnedRadiusField == null || _state.BinnedRadiusField.Count == 0)
            throw new InvalidOperationException("Bin radius field first.");

        // Only frames used for geometry count. Resting frames excluded from the
        // revolution do not add angular coverage.
        int frameCount = _state.ScanFrames?.Count(f => f.IsValid) ?? 0;

        int minAngleBins =
            frameCount >= 30 ? 30 :  // If there are 30 or more frames, require at least 30 angle bins per slice for ovality analysis
            frameCount >= 20 ? 18 :  // If there are 20 or more frames, require at least 18 angle bins per slice for ovality analysis
            frameCount >= 12 ? 10 :  // If there are 12 or more frames, require at least 10 angle bins per slice for ovality analysis
            frameCount >= 8 ? 6 :  // If there are 8 or more frames, require at least 6 angle bins per slice for ovality analysis
            4;  // If there are fewer than 8 frames, require at least 4 angle bins per slice for ovality analysis

        // Never demand more angular coverage for this report than the operator
        // scan-quality gate demands for a usable scan.
        double angleBinSizeDeg = _state.Settings.Geometry.RadiusFieldAngleBinSizeDeg;
        if (angleBinSizeDeg > 0)
        {
            int expectedAngleBins = Math.Max(1, (int)Math.Round(360.0 / angleBinSizeDeg));
            int operatorMinimumBins = Math.Max(
                3,
                (int)Math.Ceiling(
                    expectedAngleBins *
                    _state.Settings.OperatorView.MinimumAngularCoveragePercentPerSlice / 100.0));

            minAngleBins = Math.Min(minAngleBins, operatorMinimumBins);
        }

        // Roundness is a report, not a positioning input. If no slice reaches
        // the preferred coverage, analyse with what is available instead of
        // failing the whole scan.
        try
        {
            _state.Model.OvalityAnalysis =
                OvalityAnalyzer.Analyze(
                    _state.BinnedRadiusField,
                    minAngleBinsPerSlice: minAngleBins);
        }
        catch (InvalidOperationException) when (minAngleBins > 6)
        {
            minAngleBins = Math.Max(6, minAngleBins / 2);

            _state.Model.OvalityAnalysis =
                OvalityAnalyzer.Analyze(
                    _state.BinnedRadiusField,
                    minAngleBinsPerSlice: minAngleBins);
        }

        var o = _state.Model.OvalityAnalysis;  // Store the result of the ovality analysis in a local variable for easier access

        _state.DiagnosticsReport +=
            $"\r\nLOG ROUNDNESS / OVALITY CHECK\r\n" +
            $"Worst Location Along Log: {FormatLength(o.WorstZMM)} from tail\r\n" +
            $"Average Radius At That Slice: {FormatLength(o.MeanRadiusMM)}\r\n" +
            $"Widest Diameter: {FormatLength(o.MajorDiameterMM)}\r\n" +
            $"Narrowest Diameter: {FormatLength(o.MinorDiameterMM)}\r\n" +
            $"Out-of-Round Amount: {FormatLength(o.OvalityMM)}\r\n" +
            $"Out-of-Round Percent: {o.OvalityPercent:F2} %\r\n" +
            $"Oval Direction: {o.OrientationDeg:F2} deg\r\n" +
            $"Reliable Slices Checked: {o.SliceCount}\r\n" +
            $"Minimum Angle Bins Needed Per Slice: {minAngleBins}\r\n" +
            "\r\n" +
            BuildOvalityMeaning(o.OvalityMM, o.OvalityPercent);  // Append the meaning of the ovality results to the diagnostics report for user interpretation
    }

    // ------------------------------------------------------------
    // ANALYZE RADIUS DEFECTS
    // ------------------------------------------------------------

    /// <summary>
    /// Analyzes the binned radius field for radius defects based on the defect thresholds specified in the application settings.
    /// </summary>
    /// <param name="token">A cancellation token to observe while waiting for the task to complete.</param>
    /// <exception cref="InvalidOperationException">Thrown when the binned radius field is missing or empty.</exception>
    public void AnalyzeRadiusDefects(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        if (_state.BinnedRadiusField == null ||
            _state.BinnedRadiusField.Count == 0)  // Ensure that the binned radius field has been built before analyzing radius defects
        {
            throw new InvalidOperationException(
                "Bin radius field first.");
        }

        var settings = _state.Settings;  // Retrieve the application settings for defect thresholds and other parameters

        _state.RadiusDefectMap = RadiusDefectMapBuilder.Build(  // Build the radius defect map from the binned radius field using the specified defect thresholds from the settings
            _state.BinnedRadiusField,
            lowThresholdMM: settings.Defects.LowRadiusThresholdMM,  // Use the low radius threshold from the settings for defect analysis
            severeLowThresholdMM: settings.Defects.SevereLowRadiusThresholdMM,  //  Use the severe low radius threshold from the settings for defect analysis
            highThresholdMM: settings.Defects.HighRadiusThresholdMM);  // Use the high radius threshold from the settings for defect analysis

        var map = _state.RadiusDefectMap;  // Store the result of the radius defect map build in a local variable for easier access

        _state.RadiusDefectHeatmap?.Dispose();
        _state.RadiusDefectHeatmap = RadiusDefectHeatmapBuilder.Build(_state.RadiusDefectMap);  // Build a heatmap visualization of the radius defect map for visual inspection and analysis

        int total = map.Points.Count;  // Calculate the total number of points in the radius defect map for percentage calculations

        double goodPercent = total > 0 ? map.GoodCount * 100.0 / total : 0.0;  // Calculate the percentage of good surface cells in the radius defect map for reporting
        double lowPercent = total > 0 ? map.LowCount * 100.0 / total : 0.0;  // Calculate the percentage of low wood / wane-risk surface cells in the radius defect map for reporting
        double severeLowPercent = total > 0 ? map.SevereLowCount * 100.0 / total : 0.0; // Calculate the percentage of severe low wood surface cells in the radius defect map for reporting


        _state.DiagnosticsReport +=
            $"\r\nSURFACE CONDITION MAP\r\n" +
            $"Surface Cells Checked: {total}\r\n" +
            $"Normal Surface: {map.GoodCount} cells ({goodPercent:F1} %)\r\n" +
            $"Low Wood / Wane-Risk Surface: {map.LowCount} cells ({lowPercent:F1} %)\r\n" +
            $"Severe Low Wood: {map.SevereLowCount} cells ({severeLowPercent:F1} %)\r\n" +
            $"Deepest Low-Wood Area: {FormatDepthBelowSurface(map.MinErrorMM)}\r\n" +
            "\r\n" +

            BuildSurfaceConditionMeaning(goodPercent, lowPercent, severeLowPercent); // Append the meaning of the surface condition results to the diagnostics report for user interpretation

    }

    /// <summary>
    /// Analyzes the radius defect patches from the radius defect map, grouping connected low-wood areas into individual patches for further analysis and reporting.
    /// </summary>
    /// <param name="token">A cancellation token to observe while waiting for the task to complete.</param>
    /// <exception cref="InvalidOperationException">Thrown when the radius defect map is missing or empty.</exception>

    public void AnalyzeDefectPatches(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        if (_state.RadiusDefectMap == null ||
            _state.RadiusDefectMap.Points.Count == 0)  // Ensure that the radius defect map has been built before analyzing defect patches
        {
            throw new InvalidOperationException(
                "Build radius defect map first.");
        }

        _state.RadiusDefectPatches = RadiusDefectPatchBuilder.Build(_state.RadiusDefectMap); // Build the defect patches from the radius defect map, grouping connected low-wood areas into
                                                                                             // individual patches for further analysis

        _state.DefectPatchAnalysis = DefectPatchAnalyzer.Analyze(_state.RadiusDefectPatches);  // Analyze the defect patches to compute statistics such
                                                                                               // as patch count, total defect cells, average patch size, worst depth, and severity scores for reporting and optimization purposes

        var analysis = _state.DefectPatchAnalysis; // Store the result of the defect patch analysis in a local variable for easier access

        _state.DiagnosticsReport +=
                $"\r\nLOW WOOD PATCH ANALYSIS\r\n" +
                 BuildPatchMeaning() +
                 "\r\n" +
                $"Low-Wood Patch Count: {analysis.PatchCount}\r\n" +
                $"Total Low-Wood Cells: {analysis.TotalDefectCells}\r\n" +
                $"Average Patch Size: {analysis.AveragePatchSizeCells:F2} cells\r\n" +
                $"Deepest Low-Wood Area: {FormatDepthBelowSurface(analysis.WorstDepthMM)}\r\n" +
                $"Average Low-Wood Depth: {FormatDepthBelowSurface(analysis.AverageDepthMM)}\r\n" +
                $"Worst Patch Risk Rating: {GetPatchRiskRating(analysis.WorstSeverityScore)}\r\n" +
                $"Internal Severity Score: {analysis.WorstSeverityScore:F2}\r\n" +
                "Severity Meaning: Internal ranking only. Higher means a patch is larger, deeper, or more likely to affect veneer recovery.\r\n";

        if (analysis.LargestPatch != null)
        {
            var p = analysis.LargestPatch;  // Store the largest low-wood patch from the analysis in a local variable for easier access

            _state.DiagnosticsReport +=
                $"\r\nLargest Low-Wood Patch\r\n" +
                $"Cells: {p.CellCount}\r\n" +
                $"Internal Severity Score: {p.SeverityScore:F2}\r\n" +
                $"Location Along Log: {FormatLength(p.StartZMM)} to {FormatLength(p.EndZMM)} from tail\r\n" +
                $"Angle: {p.StartAngleDeg:F2} to {p.EndAngleDeg:F2} deg\r\n" +
                $"Average Depth: {FormatDepthBelowSurface(p.AverageErrorMM)}\r\n" +
                $"Worst Depth: {FormatDepthBelowSurface(p.WorstErrorMM)}\r\n";

        }

        if (analysis.WorstPatch != null &&
            analysis.WorstPatch != analysis.LargestPatch)  // Only report the worst patch if it is different from the largest patch to avoid duplicate reporting
        {
            var p = analysis.WorstPatch;  // Store the worst low-wood patch from the analysis in a local variable for easier access

            _state.DiagnosticsReport +=
                $"\r\nWorst Low-Wood Patch\r\n" +
                $"Cells: {p.CellCount}\r\n" +
                $"Internal Severity Score: {p.SeverityScore:F2}\r\n" +
                $"Location Along Log: {FormatLength(p.StartZMM)} to {FormatLength(p.EndZMM)} from tail\r\n" +
                $"Angle: {p.StartAngleDeg:F2} to {p.EndAngleDeg:F2} deg\r\n" +
                $"Average Depth: {FormatDepthBelowSurface(p.AverageErrorMM)}\r\n" +
                $"Worst Depth: {FormatDepthBelowSurface(p.WorstErrorMM)}\r\n";

        }
    }

    // ------------------------------------------------------------
    // OPTIMIZE SPINDLE AXIS
    // Finds the final fixed spindle setup.
    // Only tail/head spindle X/Y moves are machine actions.
    // No charger center movement and no log rotation are used.
    // ------------------------------------------------------------

    /// <summary>
    /// Optimizes the spindle axis based on the binned radius field, defect patches, and adaptive
    /// scoring profile, producing the final spindle setup for the log.
    /// </summary>
    /// <param name="token">A cancellation token to observe while waiting for the task to complete.</param>
    /// <exception cref="InvalidOperationException">Thrown when the point cloud, binned radius field, or radius defect patches are missing or empty.</exception>
    public void OptimizeSpindleAxis(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        if (_state.PointCloud == null || _state.PointCloud.Count == 0)  // Ensure that the point cloud has been built before optimizing the spindle axis
            throw new InvalidOperationException("Build point cloud first.");

        if (_state.OptimizationBinnedRadiusField == null ||
            _state.OptimizationBinnedRadiusField.Count == 0)
        {
            throw new InvalidOperationException(
                "Build the machine-frame optimization field first.");
        }

        if (_state.RadiusDefectPatches == null)
            throw new InvalidOperationException("Analyze defect patches first.");  // Ensure that the defect patches have been analyzed before optimizing the spindle axis

        _state.DiagnosticsReport += "\r\n=== SPINDLE AXIS OPTIMIZATION ===\r\n";

        int surfaceTotal = _state.RadiusDefectMap?.Points.Count ?? 0;  // Get the total number of surface cells in the radius defect map for percentage calculations
        double lowWoodPercent = surfaceTotal > 0 ? _state.RadiusDefectMap!.LowCount * 100.0 / surfaceTotal : 0.0;  // Calculate the percentage of low wood / wane-risk surface cells in the radius defect map for reporting
        double severeLowWoodPercent = surfaceTotal > 0 ? _state.RadiusDefectMap!.SevereLowCount * 100.0 / surfaceTotal : 0.0;  // Calculate the percentage of severe low wood surface cells in the radius defect map for reporting
        double highSpotPercent = surfaceTotal > 0 ? _state.RadiusDefectMap!.HighCount * 100.0 / surfaceTotal : 0.0; // Calculate the percentage of high surface cells in the radius defect map for reporting
        double deepestLowWoodMM = _state.RadiusDefectMap?.MinErrorMM ?? 0.0;  // Get the deepest low-wood area in millimeters from the radius defect map for reporting
        double outOfRoundMM = _state.Model.OvalityAnalysis?.OvalityMM ?? 0.0;  // Get the out-of-round amount in millimeters from the ovality analysis for reporting
        double centerlineSweepMM = _state.CenterlineSweep?.TotalSweepMM ?? 0.0;  // Get the total centerline sweep in millimeters from the centerline sweep analysis for reporting

        //------------------------------------------------------------
        // Build the adaptive scoring profile based on the current defect and geometry metrics, which will be used for optimization and reporting
        //------------------------------------------------------------
        var adaptiveDiagnostics = BuildAdaptiveLogDiagnostics(
                lowWoodPercent,
                severeLowWoodPercent,
                highSpotPercent,
                deepestLowWoodMM,
                outOfRoundMM,
                centerlineSweepMM);

        _state.AdaptiveScoringProfile =
            AdaptiveScoringProfileBuilder.Build(
                adaptiveDiagnostics,
                _state.Settings);

        _state.DiagnosticsReport +=
            AdaptiveScoringProfileBuilder.BuildReport(_state.AdaptiveScoringProfile);

        if (_state.Settings.AdaptiveScoring.Enabled &&
            _state.Settings.AdaptiveScoring.RebuildDefectMapWithAdaptiveThresholds)
        {
            RebuildRadiusDefectsWithAdaptiveProfile(_state.AdaptiveScoringProfile);

            // Recompute surface numbers after adaptive defect thresholds are applied.
            surfaceTotal = _state.RadiusDefectMap?.Points.Count ?? 0;
            lowWoodPercent = surfaceTotal > 0 ? _state.RadiusDefectMap!.LowCount * 100.0 / surfaceTotal : 0.0;
            severeLowWoodPercent = surfaceTotal > 0 ? _state.RadiusDefectMap!.SevereLowCount * 100.0 / surfaceTotal : 0.0;
            highSpotPercent = surfaceTotal > 0 ? _state.RadiusDefectMap!.HighCount * 100.0 / surfaceTotal : 0.0;
            deepestLowWoodMM = _state.RadiusDefectMap?.MinErrorMM ?? 0.0;
        }

        // ------------------------------------------------------------
        // Active settings for THIS log.
        // These are adaptive when adaptive scoring is enabled.
        // They fall back to normal app settings if the profile is missing.
        // ------------------------------------------------------------
        var activeScoring =
            _state.AdaptiveScoringProfile?.Scoring ?? _state.Settings.Scoring;

        var activeDefects =
            _state.AdaptiveScoringProfile?.Defects ?? _state.Settings.Defects;

        var activeOptimization =
            _state.AdaptiveScoringProfile?.Optimization ?? _state.Settings.Optimization;

        var activeWane =
            _state.AdaptiveScoringProfile?.Wane ?? _state.Settings.Wane;

        var activeUsableRadius = _state.Settings.UsableRadius;

        // ------------------------------------------------------------
        // Baseline measurement: current spindle axis, no movement.
        // This wane value is a real measured average radius spread.
        // ------------------------------------------------------------
        double baselineAxisWaneScore =
SpindleAxisWaneOptimizer.ComputeAxisAdjustedWaneScore(
    _state.OptimizationBinnedRadiusField,
    tailXMM: 0.0,
    tailYMM: 0.0,
    headXMM: 0.0,
    headYMM: 0.0,
    activeWane.MinAngleBinsPerWaneSlice);

        double baselineDefectPenalty =
            PatchAwareWanePenalty.ComputeSpindleAxisAdjusted(
                _state.RadiusDefectPatches,
                tailXMM: 0.0,
                tailYMM: 0.0,
                headXMM: 0.0,
                headYMM: 0.0,
                depthWeight: activeDefects.PatchDepthWeight,
                cellWeight: activeDefects.PatchCellWeight,
                severityWeight: activeDefects.PatchSeverityWeight,
                spindleReliefWeight: activeDefects.SpindleReliefWeight);

        double baselineInternalSearchScore =
            baselineAxisWaneScore + baselineDefectPenalty;  // This is the internal search score used by the optimizer to compare candidates.
                                                            // It combines the baseline axis wane score and the baseline defect penalty for a comprehensive evaluation of the current spindle setup.

        // Candidate-aware low-wood reference.
        // Baseline is always 100% remaining risk. Optimized candidates are compared against this.
        // This keeps the uniform score from ignoring the actual low-wood relief created by the proposed spindle position.
        double baselineAdjustedLowWoodRiskRemainingPercent =
            baselineDefectPenalty > 0.000001 ? 100.0 : 0.0;

        // Build the baseline uniform optimization score for the current spindle setup with no movement, using the computed metrics for reporting and comparison
        var baselineUniformScore =
            BuildUniformOptimizationScore(
                title: "Current Charger Setup",
                averageSliceRadiusSpreadMM: baselineAxisWaneScore,
                adjustedLowWoodRiskRemainingPercent: baselineAdjustedLowWoodRiskRemainingPercent,
                deepestLowWoodMM: deepestLowWoodMM,   // or whatever variable holds this value
                maxSpindleMovementMM: 0.0,
                outOfRoundMM: outOfRoundMM,
                centerlineSweepMM: centerlineSweepMM,
                scoring: activeScoring);

        // ------------------------------------------------------------
        // Report baseline metrics before searching for optimized spindle-axis setup.
        // ------------------------------------------------------------
        _state.DiagnosticsReport +=
            "\r\nBASELINE SETUP\r\n" +
            $"Average Slice Radius Spread: {FormatLength(baselineAxisWaneScore)}\r\n" +
            $"Internal Defect Penalty: {baselineDefectPenalty:F3}\r\n" +
            $"Internal Search Score: {baselineInternalSearchScore:F3}\r\n" +
            "Internal Search Score Meaning: Used only to search candidates. Do not compare this number across logs.\r\n" +
            "\r\nSearching for best fixed spindle-axis position...\r\n";

        _state.DiagnosticsReport +=
            BuildUniformScoreReport(
                "BASELINE UNIFORM RISK SCORE",
                baselineUniformScore);

        // ------------------------------------------------------------
        // Search optimized spindle-axis setup.
        // ------------------------------------------------------------
        var finalSetup =
ChargerOptimizer.FindBestSetupCoarseToFine(
    _state.OptimizationBinnedRadiusField,
    _state.RadiusDefectPatches,
    baselineInternalSearchScore,
    activeOptimization,
    activeDefects,
    activeWane,
    activeScoring,

    _state.Settings.UsableRadius) ?? throw new InvalidOperationException("Optimizer did not return a final setup.");
        var move = finalSetup.SpindleMovement ?? throw new InvalidOperationException("Final setup did not return spindle movement.");

        // Compute the maximum spindle movement in millimeters for reporting and comparison against machine limits
        double maxOptimizedSpindleMovementMM =
            MaxSpindleMovementMM(
                move.TailMoveXMM,
                move.TailMoveYMM,
                move.HeadMoveXMM,
                move.HeadMoveYMM);

        // Validate the optimized spindle movement against machine limits and generate any warnings for reporting
        var machineWarnings = ValidateMachineMovement(
                move.TailMoveXMM,
                move.TailMoveYMM,
                move.HeadMoveXMM,
                move.HeadMoveYMM,
                _state.Settings.MachineLimits);

        // Determine if the machine movement is allowed based on the generated warnings, where any warning starting with "BLOCKED" indicates a disallowed movement
        bool machineMoveAllowed = machineWarnings.All(w => !w.StartsWith("BLOCKED", StringComparison.CurrentCulture));

        // Compute the adjusted low-wood risk remaining percent for the optimized spindle setup, comparing the final defect
        // penalty against the baseline defect penalty for reporting and comparison
        double optimizedAdjustedLowWoodRiskRemainingPercent =
            baselineDefectPenalty > 0.000001
                ? Math.Max(0.0, finalSetup.DefectPenalty / baselineDefectPenalty * 100.0)
                : 0.0;

        // Build the optimized uniform optimization score for the recommended spindle setup, using the computed metrics for reporting and comparison
        var optimizedUniformScore =
            BuildUniformOptimizationScore(
                title: "Optimized Spindle-Axis Setup",
                averageSliceRadiusSpreadMM: finalSetup.WaneScore,
                adjustedLowWoodRiskRemainingPercent: optimizedAdjustedLowWoodRiskRemainingPercent,
                deepestLowWoodMM: deepestLowWoodMM,
                maxSpindleMovementMM: maxOptimizedSpindleMovementMM,
                outOfRoundMM: outOfRoundMM,
                centerlineSweepMM: centerlineSweepMM,
                scoring: activeScoring);

        // ------------------------------------------------------------
        // Compute improvement percentages for internal search score and uniform risk score for reporting and comparison
        // ------------------------------------------------------------
        double internalImprovementPercent =
            ComputeImprovementPercent(
                baselineInternalSearchScore,
                finalSetup.FinalScore);

        // ------------------------------------------------------------
        // Compute improvement percentage for uniform risk score for reporting and comparison
        // ------------------------------------------------------------
        double uniformImprovementPercent =
            ComputeImprovementPercent(
                baselineUniformScore.FinalScore,
                optimizedUniformScore.FinalScore);

        // ------------------------------------------------------------
        // Determine if the optimized spindle setup is recommended based on the computed improvement percentage and machine movement allowance
        // ------------------------------------------------------------
        var settings = _state.Settings;

        //------------------------------------------------------------
        // Store the final uniform improvement percentage and recommendation status in the final setup for reporting and comparison
        //------------------------------------------------------------
        finalSetup.ImprovementPercent = uniformImprovementPercent;
        finalSetup.IsRecommended =
            uniformImprovementPercent >= activeOptimization.MinimumImprovementPercent;

        // If the machine movement is not allowed, override the recommendation status to false for reporting and comparison
        if (!machineMoveAllowed)
        {
            finalSetup.IsRecommended = false;
        }

        // ------------------------------------------------------------
        // Build the final spindle recommendation object for reporting and comparison, including all relevant metrics and warnings
        // ------------------------------------------------------------
        _state.FinalSpindleRecommendation =
BuildFinalSpindleRecommendation(
    finalSetup,
    machineWarnings,
    machineMoveAllowed,
    baselineUniformScore.FinalScore,
    baselineUniformScore.Rating,
    optimizedUniformScore.FinalScore,
    optimizedUniformScore.Rating,
    uniformImprovementPercent,
    baselineAxisWaneScore,
    baselineInternalSearchScore,
    baselineDefectPenalty,
    maxOptimizedSpindleMovementMM);

        // ------------------------------------------------------------
        // Store the final spindle recommendation in the state for later use and reporting
        // ------------------------------------------------------------
        var final = _state.FinalSpindleRecommendation
            ?? throw new InvalidOperationException("Final spindle recommendation was not stored.");

        // Store the operator-facing score on the model.
        _state.Model.WaneScore = final.RiskScore;

        _state.DiagnosticsReport +=
            BuildUniformScoreReport(
                "OPTIMIZED UNIFORM RISK SCORE",
                optimizedUniformScore);

        // ------------------------------------------------------------
        // Tradeoff check.
        // Negative delta = improved. Positive delta = worse.
        // ------------------------------------------------------------
        double waneDelta =
            finalSetup.WaneScore - baselineAxisWaneScore;  // This is the average slice radius spread change, where a negative value
                                                           // indicates improvement and a positive value indicates worsening.

        double defectDelta =
            finalSetup.DefectPenalty - baselineDefectPenalty;   // This is the internal defect penalty change, not an operator-facing score.

        double internalSearchDelta =
            finalSetup.FinalScore - baselineInternalSearchScore; // This is the internal search score change, where a negative value indicates
                                                                 // improvement and a positive value indicates worsening.

        double uniformScoreDelta =
            optimizedUniformScore.FinalScore - baselineUniformScore.FinalScore; // This is the uniform risk score change, where a negative value
                                                                                // indicates improvement and a positive value indicates worsening.

        _state.DiagnosticsReport +=
            "\r\nOPTIMIZER TRADEOFF CHECK\r\n" +
            $"Average Slice Radius Spread Change: {FormatSignedLength(waneDelta)} " +
            $"{(waneDelta <= 0 ? "(improved)" : "(worse)")}\r\n" +
            $"Internal Low-Wood Penalty Change: {defectDelta:F3} " +
            $"{(defectDelta <= 0 ? "(improved)" : "(worse)")}\r\n" +
            $"Internal Search Score Change: {internalSearchDelta:F3} " +
            $"{(internalSearchDelta <= 0 ? "(improved)" : "(worse)")}\r\n" +
            $"Uniform Risk Score Change: {uniformScoreDelta:F1} points " +
            $"{(uniformScoreDelta <= 0 ? "(improved)" : "(worse)")}\r\n" +
            $"Internal Search Score Reduction: {internalImprovementPercent:F1}%\r\n" +
            $"Uniform Risk Score Reduction vs Baseline: {uniformImprovementPercent:F1}%\r\n" +
            "\r\n" +

            BuildOptimizerTradeoffMeaning(waneDelta, defectDelta, internalSearchDelta); // Append the meaning of the optimizer tradeoff results
                                                                                        // to the diagnostics report for user interpretation

        // ------------------------------------------------------------
        // Machine movement limit check.
        // ------------------------------------------------------------
        if (final.Warnings.Count > 0)
        {
            _state.DiagnosticsReport += "\r\nMACHINE MOVEMENT LIMIT CHECK\r\n";

            foreach (var warning in final.Warnings)
            {
                _state.DiagnosticsReport += warning + "\r\n";
            }
        }

        // ------------------------------------------------------------
        // Final charger setup report.
        // ------------------------------------------------------------
        _state.DiagnosticsReport +=
            "\r\n=== FINAL CHARGER SETUP ===\r\n" +
            $"Recommended action: {final.RecommendedAction}\r\n" +
            $"Reason: {final.Reason}\r\n" +
            $"Uniform Risk Score: {final.RiskScore:F1} / 100 ({final.RiskRating})\r\n" +
            $"Baseline Uniform Risk Score: {final.BaselineRiskScore:F1} / 100 ({final.BaselineRiskRating})\r\n" +
            $"Uniform Risk Improvement: {final.RiskScoreReductionPercent:F1}%\r\n" +
            $"Average Slice Radius Spread: {FormatLength(final.OptimizedWaneScoreMM)}\r\n" +
            $"Max Spindle Movement: {FormatLength(final.MaxSpindleMovementMM)}\r\n" +
            $"Internal Defect Penalty: {final.OptimizedDefectPenalty:F3}\r\n" +
            $"Internal Movement Penalty: {finalSetup.MovementPenalty:F3}\r\n" +
            $"Internal Search Score: {final.OptimizedInternalSearchScore:F3}\r\n" +
            $"Tested Candidates: {final.TestedCandidates}\r\n" +
            $"Coarse Candidates: {final.CoarseCandidates}\r\n" +
            $"Medium Candidates: {final.MediumCandidates}\r\n" +
            $"Fine Candidates: {final.FineCandidates}\r\n";

        // ------------------------------------------------------------
        // Best tested head/tail spindle position.
        // This is the optimizer's best tested position, even if the
        // final command is blocked or not recommended.
        // ------------------------------------------------------------
        _state.DiagnosticsReport +=
            "\r\nBEST TESTED HEAD/TAIL SPINDLE POSITION\r\n" +
            $"Tail X: {FormatLength(final.BestTestedTailXMM)}\r\n" +
            $"Tail Y: {FormatLength(final.BestTestedTailYMM)}\r\n" +
            $"Head X: {FormatLength(final.BestTestedHeadXMM)}\r\n" +
            $"Head Y: {FormatLength(final.BestTestedHeadYMM)}\r\n";

        string winner =
            final.IsRecommended
                ? "Spindle Axis Optimizer"
                : "Baseline";

        _state.DiagnosticsReport +=
            "\r\nOPTIMIZER COMPARISON\r\n" +
            $"Baseline Uniform Risk Score: {final.BaselineRiskScore:F1} / 100\r\n" +
            $"Spindle Axis Optimizer Uniform Risk Score: {final.RiskScore:F1} / 100\r\n" +
            $"Uniform Risk Improvement: {final.RiskScoreReductionPercent:F1}%\r\n" +

            $"Current Winner: {winner}\r\n";

        // ------------------------------------------------------------
        // Final machine recommendation.
        // These are the actual command values.
        // If recommendation is false, these are zero.
        // ------------------------------------------------------------
        _state.DiagnosticsReport +=
            "\r\nFINAL MACHINE RECOMMENDATION\r\n" +
            $"Tail spindle X: {FormatLength(final.CommandTailXMM)}\r\n" +
            $"Tail spindle Y: {FormatLength(final.CommandTailYMM)}\r\n" +
            $"Head spindle X: {FormatLength(final.CommandHeadXMM)}\r\n" +
            $"Head spindle Y: {FormatLength(final.CommandHeadYMM)}\r\n" +
            final.RecommendedAction + "\r\n";

        double logLengthMM = 0.0; // Compute the log length in millimeters from the point cloud for reporting and comparison

        //------------------------------------------------------------
        // Compute the log length in millimeters from the point cloud for reporting and comparison
        //------------------------------------------------------------
        if (_state.PointCloud != null && _state.PointCloud.Count > 0)
        {
            logLengthMM =
                _state.PointCloud.Max(p => p.Z) -
                _state.PointCloud.Min(p => p.Z);
        }

        //------------------------------------------------------------
        // Build the operator summary report for the final spindle recommendation, including scan quality,
        // frame usage, log length, average diameter, centerline sweep, worst out-of-round, low-wood/wane-risk
        // surface percentage, severe low wood percentage, recommended action, risk score, baseline risk score,
        // risk score reduction, final spindle position, and any warnings
        //------------------------------------------------------------
        string operatorSummary =
            "=== OPERATOR SUMMARY ===\r\n" +
            $"Scan Quality: {(_state.ScanFrames.Count(f => f.IsValid) == _state.ScanFrames.Count ? "Good" : "Needs Review")}\r\n" +
            $"Frames Used: {_state.ScanFrames.Count(f => f.IsValid)} of {_state.ScanFrames.Count}\r\n" +
            $"Log Length: {FormatLength(logLengthMM)}\r\n" +
            $"Average Diameter: {FormatLength(_state.Model.AverageDiameterMM)}\r\n" +
            $"Centerline Sweep: {FormatLength(_state.CenterlineSweep!.TotalSweepMM)}\r\n" +
            $"Worst Out-of-Round: {FormatLength(_state.Model.OvalityAnalysis!.OvalityMM)}\r\n" +
            $"Low-Wood / Wane-Risk Surface: {lowWoodPercent:F1}%\r\n" +
            $"Severe Low Wood: {severeLowWoodPercent:F1}%\r\n" +
            "\r\n" +
            $"Recommended Action: {final.RecommendedAction}\r\n" +
            $"Risk Score: {final.RiskScore:F1} / 100 ({final.RiskRating})\r\n" +
            $"Baseline Risk Score: {final.BaselineRiskScore:F1} / 100 ({final.BaselineRiskRating})\r\n" +
            $"Risk Score Reduction: {final.RiskScoreReductionPercent:F1}%\r\n" +
            "\r\n" +
            "Final Spindle Position:\r\n" +
            $"Tail X: {FormatLength(final.CommandTailXMM)}\r\n" +
            $"Tail Y: {FormatLength(final.CommandTailYMM)}\r\n" +
            $"Head X: {FormatLength(final.CommandHeadXMM)}\r\n" +
            $"Head Y: {FormatLength(final.CommandHeadYMM)}\r\n";

        // Append any warnings generated during the optimization process to the operator summary report for user awareness and action
        if (final.Warnings.Count > 0)
        {
            operatorSummary += "\r\nWarnings:\r\n";

            foreach (var warning in final.Warnings)
            {
                operatorSummary += "- " + warning + "\r\n";
            }
        }
        else
        {
            operatorSummary += "\r\nWarnings: None\r\n";
        }

        string engineeringDetails = _state.DiagnosticsReport;  // Store the current diagnostics report in a local variable for later use in building the final diagnostics report

        // The timing report is appended once, at the end of
        // RunCurrentFramesPipeline, so it includes this stage as well.

        if (_state.Settings.Diagnostics.IncludeEngineeringDetails)  // If the settings indicate that engineering details should be included in the diagnostics report,
                                                                    // append the operator summary and engineering details to the final diagnostics report for comprehensive reporting
        {
            _state.DiagnosticsReport =
                operatorSummary +
                "\r\n\r\n=== ENGINEERING DETAILS ===\r\n" +
                engineeringDetails;
        }
        else
        {
            _state.DiagnosticsReport = operatorSummary;
        }
    }


    // ------------------------------------------------------------
    // GEOMETRY-ONLY SPINDLE OPTIMIZATION

    // Runs after the complete measurement/defect-analysis pipeline. Defect,
    // adaptive-risk, movement-penalty, and uniform-risk scores are reported
    // after selection only; they never influence the chosen geometry position.
    // ------------------------------------------------------------


    /// <summary>
    /// Optimizes the spindle axis based on the binned radius field, defect patches, and adaptive
    /// scoring profile, producing the final spindle setup for the log.
    /// </summary>
    /// <param name="token">A cancellation token to observe while waiting for the task to complete.</param>
    /// <exception cref="InvalidOperationException">Thrown when the point cloud, binned radius field, or radius defect patches are missing or empty.</exception>
    public void OptimizeInscribedGeometry(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        if (_state.PointCloud == null || _state.PointCloud.Count == 0)
            throw new InvalidOperationException("Build point cloud first.");

        if (_state.OptimizationBinnedRadiusField == null ||
            _state.OptimizationBinnedRadiusField.Count == 0)
        {
            throw new InvalidOperationException(
                "Build the machine-frame optimization field first.");
        }

        SpindleOptimizationMode mode =
            _state.Settings.OptimizationMode;

        if (mode == SpindleOptimizationMode.PenaltyScored)
        {
            throw new InvalidOperationException(
                "Geometry-only optimization cannot run in PenaltyScored mode.");
        }

        _state.DiagnosticsReport +=
            "\r\n=== GEOMETRY-ONLY SPINDLE OPTIMIZATION ===\r\n" +
            $"Selected Mode: {mode}\r\n" +
            "Selection Rule: Measured inscribed geometry and fixed-axis circular virtual-peel recovery only.\r\n" +
            "Penalty Scores Used For Position: No\r\n" +
            "Defect Analysis Status: Completed and retained for reporting.\r\n";

        InscribedGeometryResult? cylinder = null;
        InscribedGeometryResult? oval = null;
        PeelSimulationResult? cylinderPeel = null;
        PeelSimulationResult? ovalPeel = null;

        try
        {
            if (mode == SpindleOptimizationMode.CompareInscribedGeometry)
            {
                // Cylinder and oval searches run at the same time.
                RunTimed("Cylinder And Oval Search (run together)", () =>
                {
                    Task<InscribedGeometryResult> cylinderTask =
                        Task.Run(() => RunTimed("Largest Inscribed Cylinder Search", () =>
                            InscribedGeometryOptimizer.FindLargestCylinder(
                                _state.OptimizationBinnedRadiusField,
                                _state.Settings.Optimization,
                                _state.Settings.InscribedGeometry,
                                _state.Settings.MachineLimits,
                                token)),
                            token);

                    Task<InscribedGeometryResult> ovalTask =
                        Task.Run(() => RunTimed("Largest Inscribed Oval Search", () =>
                            InscribedGeometryOptimizer.FindLargestOval(
                                _state.OptimizationBinnedRadiusField,
                                _state.Settings.Optimization,
                                _state.Settings.InscribedGeometry,
                                _state.Settings.MachineLimits,
                                token)),
                            token);

                    Task.WaitAll([cylinderTask, ovalTask], token);

                    cylinder = cylinderTask.Result;
                    oval = ovalTask.Result;
                });

                // The two peels are independent; work them out at the same time.
                RunTimed("Peel Numbers: Cylinder And Oval (run together)", () =>
                {
                    Task<PeelSimulationResult> cylinderPeelTask = Task.Run(
                        () => RunTimed("Peel Numbers: Cylinder", () => BuildPeelForGeometry(cylinder!)),
                        token);
                    Task<PeelSimulationResult> ovalPeelTask = Task.Run(
                        () => RunTimed("Peel Numbers: Oval", () => BuildPeelForGeometry(oval!)),
                        token);

                    Task.WaitAll([cylinderPeelTask, ovalPeelTask], token);

                    cylinderPeel = cylinderPeelTask.Result;
                    ovalPeel = ovalPeelTask.Result;
                });

                _state.DiagnosticsReport +=
                    BuildInscribedGeometryReport(cylinder!, cylinderPeel!);

                _state.DiagnosticsReport +=
                    BuildInscribedGeometryReport(oval!, ovalPeel!);
            }
            else if (mode == SpindleOptimizationMode.LargestInscribedCylinder)
            {
                cylinder =
                    InscribedGeometryOptimizer.FindLargestCylinder(
                        _state.OptimizationBinnedRadiusField,
                        _state.Settings.Optimization,
                        _state.Settings.InscribedGeometry,
                        _state.Settings.MachineLimits,
                        token);

                cylinderPeel =
                    BuildPeelForGeometry(cylinder);

                _state.DiagnosticsReport +=
                    BuildInscribedGeometryReport(
                        cylinder,
                        cylinderPeel);
            }
            else if (mode == SpindleOptimizationMode.LargestInscribedOval)
            {
                oval =
                    InscribedGeometryOptimizer.FindLargestOval(
                        _state.OptimizationBinnedRadiusField,
                        _state.Settings.Optimization,
                        _state.Settings.InscribedGeometry,
                        _state.Settings.MachineLimits,
                        token);

                ovalPeel =
                    BuildPeelForGeometry(oval);

                _state.DiagnosticsReport +=
                    BuildInscribedGeometryReport(
                        oval,
                        ovalPeel);
            }

            InscribedGeometryResult selectedGeometry;
            PeelSimulationResult selectedPeel;
            string selectionReason;

            if (mode ==
                SpindleOptimizationMode.CompareInscribedGeometry)
            {
                if (cylinder is null ||
                    cylinderPeel is null ||
                    oval is null ||
                    ovalPeel is null)
                {
                    throw new InvalidOperationException(
                        "Cylinder/oval comparison did not produce both candidates.");
                }

                GeometryPeelComparisonResult comparison =
                    GeometryPeelSelector.Select(
                        cylinder,
                        cylinderPeel,
                        oval,
                        ovalPeel,
                        _state.Settings.InscribedGeometry.OvalMinimumWaneAdvantagePercent);

                _state.GeometryPeelComparison = comparison;

                selectedGeometry = comparison.SelectedGeometry;
                selectedPeel = comparison.SelectedPeel;
                selectionReason = comparison.SelectionReason;
            }
            else if (mode ==
                     SpindleOptimizationMode.LargestInscribedCylinder)
            {
                selectedGeometry = cylinder ??
                    throw new InvalidOperationException(
                        "Largest inscribed cylinder result is missing.");

                selectedPeel = cylinderPeel ??
                    throw new InvalidOperationException(
                        "Largest inscribed cylinder peel is missing.");

                selectionReason =
                    "The operator selected Largest Inscribed Cylinder mode.";
            }
            else
            {
                selectedGeometry = oval ??
                    throw new InvalidOperationException(
                        "Largest inscribed oval result is missing.");

                selectedPeel = ovalPeel ??
                    throw new InvalidOperationException(
                        "Largest inscribed oval peel is missing.");

                selectionReason =
                    "The operator selected Largest Inscribed Oval mode.";
            }

            _state.LargestInscribedCylinder = cylinder;
            _state.LargestInscribedOval = oval;

            // The peels above are numbers only. Queue the images; they are
            // drawn after the PLC write when the scan is saved.
            DeferPeelView(PeelViewSlot.Cylinder, cylinderPeel);
            DeferPeelView(PeelViewSlot.Oval, ovalPeel);
            DeferPeelView(PeelViewSlot.Unwrapped, selectedPeel);

            OptimizedSpindleMovement move =
                selectedGeometry.SpindleMovement;

            List<string> machineWarnings =
                ValidateMachineMovement(
                    move.TailMoveXMM,
                    move.TailMoveYMM,
                    move.HeadMoveXMM,
                    move.HeadMoveYMM,
                    _state.Settings.MachineLimits);

            bool machineMoveAllowed =
                machineWarnings.All(
                    warning =>
                        !warning.StartsWith(
                            "BLOCKED",
                            StringComparison.CurrentCulture));

            double baselineWaneScore =
                SpindleAxisWaneOptimizer.ComputeAxisAdjustedWaneScore(
                    _state.OptimizationBinnedRadiusField,
                    tailXMM: 0.0,
                    tailYMM: 0.0,
                    headXMM: 0.0,
                    headYMM: 0.0,
                    _state.Settings.Wane.MinAngleBinsPerWaneSlice);

            double selectedWaneScore =
                SpindleAxisWaneOptimizer.ComputeAxisAdjustedWaneScore(
                    _state.OptimizationBinnedRadiusField,
                    move.TailMoveXMM,
                    move.TailMoveYMM,
                    move.HeadMoveXMM,
                    move.HeadMoveYMM,
                    _state.Settings.Wane.MinAngleBinsPerWaneSlice);

            double baselineDefectPenalty =
                PatchAwareWanePenalty.ComputeSpindleAxisAdjusted(
                    _state.RadiusDefectPatches,
                    tailXMM: 0.0,
                    tailYMM: 0.0,
                    headXMM: 0.0,
                    headYMM: 0.0,
                    depthWeight:
                        _state.Settings.Defects.PatchDepthWeight,
                    cellWeight:
                        _state.Settings.Defects.PatchCellWeight,
                    severityWeight:
                        _state.Settings.Defects.PatchSeverityWeight,
                    spindleReliefWeight:
                        _state.Settings.Defects.SpindleReliefWeight);

            double selectedDefectPenalty =
                PatchAwareWanePenalty.ComputeSpindleAxisAdjusted(
                    _state.RadiusDefectPatches,
                    move.TailMoveXMM,
                    move.TailMoveYMM,
                    move.HeadMoveXMM,
                    move.HeadMoveYMM,
                    depthWeight:
                        _state.Settings.Defects.PatchDepthWeight,
                    cellWeight:
                        _state.Settings.Defects.PatchCellWeight,
                    severityWeight:
                        _state.Settings.Defects.PatchSeverityWeight,
                    spindleReliefWeight:
                        _state.Settings.Defects.SpindleReliefWeight);

            string recommendedAction =
                machineMoveAllowed
                    ? $"Use {selectedGeometry.StrategyName} spindle position."
                    : "Keep current charger setup.";

            string reason =
                machineMoveAllowed
                    ? selectionReason
                    : "The selected geometry position exceeded one or more machine movement limits.";

            _state.FinalSpindleRecommendation =
                new FinalSpindleRecommendation
                {
                    OptimizationMode = mode,
                    SelectedStrategy =
                        selectedGeometry.StrategyName,
                    IsRecommended = machineMoveAllowed,
                    MachineMoveAllowed = machineMoveAllowed,
                    RecommendedAction = recommendedAction,
                    Reason = reason,
                    Warnings = [.. machineWarnings],

                    RiskScore = 0.0,
                    RiskRating = "NOT USED",
                    BaselineRiskScore = 0.0,
                    BaselineRiskRating = "NOT USED",
                    RiskScoreReductionPercent = 0.0,

                    CommandTailXMM =
                        machineMoveAllowed
                            ? -move.TailMoveXMM
                            : 0.0,
                    CommandTailYMM =
                        machineMoveAllowed
                            ? -move.TailMoveYMM
                            : 0.0,
                    CommandHeadXMM =
                        machineMoveAllowed
                            ? -move.HeadMoveXMM
                            : 0.0,
                    CommandHeadYMM =
                        machineMoveAllowed
                            ? -move.HeadMoveYMM
                            : 0.0,

                    BestTestedTailXMM = move.TailMoveXMM,
                    BestTestedTailYMM = move.TailMoveYMM,
                    BestTestedHeadXMM = move.HeadMoveXMM,
                    BestTestedHeadYMM = move.HeadMoveYMM,

                    MaxSpindleMovementMM =
                        selectedGeometry.MaxSpindleMovementMM,

                    TotalFullSheets =
                        selectedPeel.TotalFullSheets,
                    UsableFullSheets =
                        selectedPeel.UsableFullSheets,
                    RejectedFullSheets =
                        selectedPeel.RejectedFullSheets,
                    UsableVeneerLengthMM =
                        selectedPeel.UsableVeneerLengthMM,

                    InscribedMajorDiameterMM =
                        selectedGeometry.MajorSemiAxisMM * 2.0,
                    InscribedMinorDiameterMM =
                        selectedGeometry.MinorSemiAxisMM * 2.0,
                    InscribedOrientationDeg =
                        selectedGeometry.OrientationDeg,

                    BaselineWaneScoreMM =
                        baselineWaneScore,
                    OptimizedWaneScoreMM =
                        selectedWaneScore,
                    BaselineInternalSearchScore = 0.0,
                    OptimizedInternalSearchScore = 0.0,
                    BaselineDefectPenalty =
                        baselineDefectPenalty,
                    OptimizedDefectPenalty =
                        selectedDefectPenalty,

                    TestedCandidates =
                        selectedGeometry.TestedCandidates,
                    CoarseCandidates =
                        selectedGeometry.CoarseCandidates,
                    MediumCandidates =
                        selectedGeometry.MediumCandidates,
                    FineCandidates =
                        selectedGeometry.FineCandidates
                };

            _state.Model.WaneScore =
                selectedPeel.TotalWaneAreaPercent;

            _state.DiagnosticsReport +=
                "\r\nGEOMETRY PEEL SELECTION\r\n" +
                $"Winner: {selectedGeometry.StrategyName}\r\n" +
                $"Reason: {selectionReason}\r\n" +
                $"Usable Full 50-in Sheets: {selectedPeel.UsableFullSheets} of {selectedPeel.TotalFullSheets}\r\n" +
                $"Rejected Full 50-in Sheets: {selectedPeel.RejectedFullSheets}\r\n" +
                $"Usable Veneer Length: {FormatLength(selectedPeel.UsableVeneerLengthMM)}\r\n" +
                $"Estimated Wane Area: {selectedPeel.TotalWaneAreaPercent:F1}%\r\n" +
                $"Estimated Severe Wane Area: {selectedPeel.SevereWaneAreaPercent:F1}%\r\n" +
                "\r\nPOST-SELECTION METRICS — NOT USED TO CHOOSE POSITION\r\n" +
                $"Average Slice Radius Spread: {FormatLength(selectedWaneScore)}\r\n" +
                $"Internal Defect Penalty: {selectedDefectPenalty:F3}\r\n";

            FinalSpindleRecommendation final =
                _state.FinalSpindleRecommendation ??
                throw new InvalidOperationException(
                    "Final geometry recommendation was not created.");

            _state.DiagnosticsReport +=
                "\r\nFINAL MACHINE RECOMMENDATION\r\n" +
                $"Optimization Mode: {final.OptimizationMode}\r\n" +
                $"Selected Strategy: {final.SelectedStrategy}\r\n" +
                $"Tail spindle X: {FormatLength(final.CommandTailXMM)}\r\n" +
                $"Tail spindle Y: {FormatLength(final.CommandTailYMM)}\r\n" +
                $"Head spindle X: {FormatLength(final.CommandHeadXMM)}\r\n" +
                $"Head spindle Y: {FormatLength(final.CommandHeadYMM)}\r\n" +
                $"Major Inscribed Diameter: {FormatLength(final.InscribedMajorDiameterMM)}\r\n" +
                $"Minor Inscribed Diameter: {FormatLength(final.InscribedMinorDiameterMM)}\r\n" +
                $"Oval Orientation: {final.InscribedOrientationDeg:F1} deg\r\n" +
                final.RecommendedAction + "\r\n";

            if (final.Warnings.Count > 0)
            {
                _state.DiagnosticsReport +=
                    "\r\nMACHINE MOVEMENT LIMIT CHECK\r\n";

                foreach (string warning in final.Warnings)
                    _state.DiagnosticsReport += warning + "\r\n";
            }

            double logLengthMM =
                _state.PointCloud.Max(point => point.Z) -
                _state.PointCloud.Min(point => point.Z);

            int surfaceTotal =
                _state.RadiusDefectMap?.Points.Count ?? 0;

            double lowWoodPercent =
                surfaceTotal > 0
                    ? _state.RadiusDefectMap!.LowCount *
                      100.0 /
                      surfaceTotal
                    : 0.0;

            double severeLowWoodPercent =
                surfaceTotal > 0
                    ? _state.RadiusDefectMap!.SevereLowCount *
                      100.0 /
                      surfaceTotal
                    : 0.0;

            string operatorSummary =
                "=== OPERATOR SUMMARY ===\r\n" +
                $"Optimization Mode: {mode}\r\n" +
                $"Selected Strategy: {final.SelectedStrategy}\r\n" +
                "Penalty Score Involved In Position: No\r\n" +
                $"Frames Used: {_state.ScanFrames.Count(frame => frame.IsValid)} of {_state.ScanFrames.Count}\r\n" +
                $"Log Length: {FormatLength(logLengthMM)}\r\n" +
                $"Average Diameter: {FormatLength(_state.Model.AverageDiameterMM)}\r\n" +
                $"Centerline Sweep: {FormatLength(_state.CenterlineSweep?.TotalSweepMM ?? 0.0)}\r\n" +
                $"Worst Out-of-Round: {FormatLength(_state.Model.OvalityAnalysis?.OvalityMM ?? 0.0)}\r\n" +
                $"Low-Wood / Wane-Risk Surface: {lowWoodPercent:F1}%\r\n" +
                $"Severe Low Wood: {severeLowWoodPercent:F1}%\r\n" +
                "\r\n" +
                $"Usable Full 50-in Sheets: {final.UsableFullSheets} of {final.TotalFullSheets}\r\n" +
                $"Rejected Full 50-in Sheets: {final.RejectedFullSheets}\r\n" +
                $"Major Inscribed Diameter: {FormatLength(final.InscribedMajorDiameterMM)}\r\n" +
                $"Minor Inscribed Diameter: {FormatLength(final.InscribedMinorDiameterMM)}\r\n" +
                "\r\n" +
                $"Recommended Action: {final.RecommendedAction}\r\n" +
                $"Reason: {final.Reason}\r\n" +
                "\r\n" +
                "Final Spindle Position:\r\n" +
                $"Tail X: {FormatLength(final.CommandTailXMM)}\r\n" +
                $"Tail Y: {FormatLength(final.CommandTailYMM)}\r\n" +
                $"Head X: {FormatLength(final.CommandHeadXMM)}\r\n" +
                $"Head Y: {FormatLength(final.CommandHeadYMM)}\r\n";

            if (final.Warnings.Count > 0)
            {
                operatorSummary +=
                    "\r\nWarnings:\r\n";

                foreach (string warning in final.Warnings)
                    operatorSummary += "- " + warning + "\r\n";
            }
            else
            {
                operatorSummary +=
                    "\r\nWarnings: None\r\n";
            }

            string engineeringDetails =
                _state.DiagnosticsReport;

            // The timing report is appended once, at the end of
            // RunCurrentFramesPipeline, so it includes this stage as well.

            _state.DiagnosticsReport =
                _state.Settings.Diagnostics.IncludeEngineeringDetails
                    ? operatorSummary +
                      "\r\n\r\n=== ENGINEERING DETAILS ===\r\n" +
                      engineeringDetails
                    : operatorSummary;
        }
        catch
        {
            // Images are owned by PipelineState only after assignment above.
            if (!ReferenceEquals(
                    _state.CylinderPeelView,
                    cylinderPeel?.Image))
            {
                cylinderPeel?.Image.Dispose();
            }

            if (!ReferenceEquals(
                    _state.OvalPeelView,
                    ovalPeel?.Image))
            {
                ovalPeel?.Image.Dispose();
            }

            throw;
        }
    }


    /// <summary>
    /// Optimizes the spindle axis based on the binned radius field, defect patches, and adaptive
    /// scoring profile, producing the final spindle setup for the log.
    /// </summary>
    /// <param name="token">A cancellation token to observe while waiting for the task to complete.</param>
    /// <exception cref="InvalidOperationException">Thrown when the point cloud, binned radius field, or radius defect patches are missing or empty.</exception>
    public void OptimizeByVeneerRecovery(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        if (_state.Settings.OptimizationMode !=
            SpindleOptimizationMode.RecoveryOptimized)
        {
            throw new InvalidOperationException(
                "Recovery optimization can run only in RecoveryOptimized mode.");
        }

        if (_state.OptimizationBinnedRadiusField == null ||
            _state.OptimizationBinnedRadiusField.Count == 0)
        {
            throw new InvalidOperationException(
                "Build the machine-frame optimization field first.");
        }

        FinalSpindleRecommendation penaltyRecommendation =
            _state.FinalSpindleRecommendation ??
            throw new InvalidOperationException(
                "Run the penalty candidate search before recovery comparison.");

        var candidates = new List<RecoveryCandidateResult>();
        PeelSimulationResult? baselinePeel = null;
        PeelSimulationResult? penaltyPeel = null;
        PeelSimulationResult? cylinderPeel = null;
        PeelSimulationResult? ovalPeel = null;
        PeelSimulationResult? directRecoveryPeel = null;
        InscribedGeometryResult? cylinder = null;
        InscribedGeometryResult? oval = null;
        DirectRecoverySearchResult? directRecoverySearch = null;

        try
        {
            RecoveryOptimizationSettings settings =
                _state.Settings.RecoveryOptimization;

            if (settings.IncludeBaselineCandidate)
            {
                var movement = new OptimizedSpindleMovement();
                baselinePeel = BuildPeelForMovement(movement);

                candidates.Add(new RecoveryCandidateResult
                {
                    StrategyName = "Current Charger Baseline",
                    Movement = movement,
                    Peel = baselinePeel,
                    CandidateSearchCount = 1
                });
            }

            if (settings.IncludePenaltyCandidate)
            {
                var movement = new OptimizedSpindleMovement
                {
                    TailMoveXMM = penaltyRecommendation.BestTestedTailXMM,
                    TailMoveYMM = penaltyRecommendation.BestTestedTailYMM,
                    HeadMoveXMM = penaltyRecommendation.BestTestedHeadXMM,
                    HeadMoveYMM = penaltyRecommendation.BestTestedHeadYMM
                };

                penaltyPeel = BuildPeelForMovement(movement);

                candidates.Add(new RecoveryCandidateResult
                {
                    StrategyName = "Penalty Search Candidate",
                    Movement = movement,
                    Peel = penaltyPeel,
                    CandidateSearchCount = penaltyRecommendation.TestedCandidates
                });
            }

            if (settings.IncludeCylinderCandidate)
            {
                cylinder = InscribedGeometryOptimizer.FindLargestCylinder(
                    _state.OptimizationBinnedRadiusField,
                    _state.Settings.Optimization,
                    _state.Settings.InscribedGeometry,
                    _state.Settings.MachineLimits,
                    token);

                cylinderPeel = BuildPeelForGeometry(cylinder);

                candidates.Add(new RecoveryCandidateResult
                {
                    StrategyName = cylinder.StrategyName,
                    Movement = cylinder.SpindleMovement,
                    Peel = cylinderPeel,
                    Geometry = cylinder,
                    CandidateSearchCount = cylinder.TestedCandidates
                });
            }

            if (settings.IncludeOvalCandidate)
            {
                oval = InscribedGeometryOptimizer.FindLargestOval(
                    _state.OptimizationBinnedRadiusField,
                    _state.Settings.Optimization,
                    _state.Settings.InscribedGeometry,
                    _state.Settings.MachineLimits,
                    token);

                ovalPeel = BuildPeelForGeometry(oval);

                candidates.Add(new RecoveryCandidateResult
                {
                    StrategyName = oval.StrategyName,
                    Movement = oval.SpindleMovement,
                    Peel = ovalPeel,
                    Geometry = oval,
                    CandidateSearchCount = oval.TestedCandidates
                });
            }

            if (settings.EnableDirectRecoveryRefinement)
            {
                directRecoverySearch = DirectRecoveryOptimizer.FindBest(
                    _state.OptimizationBinnedRadiusField,
                    [.. candidates.Select(candidate => candidate.Movement)],
                    _state.Settings,
                    token);

                directRecoveryPeel = BuildPeelForMovement(
                    directRecoverySearch.Movement);

                candidates.Add(new RecoveryCandidateResult
                {
                    StrategyName = "Direct Virtual-Peel Refinement",
                    Movement = directRecoverySearch.Movement,
                    Peel = directRecoveryPeel,
                    CandidateSearchCount = directRecoverySearch.EvaluatedCandidates
                });
            }

            RecoveryOptimizationResult recovery =
                RecoveryStrategySelector.Select(
                    candidates,
                    settings.PreferLowerMovementOnExactTie);

            _state.RecoveryOptimization = recovery;
            _state.LargestInscribedCylinder = cylinder;
            _state.LargestInscribedOval = oval;

            // The peels above are numbers only. Queue the images; they are
            // drawn after the PLC write when the scan is saved.
            DeferPeelView(PeelViewSlot.Baseline, baselinePeel);
            DeferPeelView(PeelViewSlot.Penalty, penaltyPeel);
            DeferPeelView(PeelViewSlot.Cylinder, cylinderPeel);
            DeferPeelView(PeelViewSlot.Oval, ovalPeel);
            DeferPeelView(PeelViewSlot.DirectRecovery, directRecoveryPeel);
            _state.DirectRecoverySearch = directRecoverySearch;

            DeferPeelView(PeelViewSlot.Unwrapped, recovery.Selected.Peel);

            OptimizedSpindleMovement move = recovery.Selected.Movement;

            List<string> warnings = ValidateMachineMovement(
                move.TailMoveXMM,
                move.TailMoveYMM,
                move.HeadMoveXMM,
                move.HeadMoveYMM,
                _state.Settings.MachineLimits);

            bool machineMoveAllowed = warnings.All(
                warning => !warning.StartsWith(
                    "BLOCKED",
                    StringComparison.OrdinalIgnoreCase));

            bool isBaseline =
                recovery.Selected.StrategyName ==
                "Current Charger Baseline";

            double selectedWaneScore =
                SpindleAxisWaneOptimizer.ComputeAxisAdjustedWaneScore(
                    _state.OptimizationBinnedRadiusField,
                    move.TailMoveXMM,
                    move.TailMoveYMM,
                    move.HeadMoveXMM,
                    move.HeadMoveYMM,
                    _state.Settings.Wane.MinAngleBinsPerWaneSlice);

            double selectedDefectPenalty =
                PatchAwareWanePenalty.ComputeSpindleAxisAdjusted(
                    _state.RadiusDefectPatches,
                    move.TailMoveXMM,
                    move.TailMoveYMM,
                    move.HeadMoveXMM,
                    move.HeadMoveYMM,
                    _state.Settings.Defects.PatchDepthWeight,
                    _state.Settings.Defects.PatchCellWeight,
                    _state.Settings.Defects.PatchSeverityWeight,
                    _state.Settings.Defects.SpindleReliefWeight);

            InscribedGeometryResult? selectedGeometry =
                recovery.Selected.Geometry;

            int totalSearchCount =
                candidates.Sum(candidate => candidate.CandidateSearchCount);

            string recommendedAction = !machineMoveAllowed
                ? "Keep current charger setup; selected recovery candidate exceeded a hard machine limit."
                : isBaseline
                    ? "Keep current charger setup; baseline produced the best virtual peel."
                    : $"Use {recovery.Selected.StrategyName} spindle position.";

            _state.FinalSpindleRecommendation =
                new FinalSpindleRecommendation
                {
                    OptimizationMode =
                        SpindleOptimizationMode.RecoveryOptimized,
                    SelectedStrategy =
                        recovery.Selected.StrategyName,
                    IsRecommended = machineMoveAllowed,
                    MachineMoveAllowed = machineMoveAllowed,
                    RecommendedAction = recommendedAction,
                    Reason = recovery.SelectionReason,
                    Warnings = warnings,

                    RiskScore = penaltyRecommendation.RiskScore,
                    RiskRating = penaltyRecommendation.RiskRating,
                    BaselineRiskScore =
                        penaltyRecommendation.BaselineRiskScore,
                    BaselineRiskRating =
                        penaltyRecommendation.BaselineRiskRating,
                    RiskScoreReductionPercent =
                        penaltyRecommendation.RiskScoreReductionPercent,

                    CommandTailXMM =
                        machineMoveAllowed ? -move.TailMoveXMM : 0.0,
                    CommandTailYMM =
                        machineMoveAllowed ? -move.TailMoveYMM : 0.0,
                    CommandHeadXMM =
                        machineMoveAllowed ? -move.HeadMoveXMM : 0.0,
                    CommandHeadYMM =
                        machineMoveAllowed ? -move.HeadMoveYMM : 0.0,

                    BestTestedTailXMM = move.TailMoveXMM,
                    BestTestedTailYMM = move.TailMoveYMM,
                    BestTestedHeadXMM = move.HeadMoveXMM,
                    BestTestedHeadYMM = move.HeadMoveYMM,
                    MaxSpindleMovementMM =
                        recovery.Selected.MaxMovementMM,

                    TotalFullSheets =
                        recovery.Selected.Peel.TotalFullSheets,
                    UsableFullSheets =
                        recovery.Selected.Peel.UsableFullSheets,
                    RejectedFullSheets =
                        recovery.Selected.Peel.RejectedFullSheets,
                    UsableVeneerLengthMM =
                        recovery.Selected.Peel.UsableVeneerLengthMM,

                    InscribedMajorDiameterMM =
                        selectedGeometry?.MajorSemiAxisMM * 2.0 ?? 0.0,
                    InscribedMinorDiameterMM =
                        selectedGeometry?.MinorSemiAxisMM * 2.0 ?? 0.0,
                    InscribedOrientationDeg =
                        selectedGeometry?.OrientationDeg ?? 0.0,

                    BaselineWaneScoreMM =
                        penaltyRecommendation.BaselineWaneScoreMM,
                    OptimizedWaneScoreMM = selectedWaneScore,
                    BaselineInternalSearchScore =
                        penaltyRecommendation.BaselineInternalSearchScore,
                    OptimizedInternalSearchScore =
                        penaltyRecommendation.OptimizedInternalSearchScore,
                    BaselineDefectPenalty =
                        penaltyRecommendation.BaselineDefectPenalty,
                    OptimizedDefectPenalty = selectedDefectPenalty,

                    TestedCandidates = totalSearchCount,
                    CoarseCandidates =
                        penaltyRecommendation.CoarseCandidates +
                        (cylinder?.CoarseCandidates ?? 0) +
                        (oval?.CoarseCandidates ?? 0),
                    MediumCandidates =
                        penaltyRecommendation.MediumCandidates +
                        (cylinder?.MediumCandidates ?? 0) +
                        (oval?.MediumCandidates ?? 0),
                    FineCandidates =
                        penaltyRecommendation.FineCandidates +
                        (cylinder?.FineCandidates ?? 0) +
                        (oval?.FineCandidates ?? 0) +
                        (directRecoverySearch?.EvaluatedCandidates ?? 0)
                };

            _state.Model.WaneScore =
                recovery.Selected.Peel.TotalWaneAreaPercent;

            string comparisonReport =
                "\r\n=== RECOVERY-BASED FINAL SELECTION ===\r\n" +
                "Final objective: maximize usable full 50-inch sheets from the physical circular virtual peel.\r\n" +
                "Tie order: usable veneer length, fewer rejected sheets, lower severe wane, lower total wane, lower movement.\r\n" +
                "Penalty scores do not select the final winner; the penalty search contributes one candidate position only.\r\n" +
                "A bounded direct recovery refinement searches nearby XY positions using metrics-only circular peel simulation, then renders the winning candidate at full resolution.\r\n" +
                "Rotation command: none. Rotation remains entirely in the PLC.\r\n";

            foreach (RecoveryCandidateResult candidate in candidates)
            {
                comparisonReport +=
                    $"\r\n{candidate.StrategyName}\r\n" +
                    $"Usable Full Sheets: {candidate.Peel.UsableFullSheets} of {candidate.Peel.TotalFullSheets}\r\n" +
                    $"Rejected Full Sheets: {candidate.Peel.RejectedFullSheets}\r\n" +
                    $"Usable Veneer Length: {FormatLength(candidate.Peel.UsableVeneerLengthMM)}\r\n" +
                    $"Wane Area: {candidate.Peel.TotalWaneAreaPercent:F2}%\r\n" +
                    $"Severe Wane Area: {candidate.Peel.SevereWaneAreaPercent:F2}%\r\n" +
                    $"Max XY Movement: {FormatLength(candidate.MaxMovementMM)}\r\n";
            }

            FinalSpindleRecommendation final =
                _state.FinalSpindleRecommendation ??
                throw new InvalidOperationException(
                    "Recovery optimization did not produce a final recommendation.");

            comparisonReport +=
                "\r\nWINNER\r\n" +
                $"Strategy: {final.SelectedStrategy}\r\n" +
                $"Reason: {final.Reason}\r\n" +
                $"Tail X: {FormatLength(final.CommandTailXMM)}\r\n" +
                $"Tail Y: {FormatLength(final.CommandTailYMM)}\r\n" +
                $"Head X: {FormatLength(final.CommandHeadXMM)}\r\n" +
                $"Head Y: {FormatLength(final.CommandHeadYMM)}\r\n" +
                $"Action: {final.RecommendedAction}\r\n";

            string operatorSummary =
                "=== OPERATOR SUMMARY ===\r\n" +
                "Optimization Mode: Recovery Optimized\r\n" +
                $"Selected Strategy: {final.SelectedStrategy}\r\n" +
                $"Usable Full 50-in Sheets: {final.UsableFullSheets} of {final.TotalFullSheets}\r\n" +
                $"Rejected Full 50-in Sheets: {final.RejectedFullSheets}\r\n" +
                $"Usable Veneer Length: {FormatLength(final.UsableVeneerLengthMM)}\r\n" +
                $"Recommended Action: {final.RecommendedAction}\r\n" +
                $"Reason: {final.Reason}\r\n" +
                "\r\nFinal Spindle Position:\r\n" +
                $"Tail X: {FormatLength(final.CommandTailXMM)}\r\n" +
                $"Tail Y: {FormatLength(final.CommandTailYMM)}\r\n" +
                $"Head X: {FormatLength(final.CommandHeadXMM)}\r\n" +
                $"Head Y: {FormatLength(final.CommandHeadYMM)}\r\n" +
                "\r\nKnife/rotation note: this software outputs XY offsets only; no rotation command is generated.\r\n";

            string engineeringDetails =
                _state.DiagnosticsReport +
                comparisonReport;

            _state.DiagnosticsReport =
                _state.Settings.Diagnostics.IncludeEngineeringDetails
                    ? operatorSummary +
                      "\r\n\r\n=== ENGINEERING DETAILS ===\r\n" +
                      engineeringDetails
                    : operatorSummary;
        }
        catch
        {
            if (!ReferenceEquals(_state.BaselinePeelView, baselinePeel?.Image))
                baselinePeel?.Image.Dispose();

            if (!ReferenceEquals(_state.PenaltyPeelView, penaltyPeel?.Image))
                penaltyPeel?.Image.Dispose();

            if (!ReferenceEquals(_state.CylinderPeelView, cylinderPeel?.Image))
                cylinderPeel?.Image.Dispose();

            if (!ReferenceEquals(_state.OvalPeelView, ovalPeel?.Image))
                ovalPeel?.Image.Dispose();

            if (!ReferenceEquals(_state.DirectRecoveryPeelView, directRecoveryPeel?.Image))
                directRecoveryPeel?.Image.Dispose();

            throw;
        }
    }

    /// <summary>
    /// Builds a peel simulation result for a given spindle movement, using the current point cloud and settings.
    /// </summary>
    /// <param name="movement">The optimized spindle movement to simulate.</param>
    /// <returns>A peel simulation result for the given spindle movement.</returns>
    /// <summary>
    /// Queues the image for a numbers-only peel. Any image left in that slot
    /// from earlier is released now; the new one is drawn when the scan is saved.
    /// </summary>
    private void DeferPeelView(PeelViewSlot slot, PeelSimulationResult? peel)
    {
        switch (slot)
        {
            case PeelViewSlot.Unwrapped: _state.UnwrappedPeelView?.Dispose(); _state.UnwrappedPeelView = null; break;
            case PeelViewSlot.Cylinder: _state.CylinderPeelView?.Dispose(); _state.CylinderPeelView = null; break;
            case PeelViewSlot.Oval: _state.OvalPeelView?.Dispose(); _state.OvalPeelView = null; break;
            case PeelViewSlot.Baseline: _state.BaselinePeelView?.Dispose(); _state.BaselinePeelView = null; break;
            case PeelViewSlot.Penalty: _state.PenaltyPeelView?.Dispose(); _state.PenaltyPeelView = null; break;
            case PeelViewSlot.DirectRecovery: _state.DirectRecoveryPeelView?.Dispose(); _state.DirectRecoveryPeelView = null; break;
        }

        _state.DeferredPeelViews.RemoveAll(request => request.Slot == slot);

        if (peel is not null)
            _state.DeferredPeelViews.Add(new DeferredPeelView(slot, peel));
    }

    private PeelSimulationResult BuildPeelForMovement(OptimizedSpindleMovement movement)
    {
        return UnwrappedPeelViewBuilder.ComputeMetrics(
            _state.PointCloud,
            _state.Settings.PeelView,
            _state.Settings.UsableRadius,
            _state.Settings.Wane,
            movement.TailMoveXMM,
            movement.TailMoveYMM,
            movement.HeadMoveXMM,
            movement.HeadMoveYMM);
    }

    /// <summary>
    /// Builds a peel simulation result for a given inscribed geometry result, using the current point cloud and settings.
    /// </summary>
    /// <param name="geometry">The inscribed geometry result to simulate.</param>
    /// <returns>A peel simulation result for the given inscribed geometry result.</returns>
    private PeelSimulationResult BuildPeelForGeometry(InscribedGeometryResult geometry)
    {
        OptimizedSpindleMovement move =
            geometry.SpindleMovement;

        return UnwrappedPeelViewBuilder.ComputeMetrics(
            _state.PointCloud,
            _state.Settings.PeelView,
            _state.Settings.UsableRadius,
            _state.Settings.Wane,
            move.TailMoveXMM,
            move.TailMoveYMM,
            move.HeadMoveXMM,
            move.HeadMoveYMM);
    }

    /// <summary>
    /// Builds a report for a given inscribed geometry result and its corresponding peel simulation result.
    /// </summary>
    /// <param name="geometry">The inscribed geometry result to report on.</param>
    /// <param name="peel">The peel simulation result corresponding to the geometry.</param>
    /// <returns>A string containing the report for the given inscribed geometry result and peel simulation result.</returns>
    private static string BuildInscribedGeometryReport(
        InscribedGeometryResult geometry,
        PeelSimulationResult peel)
    {
        string geometryLine =
            geometry.Kind ==
            InscribedGeometryKind.Cylinder
                ? $"Inscribed Diameter: {FormatLength(geometry.GuaranteedCircularRadiusMM * 2.0)}\r\n"
                : $"Major Diameter: {FormatLength(geometry.MajorSemiAxisMM * 2.0)}\r\n" +
                  $"Minor Diameter: {FormatLength(geometry.MinorSemiAxisMM * 2.0)}\r\n" +
                  $"Orientation: {geometry.OrientationDeg:F1} deg\r\n";

        string centerlineLines =
            geometry.CenterlineAnchorUsed
                ? $"Fitted Centerline Tail X: {FormatLength(geometry.CenterlineTailXMM)}\r\n" +
                  $"Fitted Centerline Tail Y: {FormatLength(geometry.CenterlineTailYMM)}\r\n" +
                  $"Fitted Centerline Head X: {FormatLength(geometry.CenterlineHeadXMM)}\r\n" +
                  $"Fitted Centerline Head Y: {FormatLength(geometry.CenterlineHeadYMM)}\r\n" +
                  $"Axis Distance From Fitted Centerline: {FormatLength(geometry.CenterlineDeviationMM)}\r\n"
                : "Fitted Centerline Anchor: Not used\r\n";

        return
            $"\r\n{geometry.StrategyName.ToUpperInvariant()}\r\n" +
            geometryLine +
            $"Tail X: {FormatLength(geometry.SpindleMovement.TailMoveXMM)}\r\n" +
            $"Tail Y: {FormatLength(geometry.SpindleMovement.TailMoveYMM)}\r\n" +
            $"Head X: {FormatLength(geometry.SpindleMovement.HeadMoveXMM)}\r\n" +
            $"Head Y: {FormatLength(geometry.SpindleMovement.HeadMoveYMM)}\r\n" +
            centerlineLines +
            (geometry.CertifiedGapMM.HasValue
                ? $"Certified Search: {geometry.CertifiedEvaluations} positions tested over the full machine travel; " +
                  $"no position can score more than {geometry.CertifiedGapMM.Value:F2} mm better" +
                  (geometry.CertifiedSearchImproved ? " (improved on the grid search)" : " (grid search answer kept)") + "\r\n"
                : string.Empty) +
            $"Tested Candidates: {geometry.TestedCandidates}\r\n" +
            $"Usable Full 50-in Sheets: {peel.UsableFullSheets} of {peel.TotalFullSheets}\r\n" +
            $"Rejected Full 50-in Sheets: {peel.RejectedFullSheets}\r\n" +
            $"Estimated Wane Area: {peel.TotalWaneAreaPercent:F1}%\r\n" +
            $"Estimated Severe Wane Area: {peel.SevereWaneAreaPercent:F1}%\r\n";
    }

    // ------------------------------------------------------------
    // BUILD UNWRAPPED PEEL VIEW
    // Creates a flat view of the scanned log surface.
    // ------------------------------------------------------------

    /// <summary>
    /// Builds an unwrapped peel view from the scanned rotating point cloud, using the best-tested spindle-axis position.
    /// </summary>
    /// <param name="token">A cancellation token to observe while performing the operation.</param>
    /// <exception cref="InvalidOperationException">Thrown if the point cloud has not been built or if peel view settings are missing.</exception>
    public void BuildUnwrappedPeelView(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        if (_state.PointCloud == null || _state.PointCloud.Count == 0)  // Ensure that the point cloud has been built before creating the unwrapped peel view
            throw new InvalidOperationException("Build the rotating point cloud first.");

        if (_state.Settings.PeelView == null)
            throw new InvalidOperationException("Peel view settings are missing.");

        var final = _state.FinalSpindleRecommendation;

        double tailXMM = final?.BestTestedTailXMM ?? 0.0;
        double tailYMM = final?.BestTestedTailYMM ?? 0.0;
        double headXMM = final?.BestTestedHeadXMM ?? 0.0;
        double headYMM = final?.BestTestedHeadYMM ?? 0.0;

        // Numbers only; the image is drawn after the PLC write when the scan
        // is saved (DeferredPeelViewRenderer).
        var result = UnwrappedPeelViewBuilder.ComputeMetrics(
            _state.PointCloud,
            _state.Settings.PeelView,
            _state.Settings.UsableRadius,
            _state.Settings.Wane,
            tailXMM,
            tailYMM,
            headXMM,
            headYMM);

        DeferPeelView(PeelViewSlot.Unwrapped, result);

        if (_state.FinalSpindleRecommendation is not null)
        {
            _state.FinalSpindleRecommendation.TotalFullSheets =
                result.TotalFullSheets;
            _state.FinalSpindleRecommendation.UsableFullSheets =
                result.UsableFullSheets;
            _state.FinalSpindleRecommendation.RejectedFullSheets =
                result.RejectedFullSheets;
            _state.FinalSpindleRecommendation.UsableVeneerLengthMM =
                result.UsableVeneerLengthMM;
        }

        _state.DiagnosticsReport +=
            "\r\nUNWRAPPED PEEL VIEW\r\n" +
            "Status: Created from scanned rotating point cloud using best-tested spindle-axis position\r\n" +
            $"Peel Tail X: {FormatLength(tailXMM)}\r\n" +
            $"Peel Tail Y: {FormatLength(tailYMM)}\r\n" +
            $"Peel Head X: {FormatLength(headXMM)}\r\n" +
            $"Peel Head Y: {FormatLength(headYMM)}\r\n" +
            $"Outer Diameter: {result.OuterDiameterMM / 25.4:F3} in\r\n" +
            $"Core Diameter: {result.CoreDiameterMM / 25.4:F3} in\r\n" +
            $"Log Length: {result.LogLengthMM / 25.4:F3} in\r\n" +
            $"Estimated Peel Revolutions: {result.Revolutions}\r\n" +
            $"Estimated Wane / Missing Veneer Area: {result.TotalWaneAreaPercent:F1}%\r\n" +
            $"Estimated Severe Wane Area: {result.SevereWaneAreaPercent:F1}%\r\n" +
            $"Usable Full 50-in Sheets: {result.UsableFullSheets} of {result.TotalFullSheets}\r\n" +
            $"Rejected Full 50-in Sheets: {result.RejectedFullSheets}\r\n" +
            $"Usable Veneer Length: {FormatLength(result.UsableVeneerLengthMM)}\r\n";
    }

    // ------------------------------------------------------------------------------------------------------------------------
    // HELPER FUNCTIONS
    // To understand the meaning of the results
    // ------------------------------------------------------------------------------------------------------------------------

    // ------------------------------------------------------------
    // Unit / Formatting Helpers
    // ------------------------------------------------------------

    /// <summary>
    /// Converts a length from millimeters to inches.
    /// </summary>
    /// <param name="mm">The length in millimeters.</param>
    /// <returns>The length in inches.</returns>
    private static double Inches(double mm)
    {
        return mm / 25.4;
    }

    /// <summary>
    /// Formats a length in both inches and millimeters.
    /// </summary>
    /// <param name="mm">The length in millimeters.</param>
    /// <param name="inDecimals">The number of decimal places for inches.</param>
    /// <param name="mmDecimals">The number of decimal places for millimeters.</param>
    /// <returns>A formatted string representing the length in both inches and millimeters.</returns>
    private static string FormatLength(
        double mm,
        int inDecimals = 3,
        int mmDecimals = 2)
    {
        CultureInfo culture = CultureInfo.CurrentCulture;

        string inchesFormat = "F" + inDecimals.ToString(CultureInfo.InvariantCulture);
        string millimetersFormat = "F" + mmDecimals.ToString(CultureInfo.InvariantCulture);

        return
            $"{Inches(mm).ToString(inchesFormat, culture)} in / " +
            $"{mm.ToString(millimetersFormat, culture)} mm";
    }

    /// <summary>
    /// Formats a range of lengths in both inches and millimeters.
    /// </summary>
    /// <param name="startMM">The starting length in millimeters.</param>
    /// <param name="endMM">The ending length in millimeters.</param>
    /// <param name="inDecimals">The number of decimal places for inches.</param>
    /// <param name="mmDecimals">The number of decimal places for millimeters.</param>
    /// <returns>A formatted string representing the range of lengths in both inches and millimeters.</returns>
    private static string FormatLengthRange(
        double startMM,
        double endMM,
        int inDecimals = 3,
        int mmDecimals = 2)
    {
        CultureInfo culture = CultureInfo.CurrentCulture;

        string inchesFormat = "F" + inDecimals.ToString(CultureInfo.InvariantCulture);
        string millimetersFormat = "F" + mmDecimals.ToString(CultureInfo.InvariantCulture);

        return
            $"{Inches(startMM).ToString(inchesFormat, culture)} to " +
            $"{Inches(endMM).ToString(inchesFormat, culture)} in / " +
            $"{startMM.ToString(millimetersFormat, culture)} to " +
            $"{endMM.ToString(millimetersFormat, culture)} mm";
    }

    /// <summary>
    /// Formats a signed length in both inches and millimeters.
    /// </summary>
    /// <param name="mm">The length in millimeters.</param>
    /// <param name="inDecimals">The number of decimal places for inches.</param>
    /// <param name="mmDecimals">The number of decimal places for millimeters.</param>
    /// <returns>A formatted string representing the signed length in both inches and millimeters.</returns>
    private static string FormatSignedLength(
        double mm,
        int inDecimals = 3,
        int mmDecimals = 2)
    {
        CultureInfo culture = CultureInfo.CurrentCulture;

        double inches = Inches(mm);

        string inchesFormat = "F" + inDecimals.ToString(CultureInfo.InvariantCulture);
        string millimetersFormat = "F" + mmDecimals.ToString(CultureInfo.InvariantCulture);

        string inchSign = inches > 0.0 ? "+" : string.Empty;
        string millimeterSign = mm > 0.0 ? "+" : string.Empty;

        return
            $"{inchSign}{inches.ToString(inchesFormat, culture)} in / " +
            $"{millimeterSign}{mm.ToString(millimetersFormat, culture)} mm";
    }

    /// <summary>
    /// Formats a depth below the expected surface in inches, given a negative surface error in millimeters.
    /// </summary>
    /// <param name="mm">The negative surface error in millimeters.</param>
    /// <param name="inDecimals">The number of decimal places for inches.</param>
    /// <returns>A formatted string representing the depth below the expected surface in inches.</returns>
    private static string FormatDepthBelowSurface(
        double mm,
        int inDecimals = 3)
    {
        CultureInfo culture = CultureInfo.CurrentCulture;

        string inchesFormat = "F" + inDecimals.ToString(CultureInfo.InvariantCulture);

        return
            $"{Math.Abs(Inches(mm)).ToString(inchesFormat, culture)} " +
            "in below expected surface";
    }

    /// <summary>
    /// Formats a score value with its unit, using the current culture for formatting.
    /// </summary>
    /// <param name="value">The score value to format.</param>
    /// <param name="unit">The unit of the score value.</param>
    /// <returns>A formatted string representing the score value with its unit.</returns>

    private static string FormatScoreValue(double value, string unit)
    {
        CultureInfo culture = CultureInfo.CurrentCulture;

        if (unit == "%")
        {
            return value.ToString("F1", culture) + "%";
        }

        return value.ToString("F3", culture) + " " + unit;
    }



    /// <summary>
    /// Converts a negative surface error to a positive depth in inches.
    /// </summary>
    /// <param name="errorMM">The negative surface error in millimeters.</param>
    /// <returns>The positive depth below the expected surface in inches.</returns>
    private static double DepthBelowSurfaceInches(double errorMM)
    {
        if (errorMM >= 0.0)
        {
            return 0.0;
        }

        return Math.Abs(Inches(errorMM));
    }

    // ------------------------------------------------------------
    // Geometry / Movement Helpers
    // ------------------------------------------------------------

    /// <summary>
    /// Computes the maximum spindle movement in millimeters based on the tail and head X/Y movements, returning the larger of the two calculated distances.
    /// </summary>
    /// <param name="tailXMM">The tail spindle movement in the X direction in millimeters.</param>
    /// <param name="tailYMM">The tail spindle movement in the Y direction in millimeters.</param>
    /// <param name="headXMM">The head spindle movement in the X direction in millimeters.</param>
    /// <param name="headYMM">The head spindle movement in the Y direction in millimeters.</param>
    /// <returns>The maximum spindle movement in millimeters.</returns>
    private static double MaxSpindleMovementMM(double tailXMM, double tailYMM, double headXMM, double headYMM)
    {
        double tailMove = Math.Sqrt(tailXMM * tailXMM + tailYMM * tailYMM); // Calculate the Euclidean distance for the tail spindle movement based on its X and Y movements
        double headMove = Math.Sqrt(headXMM * headXMM + headYMM * headYMM); // Calculate the Euclidean distance for the head spindle movement based on its X and Y movements

        return Math.Max(tailMove, headMove); // Return the larger of the two calculated distances, representing the maximum spindle movement in millimeters
    }

    /// <summary>
    /// Computes the percentage improvement from a baseline score to a candidate score, returning 0.0 if the baseline score is effectively zero to avoid division by zero.
    /// </summary>
    /// <param name="baselineScore">The baseline score to compare against.</param>
    /// <param name="candidateScore">The candidate score to compare.</param>
    /// <returns>The percentage improvement from the baseline score to the candidate score.</returns>
    private static double ComputeImprovementPercent(double baselineScore, double candidateScore)
    {
        if (baselineScore <= 0.000001)
            return 0.0;

        return ((baselineScore - candidateScore) / baselineScore) * 100.0;
    }

    // ------------------------------------------------------------
    // Uniform Score Helpers
    // ------------------------------------------------------------

    /// <summary>
    /// Represents a component of the uniform score, including its name, measured value, unit, good and bad limits, weight percentage, and meaning.
    /// It also calculates the risk percentage and weighted points based on the measured value and limits.
    /// </summary>
    private sealed class UniformScoreResult
    {
        public string Title { get; set; } = string.Empty;
        public List<UniformScoreComponent> Components { get; set; } = [];

        public double FinalScore => Components.Sum(c => c.WeightedPoints);
        public string Rating => GetUniformScoreRating(FinalScore);
    }


    /// <summary>
    /// Builds a uniform optimization score result based on the provided parameters, including average slice radius spread, adjusted low-wood risk,
    /// deepest low-wood area, maximum spindle movement, out-of-round measurement, centerline sweep, and scoring settings.
    /// </summary>
    /// <param name="title">The title of the uniform score result.</param>
    /// <param name="averageSliceRadiusSpreadMM">The average slice radius spread in millimeters.</param>
    /// <param name="adjustedLowWoodRiskRemainingPercent">The adjusted low-wood risk remaining percentage.</param>
    /// <param name="deepestLowWoodMM">The deepest low-wood area in millimeters.</param>
    /// <param name="maxSpindleMovementMM">The maximum spindle movement in millimeters.</param>
    /// <param name="outOfRoundMM">The out-of-round measurement in millimeters.</param>
    /// <param name="centerlineSweepMM">The centerline sweep in millimeters.</param>
    /// <param name="scoring">The scoring settings to use for calculating the uniform score.</param>
    /// <returns>The uniform score result based on the provided parameters.</returns>
    private static UniformScoreResult BuildUniformOptimizationScore(
        string title,
        double averageSliceRadiusSpreadMM,
        double adjustedLowWoodRiskRemainingPercent,


        double deepestLowWoodMM,
        double maxSpindleMovementMM,
        double outOfRoundMM,
        double centerlineSweepMM,
         ScoringSettings scoring)
    {
        var result = new UniformScoreResult
        {
            Title = title
        };

        AddScoreComponent(
            result,
            "Wane Balance",
            Math.Max(0.0, Inches(averageSliceRadiusSpreadMM)),
            "in",
            scoring.WaneBalanceGoodIn,
            scoring.WaneBalanceBadIn,
            scoring.WaneBalanceWeightPercent,
            "Average slice radius spread after spindle-axis adjustment. Lower means the log is more balanced for cutting.");

        AddScoreComponent(
            result,
            "Adjusted Low-Wood Risk",
            Math.Max(0.0, adjustedLowWoodRiskRemainingPercent),
            "%",
            scoring.LowWoodRiskGoodPercent,
            scoring.LowWoodRiskBadPercent,
            scoring.LowWoodRiskWeightPercent,
            "Remaining low-wood patch risk compared with the current setup. Baseline is 100%. Lower means the proposed spindle axis relieves more low-wood/wane-risk patches.");

        AddScoreComponent(
            result,
            "Deepest Low-Wood Area",
            DepthBelowSurfaceInches(deepestLowWoodMM),
            "in",
            scoring.DeepestLowWoodGoodIn,
            scoring.DeepestLowWoodBadIn,
            scoring.DeepestLowWoodWeightPercent,
            "Worst measured depth below expected surface. This is a log-condition risk; it does not move with the spindle axis.");

        AddScoreComponent(
            result,
            "Spindle Movement",
            Math.Max(0.0, Inches(maxSpindleMovementMM)),
            "in",
            scoring.SpindleMovementGoodIn,
            scoring.SpindleMovementBadIn,
            scoring.SpindleMovementWeightPercent,
            "Largest head/tail spindle move. This prevents the system from choosing an extreme move unless the gain is worth it.");

        AddScoreComponent(
            result,
            "Out-of-Round",
            Math.Max(0.0, Inches(outOfRoundMM)),
            "in",
            scoring.OutOfRoundGoodIn,
            scoring.OutOfRoundBadIn,
            scoring.OutOfRoundWeightPercent,
            "Worst measured difference between widest and narrowest diameter at one slice. This is a log-condition risk.");

        AddScoreComponent(
            result,
            "Centerline Sweep",
            Math.Max(0.0, Inches(centerlineSweepMM)),
            "in",
            scoring.CenterlineSweepGoodIn,
            scoring.CenterlineSweepBadIn,
            scoring.CenterlineSweepWeightPercent,
            "End-to-end centerline movement. Higher sweep means the log center moves more along its length.");

        return result;
    }

    /// <summary>
    /// Adds a score component to the uniform score result with the specified parameters, including name,
    /// measured value, unit, good and bad limits, weight percentage, and meaning.
    /// </summary>
    /// <param name="result">The uniform score result to which the component will be added.</param>
    /// <param name="name">The name of the score component.</param>
    /// <param name="measuredValue">The measured value of the score component.</param>
    /// <param name="unit">The unit of measurement for the score component.</param>
    /// <param name="goodLimit">The good limit for the score component.</param>
    /// <param name="badLimit">The bad limit for the score component.</param>
    /// <param name="weightPercent">The weight percentage of the score component.</param>
    /// <param name="meaning">The meaning or description of the score component.</param>
    private static void AddScoreComponent(
        UniformScoreResult result,
        string name,
        double measuredValue,
        string unit,
        double goodLimit,
        double badLimit,
        double weightPercent,
        string meaning)
    {
        result.Components.Add(new UniformScoreComponent
        {
            Name = name,
            MeasuredValue = measuredValue,
            Unit = unit,
            GoodLimit = goodLimit,
            BadLimit = badLimit,
            WeightPercent = weightPercent,
            Meaning = meaning
        });
    }


    /// <summary>
    /// Builds a uniform score report string based on the provided title and uniform score result, including the final risk score, rating, and a breakdown of each score component with its
    /// measured value, reference limits, risk percentage, weight percentage, contribution points, and meaning.
    /// </summary>
    /// <param name="title">The title of the uniform score report.</param>
    /// <param name="score">The uniform score result to be included in the report.</param>
    /// <returns>A string representing the formatted uniform score report.</returns>
    private static string BuildUniformScoreReport(string title, UniformScoreResult score)
    {
        var sb = new System.Text.StringBuilder();
        CultureInfo culture = CultureInfo.CurrentCulture;


        sb.AppendLine();
        sb.AppendLine(title);
        sb.AppendLine("Scale: 0 = best, 100 = at/beyond bad reference limit");
        sb.AppendLine(FormattableString.CurrentCulture($"Setup: {score.Title}"));
        sb.AppendLine(FormattableString.CurrentCulture($"Final Risk Score: {score.FinalScore.ToString("F1", culture)} / 100"));
        sb.AppendLine(FormattableString.CurrentCulture($"Rating: {score.Rating}"));
        sb.AppendLine();
        sb.AppendLine("Score Breakdown:");

        foreach (var component in score.Components)
        {
            sb.AppendLine(FormattableString.CurrentCulture($"{component.Name}:"));
            sb.AppendLine(FormattableString.CurrentCulture($"  Measured: {FormatScoreValue(component.MeasuredValue, component.Unit)}"));
            sb.AppendLine(FormattableString.CurrentCulture($"  Reference: Good <= {FormatScoreValue(component.GoodLimit, component.Unit)}, Bad >= {FormatScoreValue(component.BadLimit, component.Unit)}"));
            sb.AppendLine(FormattableString.CurrentCulture($"  Risk: {component.RiskPercent.ToString("F1", culture)} / 100"));
            sb.AppendLine(FormattableString.CurrentCulture($"  Weight: {component.WeightPercent:F0}%"));
            sb.AppendLine(FormattableString.CurrentCulture($"  Contribution: {component.WeightedPoints:F1} points"));
            sb.AppendLine(FormattableString.CurrentCulture($"  Meaning: {component.Meaning}"));
            sb.AppendLine();
        }

        return sb.ToString();
    }


    /// <summary>
    /// Converts a final uniform score into a rating string based on predefined thresholds, indicating whether the score is considered good, cautionary, poor, or high risk.
    /// </summary>
    /// <param name="finalScore">The final uniform score to be converted into a rating.</param>
    /// <returns>A string representing the rating corresponding to the final uniform score.</returns>
    private static string GetUniformScoreRating(double finalScore)
    {
        if (finalScore <= 25.0)
            return "GOOD";

        if (finalScore <= 50.0)
            return "CAUTION";

        if (finalScore <= 75.0)
            return "POOR";

        return "HIGH RISK";
    }

    // ------------------------------------------------------------
    // Report Meaning Helpers
    // ------------------------------------------------------------

    /// <summary>
    /// Builds a validation report for the binned radius field, including the number of slices, average radius spread, and average diameter, all formatted in inches.
    /// </summary>
    /// <param name="ovalityPercent">The ovality percentage to be evaluated.</param>
    /// <returns>A string representing the ovality rating.</returns>
    private static string GetOvalityRating(double ovalityPercent)
    {
        if (ovalityPercent < 3.0)  // If the ovality percentage is less than 3.0, return "Low" indicating a low level of ovality and good roundness
            return "Low";
        if (ovalityPercent < 7.0)  // If the ovality percentage is less than 7.0, return "Moderate" indicating a moderate level of ovality and acceptable roundness
            return "Moderate";

        return "High";
    }

    /// <summary>
    /// Converts the worst severity score into a risk rating string based on predefined thresholds, indicating whether the patch is considered low, moderate, or high risk.
    /// </summary>
    /// <param name="worstSeverityScore">The worst severity score to be converted into a risk rating.</param>
    /// <returns>A string representing the risk rating corresponding to the worst severity score.</returns>
    private static string GetPatchRiskRating(double worstSeverityScore)
    {
        if (worstSeverityScore < 50.0)  // If the worst severity score is less than 50.0, return "Low" indicating a low-risk patch
            return "Low";

        if (worstSeverityScore < 175.0)  // If the worst severity score is less than 175.0, return "Moderate" indicating a moderate-risk patch
            return "Moderate";

        return "High";
    }

    /// <summary>
    /// Builds a report explaining the meaning of the ovality measurement, including the roundness rating, worst slice difference, and implications for log shape and optimizer considerations.
    /// </summary>
    /// <param name="ovalityMM">The ovality in millimeters to be evaluated.</param>
    /// <param name="ovalityPercent">The ovality percentage to be evaluated.</param>
    /// <returns>A string representing the meaning of the ovality measurement.</returns>
    private static string BuildOvalityMeaning(double ovalityMM, double ovalityPercent)
    {
        string rating = GetOvalityRating(ovalityPercent);  // Get the roundness rating based on the ovality percentage, indicating whether the log is considered low, moderate,
                                                           // or high in terms of ovality and roundness

        return

            // Build the ovality meaning report string with the roundness rating, worst slice difference, and implications for log shape and optimizer considerations
            $"Roundness Rating: {rating}\r\n" +
            "Meaning: This checks how close the log is to being round at each slice.\r\n" +
            $"At the worst slice, the widest and narrowest directions differ by {FormatLength(ovalityMM)}.\r\n" +
            "A higher number means the log is more oval-shaped at that location.\r\n" +
            "This does not automatically mean the log is bad; it means the optimizer should account for shape when choosing the spindle position.\r\n";
    }

    /// <summary>
    /// Builds a report explaining the meaning of the surface condition measurement, including percentages of normal, low wood, and severe low wood areas, and implications for log shape and cutting risk.
    /// </summary>
    /// <param name="goodPercent">The percentage of normal surface area.</param>
    /// <param name="lowPercent">The percentage of low wood surface area.</param>
    /// <param name="severeLowPercent">The percentage of severe low wood surface area.</param>
    /// <returns>A string representing the meaning of the surface condition measurement.</returns>
    private static string BuildSurfaceConditionMeaning(double goodPercent, double lowPercent, double severeLowPercent)
    {

        // Build the surface condition meaning report string with percentages of normal, low wood, and severe low wood areas, and implications for log shape and cutting risk
        return
            "Meaning:\r\n" +
            "Normal surface means the radius is close to the expected log shape.\r\n" +
            "Low wood areas matter most because they can cause wane, missing veneer, or thin spots.\r\n" +
            "Severe low wood areas are the highest-risk cutting zones.\r\n" +
            $"\r\nSurface Summary: {goodPercent:F1}% normal, {lowPercent:F1}% low wood, {severeLowPercent:F1}% severe low wood\r\n";
    }

    /// <summary>
    /// Builds a report explaining the meaning of the patch grouping, including how connected low-wood cells are grouped into patches and the implications for veneer recovery and defect assessment.
    /// </summary>
    /// <returns>A string representing the meaning of the patch grouping.</returns>
    private static string BuildPatchMeaning()
    {
        // Build the patch meaning report string with explanations of how connected low-wood cells are grouped into patches and the implications for veneer recovery and defect assessment
        return
            "Meaning:\r\n" +
            "This section groups connected low-wood cells into patches.\r\n" +
            "A larger or deeper patch is more important because it is more likely to affect veneer recovery.\r\n" +
            "High spots are not treated as major defect patches because they usually represent extra material, not missing material.\r\n";
    }

    /// <summary>
    /// Builds a report explaining the meaning of the optimizer tradeoff, including changes in wane balance, low-wood defect risk, and internal search score, and how the recommended setup is chosen based on uniform risk score rather than wane score alone.
    /// </summary>
    /// <param name="waneDeltaMM">The change in wane balance in millimeters.</param>
    /// <param name="defectDelta">The change in low-wood defect risk.</param>
    /// <param name="finalDelta">The change in internal search score.</param>
    /// <returns>A string representing the meaning of the optimizer tradeoff.</returns>
    private static string BuildOptimizerTradeoffMeaning(double waneDeltaMM, double defectDelta, double finalDelta)
    {

        // Build the optimizer tradeoff meaning report string with explanations of changes in wane balance, low-wood defect risk, and internal search score,
        // and how the recommended setup is chosen based on uniform risk score rather than wane score alone
        string waneText = waneDeltaMM <= 0
            ? $"{FormatLength(Math.Abs(waneDeltaMM))} better"
            : $"{FormatLength(waneDeltaMM)} worse";

        // Build the defect text based on the change in low-wood defect risk, indicating whether it improved or worsened and by how many points
        string defectText = defectDelta <= 0
            ? $"improved by {Math.Abs(defectDelta):F3} points"
            : $"worse by {defectDelta:F3} points";

        // Build the final text based on the change in internal search score, indicating whether it improved or worsened and by how many points
        string finalText = finalDelta <= 0
            ? $"improved by {Math.Abs(finalDelta):F3} points"
            : $"worse by {finalDelta:F3} points";

        // Build the optimizer tradeoff meaning report string with explanations of changes in wane balance, low-wood defect risk, and internal search score,
        return
            "Meaning:\r\n" +
            "The optimizer compares basic wane balance against low-wood defect risk.\r\n" +
            $"Basic wane balance became {waneText}.\r\n" +
            $"Low-wood defect risk {defectText}.\r\n" +
            $"Internal search score {finalText}.\r\n" +
            "The recommended setup is chosen from the candidate-aware uniform risk score, not from wane score alone.\r\n";
    }

    // ------------------------------------------------------------
    // Validation Report Helpers
    // ------------------------------------------------------------

    /// <summary>
    /// Builds a validation report for the point cloud, including the number of points, ranges for X, Y, and Z coordinates, length, average radius, and average diameter, all formatted in inches.
    /// </summary>
    /// <param name="points">The list of 3D points representing the point cloud.</param>
    /// <returns>A string representing the point cloud validation report.</returns>
    private static string BuildPointCloudValidationReportInches(List<Point3D> points)
    {
        if (points == null || points.Count == 0)
            return "POINT CLOUD VALIDATION\r\nPoints: 0\r\n";

        // Compute the minimum and maximum values for X, Y, and Z coordinates in the point cloud to determine the ranges for each axis
        double minX = points.Min(p => p.X);
        double maxX = points.Max(p => p.X);
        double minY = points.Min(p => p.Y);
        double maxY = points.Max(p => p.Y);
        double minZ = points.Min(p => p.Z);
        double maxZ = points.Max(p => p.Z);
        double lengthMM = maxZ - minZ;

        // Compute the average radius in millimeters by calculating the Euclidean distance from the origin for each point in the point cloud and averaging those distances
        double avgRadiusMM = points.Average(p => Math.Sqrt(p.X * p.X + p.Y * p.Y));
        double avgDiameterMM = avgRadiusMM * 2.0;

        return

            // Build the point cloud validation report string with the computed values and formatted ranges for each axis, length, average radius, and average diameter
            "POINT CLOUD VALIDATION\r\n" +
            $"Points: {points.Count}\r\n" +
            $"X Range: {FormatLengthRange(minX, maxX)}\r\n" +
            $"Y Range: {FormatLengthRange(minY, maxY)}\r\n" +
            $"Z Range: {FormatLengthRange(minZ, maxZ)}\r\n" +
            $"Length: {FormatLength(lengthMM)}\r\n" +
            $"Avg Radius: {FormatLength(avgRadiusMM)}\r\n" +
            $"Avg Diameter: {FormatLength(avgDiameterMM)}\r\n";
    }

    // ------------------------------------------------------------
    // Build Diagnostics adaptive learning Report Helpers
    // ------------------------------------------------------------

    /// <summary>
    /// Builds an adaptive log diagnostics report based on the provided parameters, including low wood percentage,
    /// severe low wood percentage, high spot percentage, deepest low wood depth, out-of-round measurement, and centerline sweep.
    /// </summary>
    /// <param name="lowWoodPercent">The percentage of low wood detected in the log.</param>
    /// <param name="severeLowWoodPercent">The percentage of severe low wood detected in the log.</param>
    /// <param name="highSpotPercent">The percentage of high spots detected in the log.</param>
    /// <param name="deepestLowWoodMM">The depth of the deepest low wood detected in millimeters.</param>
    /// <param name="outOfRoundMM">The out-of-round measurement in millimeters.</param>
    /// <param name="centerlineSweepMM">The centerline sweep measurement in millimeters.</param>
    /// <returns>An AdaptiveLogDiagnostics object containing the diagnostics report.</returns>
    private AdaptiveLogDiagnostics BuildAdaptiveLogDiagnostics(
double lowWoodPercent,
double severeLowWoodPercent,
double highSpotPercent,
double deepestLowWoodMM,
double outOfRoundMM,
double centerlineSweepMM)
    {
        double logLengthMM = 0.0;

        if (_state.PointCloud != null && _state.PointCloud.Count > 0)
        {
            logLengthMM =
                _state.PointCloud.Max(p => p.Z) -
                _state.PointCloud.Min(p => p.Z);
        }

        return new AdaptiveLogDiagnostics
        {
            TotalFrames = _state.ScanFrames?.Count ?? 0,
            ValidFrames = _state.ScanFrames?.Count(f => f.IsValid) ?? 0,

            LogLengthMM = logLengthMM,
            AverageDiameterMM = _state.Model.AverageDiameterMM,

            CenterlineSweepMM = centerlineSweepMM,
            MaxBowMM = _state.CenterlineSweep?.MaxBowSweepMM ?? 0.0,
            OutOfRoundMM = outOfRoundMM,

            LowWoodPercent = lowWoodPercent,
            SevereLowWoodPercent = severeLowWoodPercent,
            HighSpotPercent = highSpotPercent,
            DeepestLowWoodMM = deepestLowWoodMM,

            WorstPatchSeverityScore = _state.DefectPatchAnalysis?.WorstSeverityScore ?? 0.0,
            LargestPatchCells = _state.DefectPatchAnalysis?.LargestPatch?.CellCount ?? 0
        };
    }

    /// <summary>
    /// Rebuilds the radius defect map and related data structures using the
    /// provided adaptive scoring profile, updating the state with the new defect map, patches, analysis, and heatmap.
    /// </summary>
    /// <param name="profile">The adaptive scoring profile to use for rebuilding the radius defects.</param>
    /// <exception cref="InvalidOperationException">Thrown if the adaptive scoring profile is missing or if the bin radius field is not initialized.</exception>
    private void RebuildRadiusDefectsWithAdaptiveProfile(AdaptiveScoringProfile profile)
    {
        if (profile == null)
            throw new InvalidOperationException("Adaptive scoring profile is missing.");

        if (_state.BinnedRadiusField == null || _state.BinnedRadiusField.Count == 0)
            throw new InvalidOperationException("Bin radius field first.");

        _state.RadiusDefectMap = RadiusDefectMapBuilder.Build(
            _state.BinnedRadiusField,
            lowThresholdMM: profile.Defects.LowRadiusThresholdMM,
            severeLowThresholdMM: profile.Defects.SevereLowRadiusThresholdMM,
            highThresholdMM: profile.Defects.HighRadiusThresholdMM);

        _state.RadiusDefectPatches =
            RadiusDefectPatchBuilder.Build(_state.RadiusDefectMap);

        _state.DefectPatchAnalysis =
            DefectPatchAnalyzer.Analyze(_state.RadiusDefectPatches);

        _state.RadiusDefectHeatmap?.Dispose();
        _state.RadiusDefectHeatmap =
            RadiusDefectHeatmapBuilder.Build(_state.RadiusDefectMap);

        _state.DiagnosticsReport +=
            "\r\nADAPTIVE DEFECT MAP REBUILD\r\n" +
            "Status: Rebuilt surface condition map and low-wood patches using adaptive per-log thresholds.\r\n";
    }

    // ------------------------------------------------------------
    // Final Recommendation Helper
    // ------------------------------------------------------------

    /// <summary>
    /// Builds the final spindle recommendation based on the optimizer results, machine warnings, and movement constraints.
    /// </summary>
    /// <param name="finalSetup">The final optimizer setup result.</param>
    /// <param name="machineWarnings">A list of machine warnings.</param>
    /// <param name="machineMoveAllowed">Indicates whether machine movement is allowed.</param>
    /// <param name="baselineRiskScore">The baseline risk score.</param>
    private static FinalSpindleRecommendation BuildFinalSpindleRecommendation(
ChargerOptimizationResult finalSetup,
List<string> machineWarnings,
bool machineMoveAllowed,
double baselineRiskScore,
string baselineRiskRating,
double optimizedRiskScore,
string optimizedRiskRating,
double riskScoreReductionPercent,
double baselineWaneScoreMM,
double baselineInternalSearchScore,
double baselineDefectPenalty,
double maxSpindleMovementMM)
    {
        if (finalSetup == null)
            throw new InvalidOperationException("Final optimizer setup is missing.");

        var move = finalSetup.SpindleMovement ?? throw new InvalidOperationException("Final optimizer setup is missing spindle movement.");  // Get the spindle movement from the final optimizer setup to determine the recommended head/tail positions

        bool isRecommended = finalSetup.IsRecommended;

        string action;  // Initialize a string variable to hold the recommended action based on the optimizer results and machine constraints
        string reason;  // Initialize a string variable to hold the reason for the recommended action based on the optimizer results and machine constraints

        // Determine the recommended action and reason based on whether the optimized setup is recommended, whether machine movement is allowed, and whether there are any machine warnings
        if (!machineMoveAllowed)
        {
            action = "Keep current charger setup.";
            reason = "Best tested setup was blocked because one or more spindle movements exceeded machine limits.";
        }
        else if (isRecommended && machineWarnings.Count > 0) // If the optimized setup is recommended but there are machine warnings, suggest using the optimized setup with review and provide the reason for the recommendation
        {
            action = "Use optimized spindle-axis setup with review.";
            reason = "Optimized setup improved the risk score, but one or more movements are above the warning level.";
        }
        else if (isRecommended) // If the optimized setup is recommended and there are no machine warnings, suggest using the optimized setup and provide the reason for the recommendation
        {
            action = "Use optimized spindle-axis setup.";
            reason = "Optimized setup improved the risk score and stayed within machine limits.";
        }
        else  // If the optimized setup is not recommended, suggest keeping the current charger setup and provide the reason for the recommendation
        {
            action = "Keep current charger setup.";
            reason = "Best tested setup did not improve enough to pass the minimum improvement threshold.";
        }

        return new FinalSpindleRecommendation
        {
            OptimizationMode = SpindleOptimizationMode.PenaltyScored,
            SelectedStrategy = "Penalty-Scored Spindle Optimizer",
            IsRecommended = isRecommended,
            MachineMoveAllowed = machineMoveAllowed,

            RecommendedAction = action,
            Reason = reason,

            Warnings = [.. machineWarnings],

            RiskScore = optimizedRiskScore,
            RiskRating = optimizedRiskRating,

            BaselineRiskScore = baselineRiskScore,
            BaselineRiskRating = baselineRiskRating,

            RiskScoreReductionPercent = riskScoreReductionPercent,

            CommandTailXMM = isRecommended ? -move.TailMoveXMM : 0.0,
            CommandTailYMM = isRecommended ? -move.TailMoveYMM : 0.0,
            CommandHeadXMM = isRecommended ? -move.HeadMoveXMM : 0.0,
            CommandHeadYMM = isRecommended ? -move.HeadMoveYMM : 0.0,

            BestTestedTailXMM = move.TailMoveXMM,
            BestTestedTailYMM = move.TailMoveYMM,
            BestTestedHeadXMM = move.HeadMoveXMM,
            BestTestedHeadYMM = move.HeadMoveYMM,

            MaxSpindleMovementMM = maxSpindleMovementMM,

            BaselineWaneScoreMM = baselineWaneScoreMM,
            OptimizedWaneScoreMM = finalSetup.WaneScore,

            BaselineInternalSearchScore = baselineInternalSearchScore,
            OptimizedInternalSearchScore = finalSetup.FinalScore,

            BaselineDefectPenalty = baselineDefectPenalty,
            OptimizedDefectPenalty = finalSetup.DefectPenalty,

            TestedCandidates = finalSetup.TestedCandidates,
            CoarseCandidates = finalSetup.CoarseCandidates,
            MediumCandidates = finalSetup.MediumCandidates,
            FineCandidates = finalSetup.FineCandidates
        };
    }

    // ------------------------------------------------------------
    // Timing Helpers
    // ------------------------------------------------------------

    /// <summary>
    /// Runs a given action and measures the time it takes to execute, recording the elapsed time in milliseconds along with the step name in the pipeline state for later reporting.
    /// </summary>
    /// <param name="stepName">The name of the pipeline step.</param>
    /// <param name="action">The action to execute and time.</param>
    private void RunTimed(string stepName, Action action)
    {
        double startOffsetMS = _runClock.Elapsed.TotalMilliseconds;
        int depth = TimingDepth.Value;
        Stopwatch stopwatch = Stopwatch.StartNew();
        TimingDepth.Value = depth + 1;

        try
        {
            action();
        }
        finally
        {
            stopwatch.Stop();
            TimingDepth.Value = depth;
            RecordTiming(stepName, stopwatch.Elapsed.TotalMilliseconds, startOffsetMS, depth);
        }
    }

    /// <summary>
    /// Clock for the current processing run; restarted by RunCurrentFramesPipeline.
    /// </summary>
    private readonly Stopwatch _runClock = new();

    /// <summary>
    /// How deeply the current step is nested inside other timed steps. Flows into
    /// parallel work, so per-frame steps are recorded as part of their stage.
    /// </summary>
    private static readonly AsyncLocal<int> TimingDepth = new();

    /// <summary>
    /// Guards <see cref="PipelineState.TimingEntries"/>. RunTimed is called from
    /// inside the parallel per-frame loop, and List.Add is not thread-safe:
    /// two frames adding at the same time can lose an entry or throw, and a
    /// throw there used to mark a good frame invalid.
    /// </summary>
    private static readonly object TimingEntriesLock = new();

    /// <summary>
    /// Adds one timing entry. Safe to call from several threads at once.
    /// </summary>
    private void RecordTiming(
        string stepName,
        double elapsedMilliseconds,
        double startOffsetMilliseconds,
        int depth)
    {
        lock (TimingEntriesLock)
        {
            _state.TimingEntries.Add(new PipelineTimingEntry
            {
                StepName = stepName,
                ElapsedMilliseconds = elapsedMilliseconds,
                StartOffsetMilliseconds = startOffsetMilliseconds,
                Depth = depth
            });
        }
    }

    /// <summary>
    /// Runs a given function and measures the time it takes to execute, recording the elapsed time in milliseconds
    /// along with the step name in the pipeline state for later reporting, and returns the result of the function.
    /// </summary>
    /// <typeparam name="T">The return type of the function.</typeparam>
    /// <param name="stepName">The name of the pipeline step.</param>
    /// <param name="func">The function to execute and time.</param>
    /// <returns>The result of the function.</returns>
    private T RunTimed<T>(string stepName, Func<T> func)
    {
        double startOffsetMS = _runClock.Elapsed.TotalMilliseconds;
        int depth = TimingDepth.Value;
        Stopwatch stopwatch = Stopwatch.StartNew();
        TimingDepth.Value = depth + 1;

        try
        {
            return func();
        }
        finally
        {
            stopwatch.Stop();
            TimingDepth.Value = depth;
            RecordTiming(stepName, stopwatch.Elapsed.TotalMilliseconds, startOffsetMS, depth);
        }
    }

    /// <summary>
    /// Timing report in real elapsed (wall-clock) time.
    ///
    /// Frames are processed in parallel, so adding up per-frame times gives CPU
    /// time, not elapsed time (the old "Total Time" did that and read several
    /// times too high). Stages are listed in the order they ran with their real
    /// elapsed time; steps timed inside a stage are indented under it and are
    /// part of that stage's time. Per-frame work is shown separately as CPU time.
    /// </summary>
    internal static string BuildTimingReport(
        List<PipelineTimingEntry> entries,
        double processingElapsedMS)
    {
        if (entries == null || entries.Count == 0)
            return string.Empty;

        static bool IsPerFrame(PipelineTimingEntry entry) =>
            entry.StepName.StartsWith("Frame", StringComparison.OrdinalIgnoreCase) &&
            entry.StepName.Contains(" - ", StringComparison.Ordinal);

        var report = new StringBuilder();

        report.Append("\r\nPIPELINE TIMING REPORT (real elapsed time)\r\n");
        StringBuilder stringBuilder = report.Append(
            CultureInfo.InvariantCulture,
            $"Processing time, frames in hand to position chosen: {processingElapsedMS / 1000.0:F2} s\r\n");

        List<PipelineTimingEntry> stages = [.. entries
            .Where(entry => !IsPerFrame(entry))
            .OrderBy(entry => entry.StartOffsetMilliseconds)
            .ThenBy(entry => entry.Depth)];

        if (stages.Count > 0)
        {
            report.Append("\r\nStages in the order they ran (indented lines are part of the line above):\r\n");

            foreach (PipelineTimingEntry entry in stages)
            {
                string indent = new(' ', 2 + 2 * Math.Max(0, entry.Depth));
                string name = indent + entry.StepName;
                StringBuilder stringBuilder1 = report.Append(
                    CultureInfo.InvariantCulture,
                    $"{name.PadRight(64)} {entry.ElapsedSeconds,7:F2} s   (started at {entry.StartOffsetMilliseconds / 1000.0:F2} s)\r\n");
            }

            double topLevelMS = stages
                .Where(entry => entry.Depth == 0)
                .Sum(entry => entry.ElapsedMilliseconds);
            double otherMS = Math.Max(0.0, processingElapsedMS - topLevelMS);

            report.Append(
                CultureInfo.InvariantCulture,
                $"{"  Between stages (reports, hand-over)".PadRight(64)} {otherMS / 1000.0,7:F2} s\r\n");
        }

        var perFrame = entries
            .Where(IsPerFrame)
            .GroupBy(entry => entry.StepName[(entry.StepName.IndexOf(" - ", StringComparison.Ordinal) + 3)..])
            .OrderByDescending(group => group.Sum(entry => entry.ElapsedMilliseconds))
            .ToList();

        if (perFrame.Count > 0)
        {
            report.Append(
                "\r\nPer-frame work inside \"Process Rotating Scan Frames\" (CPU time added up over all frames).\r\n" +
                $"Frames run in parallel on {Environment.ProcessorCount} logical processors, so these add up to more than the stage's real time:\r\n");

            foreach (var group in perFrame)
            {
                double totalMS = group.Sum(entry => entry.ElapsedMilliseconds);
                int count = group.Count();

                report.Append(
                    CultureInfo.InvariantCulture,
                    $"  {group.Key} ({count} frames): {totalMS / 1000.0:F2} s CPU, {totalMS / Math.Max(1, count):F1} ms per frame\r\n");
            }
        }

        return report.ToString();
    }

    /// <summary>
    /// Runs every processing stage that operates on the frames currently held
    /// in PipelineState. Both live acquisition and folder-based processing use
    /// this method so stage order cannot drift between entry points.
    /// </summary>
    /// <param name="lookupTable">The scanner lookup table containing calibration data.</param>
    /// <param name="token">A cancellation token to observe while waiting for the task to complete.</param>
    /// <param name="progress">An optional action to report progress messages.</param>
    internal void RunCurrentFramesPipeline(
        ScannerLookupTable lookupTable,
        CancellationToken token,
        Action<string>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(lookupTable);

        if (!lookupTable.IsValid || lookupTable.LengthPixelsPerMM <= 0)
        {
            throw new InvalidOperationException(
                "Scanner calibration is invalid or incomplete.");
        }

        _runClock.Restart();
        TimingDepth.Value = 0;

        try
         {
             RunAllStages(lookupTable, progress, token);
         }
         finally
         {
             _runClock.Stop();
         }

        _state.ProcessingElapsedSeconds = _runClock.Elapsed.TotalSeconds;

        if (_state.Settings.Diagnostics.IncludeTimingReport &&
            _state.Settings.Diagnostics.IncludeEngineeringDetails)
        {
            List<PipelineTimingEntry> snapshot;

            lock (TimingEntriesLock)
            {
                snapshot = [.. _state.TimingEntries];
            }

            _state.DiagnosticsReport += BuildTimingReport(
                snapshot,
                _runClock.Elapsed.TotalMilliseconds);
        }
    }

    /// <summary>
    /// The ordered stage list of <see cref="RunCurrentFramesPipeline"/>.
    /// </summary>
    private void RunAllStages(
        ScannerLookupTable lookupTable,
        Action<string>? progress,
        CancellationToken token)
    {
        void RunStage(
            string timingName,
            string progressMessage,
            Action action)
        {
            token.ThrowIfCancellationRequested();
            progress?.Invoke(progressMessage);
            RunTimed(timingName, action);
        }

        RunStage(
            "Apply Scanner Calibration",
            "Applying scanner calibration...",
            () =>
            {
                _state.Calibration = new CalibrationResult
                {
                    LengthPixelsPerMM = lookupTable.LengthPixelsPerMM
                };

                _state.ScannerCalibrationSnapshot = new ScannerLookupTable
                {
                    SchemaVersion = lookupTable.SchemaVersion,
                    CalibrationId = lookupTable.CalibrationId,
                    CreatedUtc = lookupTable.CreatedUtc,
                    OperatorName = lookupTable.OperatorName,
                    MachineName = lookupTable.MachineName,
                    SoftwareVersion = lookupTable.SoftwareVersion,
                    HeadCameraServerName = lookupTable.HeadCameraServerName,
                    TailCameraServerName = lookupTable.TailCameraServerName,
                    HeadCameraConfigurationSha256 = lookupTable.HeadCameraConfigurationSha256,
                    TailCameraConfigurationSha256 = lookupTable.TailCameraConfigurationSha256,
                    AcquisitionGeometrySha256 = lookupTable.AcquisitionGeometrySha256,
                    ImageWidth = lookupTable.ImageWidth,
                    ImageHeight = lookupTable.ImageHeight,
                    LengthPixelsPerMM = lookupTable.LengthPixelsPerMM,
                    MinimumCalibratedRadiusMM = lookupTable.MinimumCalibratedRadiusMM,
                    MaximumCalibratedRadiusMM = lookupTable.MaximumCalibratedRadiusMM,
                    References = [.. lookupTable.References
                        .Select(reference => new CalibrationReferenceSummary
                        {
                            RadiusBandId = reference.RadiusBandId,
                            Name = reference.Name,
                            DiameterMM = reference.DiameterMM,
                            RadiusMM = reference.RadiusMM,
                            ImageWidth = reference.ImageWidth,
                            ImageHeight = reference.ImageHeight,
                            CapturedFrameCount = reference.CapturedFrameCount,
                            AcceptedPointCount = reference.AcceptedPointCount,
                            MeanDetectedLaserY = reference.MeanDetectedLaserY,
                            LaserYStandardDeviation = reference.LaserYStandardDeviation,
                            CapturedUtc = reference.CapturedUtc
                        })],
                    Quality = new CalibrationQualitySummary
                    {
                        ReferenceCount = lookupTable.Quality.ReferenceCount,
                        LookupPointCount = lookupTable.Quality.LookupPointCount,
                        MinimumRadiusMM = lookupTable.Quality.MinimumRadiusMM,
                        MaximumRadiusMM = lookupTable.Quality.MaximumRadiusMM,
                        MeanRepeatabilityErrorMM = lookupTable.Quality.MeanRepeatabilityErrorMM,
                        MaximumRepeatabilityErrorMM = lookupTable.Quality.MaximumRepeatabilityErrorMM,
                        IndependentValidationCompleted = lookupTable.Quality.IndependentValidationCompleted,
                        IndependentValidationMeanErrorMM = lookupTable.Quality.IndependentValidationMeanErrorMM,
                        IndependentValidationMaximumErrorMM = lookupTable.Quality.IndependentValidationMaximumErrorMM,
                        Status = lookupTable.Quality.Status,
                        Notes = lookupTable.Quality.Notes
                    },
                    Points = [.. lookupTable.Points
                        .Select(point => new LookupPoint
                        {
                            X = point.X,
                            LaserY = point.LaserY,
                            RadiusMM = point.RadiusMM,
                            RadiusBandId = point.RadiusBandId,
                            SourceName = point.SourceName
                        })]
                };
            });

        RunStage(
            "Process Rotating Scan Frames",
            "Processing rotating scan frames...",
            () => ProcessRotatingScanFrames(lookupTable, token));
        RunStage(
            "Build Rotating Scan Point Cloud",
            "Building point cloud...",
            () => BuildRotatingScanPointCloud(token));
        RunStage(
            "Fit Rotating Scan Centerline",
            "Fitting centerline...",
            () => FitRotatingScanCenterline(token));
        RunStage(
            "Build Radius Field",
            "Building radius field...",
            () => BuildRadiusField(token));
        RunStage(
            "Bin Rotating Scan Radius Field",
            "Binning radius field...",
            () => BinRotatingScanRadiusField(token));

        // Ovality and radius defects run one after the other. Both append a
        // section to _state.DiagnosticsReport; run in parallel, the two
        // appends could overlap and one section was silently lost. Both
        // stages take only milliseconds, so nothing is gained by overlapping.
        RunStage(
            "Analyze Ovality",
            "Analyzing ovality...",
            () => AnalyzeOvality(token));
        RunStage(
            "Analyze Radius Defects",
            "Analyzing radius defects...",
            () => AnalyzeRadiusDefects(token));

        RunStage(
            "Analyze Defect Patches",
            "Analyzing defect patches...",
            () => AnalyzeDefectPatches(token));

        if (_state.Settings.OptimizationMode ==
            SpindleOptimizationMode.PenaltyScored)
        {
            RunStage(
                "Optimize Spindle Axis",
                "Running penalty-scored spindle optimization...",
                () => OptimizeSpindleAxis(token));

            RunStage(
                "Build Unwrapped Peel View",
                "Building selected peel visualization...",
                () => BuildUnwrappedPeelView(token));
        }
        else if (_state.Settings.OptimizationMode ==
                 SpindleOptimizationMode.RecoveryOptimized)
        {
            RunStage(
                "Generate Penalty Search Candidate",
                "Generating penalty-search recovery candidate...",
                () => OptimizeSpindleAxis(token));

            RunStage(
                "Compare All Strategies By Veneer Recovery",
                "Comparing seed strategies and direct recovery refinement by virtual peel...",
                () => OptimizeByVeneerRecovery(token));
        }
        else
        {
            RunStage(
                "Optimize Inscribed Geometry And Compare Peels",
                "Running geometry-only cylinder/oval optimization...",
                () => OptimizeInscribedGeometry(token));
        }
    }


}
