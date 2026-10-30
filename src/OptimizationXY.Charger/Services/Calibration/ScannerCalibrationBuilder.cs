using System.Reflection;
using System.Security.Cryptography;
using Emgu.CV;
using OptimizationXY.Charger.Models.Calibration;
using OptimizationXY.Charger.Models.Settings;
using OptimizationXY.Charger.Services.Preprocessing;


// =============================================================================
// ScannerCalibrationBuilder.cs - turns reference images into a lookup table
// -----------------------------------------------------------------------------
// For each reference (small tube, large tube, home cable at radius 0):
//   ExtractReference*  - detect the laser line in every frame with the SAME
//                        preprocessing/detector as production, then for each
//                        image column average the laser row over the frames
//                        (after rejecting outliers). Each column becomes one
//                        LookupPoint "(column, laser row) -> known radius".
//                        About 25% of the frames are held back to test with.
// Then BuildTable puts all references together, stamps the calibration
// fingerprint (camera snapshot hashes + image geometry hash), measures the
// held-out repeatability error, and validates the result.
// This class only BUILDS a table; CalibrationPackageStore saves/activates it.
// =============================================================================
namespace OptimizationXY.Charger.Services.Calibration;

/// <summary>
/// Builds and checks scanner calibration data: extracts reference captures,
/// constructs the lookup table and summarises laser detection quality.
/// </summary>
public static class ScannerCalibrationBuilder
{

    /// <summary>
    /// Extracts a calibration reference from a batch of concatenated frames, detecting the laser stripe and computing stable lookup points.
    /// </summary>
    /// <param name="batch">The batch of concatenated frames to process.</param>
    /// <param name="referenceName">The name of the calibration reference.</param>
    /// <param name="measuredDiameterMM">The measured diameter in millimeters.</param>
    /// <param name="radiusBandId">The radius band identifier.</param>
    /// <returns>The extracted <see cref="CalibrationReferenceCapture"/>.</returns>
    /// <exception cref="ArgumentException">Thrown if the reference name is null or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if the measured diameter is not a positive finite value or the radius band ID is negative or zero.</exception>
    /// <exception cref="InvalidOperationException">Thrown if there are not enough concatenated frames or if laser detection fails on sufficient frames.</exception>
    public static CalibrationReferenceCapture ExtractReference(
        CalibrationCaptureBatch batch,
        string referenceName,
        double measuredDiameterMM,
        int radiusBandId)
    {
        ArgumentNullException.ThrowIfNull(batch);

        return ExtractReferenceCore(
            batch.ConcatenatedFrames,
            batch.CapturedUtc,
            referenceName,
            measuredDiameterMM,
            radiusBandId,
            minimumFrames: 3,
            isHomeAxisReference: false);
    }

    /// <summary>
    /// Extracts a calibration reference from saved, already concatenated
    /// reference images (for example the small-tube, large-tube and cable
    /// images from another calibration). Uses the same laser detection as a
    /// live capture. One image per reference is accepted; with fewer than three
    /// images there is no held-out repeatability check for that reference.
    /// </summary>
    /// <param name="frames">The concatenated grayscale reference images.</param>
    /// <param name="capturedUtc">When the images were captured.</param>
    /// <param name="referenceName">The name of the calibration reference.</param>
    /// <param name="measuredDiameterMM">The measured diameter in millimeters; 0 for the home-position reference.</param>
    /// <param name="radiusBandId">The radius band identifier.</param>
    /// <param name="isHomeAxisReference">
    /// True for the home-position reference (the cable on the spindle axis),
    /// which is stored as a band at radius 0.
    /// </param>
    public static CalibrationReferenceCapture ExtractReferenceFromImages(
        IReadOnlyList<Mat> frames,
        DateTime capturedUtc,
        string referenceName,
        double measuredDiameterMM,
        int radiusBandId,
        bool isHomeAxisReference)
    {
        ArgumentNullException.ThrowIfNull(frames);

        return ExtractReferenceCore(
            frames,
            capturedUtc,
            referenceName,
            measuredDiameterMM,
            radiusBandId,
            minimumFrames: 1,
            isHomeAxisReference);
    }

    /// <summary>
    /// Shared work of both ExtractReference methods: detect the laser line in each
    /// frame, split frames into training and held-out sets, and build one lookup
    /// point per image column from the training frames.
    /// </summary>
    private static CalibrationReferenceCapture ExtractReferenceCore(
        IReadOnlyList<Mat> concatenatedFrames,
        DateTime capturedUtc,
        string referenceName,
        double measuredDiameterMM,
        int radiusBandId,
        int minimumFrames,
        bool isHomeAxisReference)
    {
        if (string.IsNullOrWhiteSpace(referenceName))
        {
            throw new ArgumentException(
                "A reference name is required.",
                nameof(referenceName));
        }

        if (isHomeAxisReference)
        {
            if (measuredDiameterMM != 0.0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(measuredDiameterMM),
                    "The home-position reference is the spindle axis and must use diameter 0.");
            }
        }
        else if (!double.IsFinite(measuredDiameterMM) || measuredDiameterMM <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(measuredDiameterMM),
                "The measured diameter must be a positive finite value.");
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(radiusBandId);

        if (concatenatedFrames.Count < minimumFrames)
        {
            throw new InvalidOperationException(minimumFrames == 3
                ? "At least three concatenated frames are required for calibration extraction."
                : $"At least {minimumFrames} reference image(s) are required for calibration extraction.");
        }

        int imageWidth = concatenatedFrames[0].Width;
        int imageHeight = concatenatedFrames[0].Height;

        if (imageWidth <= 0 || imageHeight <= 0 ||
            concatenatedFrames.Any(frame =>
                frame.Width != imageWidth || frame.Height != imageHeight))
        {
            throw new InvalidOperationException(
                "All calibration frames must have the same positive concatenated image dimensions.");
        }

        double knownRadiusMM = measuredDiameterMM / 2.0;
        var profiles = new List<IReadOnlyDictionary<int, double>>();

        foreach (Mat frame in concatenatedFrames)
        {
            using Mat binary = ImagePreprocessingService.BuildBinary(frame);
            var profile = LaserStripeDetector.Detect(binary)
                .ToDictionary(point => point.X, point => point.LaserCenterY);

            // A frame needs at least 30 laser columns to be usable.
            if (profile.Count >= 30)
            {
                profiles.Add(profile);
            }
        }

        if (profiles.Count < minimumFrames)
        {
            throw new InvalidOperationException(minimumFrames == 3
                ? "Laser detection succeeded on fewer than three calibration frames. Check laser brightness, exposure, focus, and obstruction."
                : $"Laser detection succeeded on fewer than {minimumFrames} reference image(s). Check the image shows the laser line.");
        }

        List<IReadOnlyDictionary<int, double>> trainingProfiles;
        IReadOnlyList<IReadOnlyDictionary<int, double>> heldOutProfiles;
        int minimumSamplesPerColumn;

        if (profiles.Count >= 3)
        {
            // Use about 75% of frames to build the points and keep the rest back to
            // measure repeatability. A column must appear in at least half of the
            // training frames to be trusted.
            int trainingCount = Math.Max(2, (int)Math.Ceiling(profiles.Count * 0.75));
            trainingCount = Math.Min(trainingCount, profiles.Count - 1);

            trainingProfiles = [.. profiles.Take(trainingCount)];
            heldOutProfiles = [.. profiles.Skip(trainingCount)];

            minimumSamplesPerColumn = Math.Max(
                2,
                (int)Math.Ceiling(trainingProfiles.Count * 0.5));
        }
        else
        {
            // Only reachable for saved reference images (one or two per
            // reference): every image is used, none is held out.
            trainingProfiles = profiles;
            heldOutProfiles = [];
            minimumSamplesPerColumn = profiles.Count;
        }

        var allColumns = trainingProfiles
            .SelectMany(profile => profile.Keys)
            .Distinct()
            .OrderBy(x => x);

        var points = new List<LookupPoint>();
        var acceptedLaserYValues = new List<double>();

        foreach (int x in allColumns)
        {
            double[] values = [.. trainingProfiles
                .Where(profile => profile.ContainsKey(x))
                .Select(profile => profile[x])];

            if (values.Length < minimumSamplesPerColumn)
            {
                continue;
            }

            // Drop laser rows that disagree with the other frames in this column, then
            // average what is left.
            double[] filtered = RejectOutliers(values);
            if (filtered.Length < minimumSamplesPerColumn)
            {
                continue;
            }

            double laserY = filtered.Average();
            acceptedLaserYValues.Add(laserY);

            points.Add(new LookupPoint
            {
                X = x,
                LaserY = laserY,
                RadiusMM = knownRadiusMM,
                RadiusBandId = radiusBandId,
                SourceName = referenceName.Trim()
            });
        }

        if (points.Count < 30)
        {
            throw new InvalidOperationException(
                $"Only {points.Count} stable laser columns were extracted. At least 30 are required.");
        }

        var heldOutSamples = heldOutProfiles
            .SelectMany(profile => profile.Select(pair => new CalibrationLaserSample
            {
                X = pair.Key,
                LaserY = pair.Value
            }))
            .Where(sample => double.IsFinite(sample.LaserY))
            .ToList();

        double meanLaserY = acceptedLaserYValues.Average();
        double standardDeviation = StandardDeviation(acceptedLaserYValues);

        return new CalibrationReferenceCapture
        {
            Summary = new CalibrationReferenceSummary
            {
                RadiusBandId = radiusBandId,
                Name = referenceName.Trim(),
                DiameterMM = measuredDiameterMM,
                RadiusMM = knownRadiusMM,
                ImageWidth = imageWidth,
                ImageHeight = imageHeight,
                CapturedFrameCount = profiles.Count,
                AcceptedPointCount = points.Count,
                MeanDetectedLaserY = meanLaserY,
                LaserYStandardDeviation = standardDeviation,
                CapturedUtc = capturedUtc
            },
            Points = points,
            HeldOutSamples = heldOutSamples
        };
    }

    /// <summary>
    /// Builds a scanner lookup table from the provided calibration references, camera snapshots, and other metadata.
    /// </summary>
    /// <param name="settings">The application settings.</param>
    /// <param name="operatorName">The name of the operator performing the calibration.</param>
    /// <param name="lengthPixelsPerMM">The length in pixels per millimeter.</param>
    /// <param name="references">The calibration reference captures.</param>
    /// <param name="headCameraSnapshot">The snapshot of the head camera configuration.</param>
    /// <param name="tailCameraSnapshot">The snapshot of the tail camera configuration.</param>
    /// <param name="independentValidationCompleted">Indicates whether independent validation was completed.</param>
    /// <param name="independentValidationMeanErrorMM">The mean error in millimeters from independent validation.</param>
    /// <param name="independentValidationMaximumErrorMM">The maximum error in millimeters from independent validation.</param>
    /// <param name="notes">Additional notes for the calibration.</param>
    /// <returns>The constructed <see cref="ScannerLookupTable"/>.</returns>
    /// <exception cref="ArgumentException">Thrown if the operator name is missing.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if lengthPixelsPerMM is not positive and finite.</exception>
    /// <exception cref="InvalidOperationException">Thrown if camera snapshots are missing, fewer than two different radii are given, or the references have different image sizes.</exception>
    /// <exception cref="InvalidDataException">Thrown if the constructed table is not valid for production use.</exception>
    public static ScannerLookupTable BuildTable(
        AppSettings settings,
        string operatorName,
        double lengthPixelsPerMM,
        IReadOnlyList<CalibrationReferenceCapture> references,
        byte[] headCameraSnapshot,
        byte[] tailCameraSnapshot,
        bool independentValidationCompleted,
        double? independentValidationMeanErrorMM,
        double? independentValidationMaximumErrorMM,
        string notes)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(references);

        if (string.IsNullOrWhiteSpace(operatorName))
        {
            throw new ArgumentException(
                "The calibration operator name is required.",
                nameof(operatorName));
        }

        if (!double.IsFinite(lengthPixelsPerMM) || lengthPixelsPerMM <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(lengthPixelsPerMM),
                "Length pixels per millimeter must be positive and finite.");
        }

        if (headCameraSnapshot is not { Length: > 0 } ||
            tailCameraSnapshot is not { Length: > 0 })
        {
            throw new InvalidOperationException(
                "The exact loaded head and tail camera-configuration snapshots are required to build a production calibration.");
        }

        if (references.Count < 2)
        {
            throw new InvalidOperationException(
                "At least two different known diameters are required to create a radial lookup table.");
        }

        int[] widths = [.. references
            .Select(reference => reference.Summary.ImageWidth)
            .Distinct()];
        int[] heights = [.. references
            .Select(reference => reference.Summary.ImageHeight)
            .Distinct()];

        if (widths.Length != 1 || heights.Length != 1 ||
            widths[0] <= 0 || heights[0] <= 0)
        {
            throw new InvalidOperationException(
                "All known-reference captures must use identical positive concatenated image dimensions.");
        }

        double[] distinctRadii = [.. references
            .Select(reference => reference.Summary.RadiusMM)
            .DistinctBy(radius => Math.Round(radius, 4))];

        if (distinctRadii.Length < 2)
        {
            throw new InvalidOperationException(
                "The calibration references must contain at least two different known radii.");
        }

        // e.g. "CAL-20261030-091500-1a2b3c4d5e6" (cut to 32 characters).
        string calibrationId =
            $"CAL-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}"[..32];

        var table = new ScannerLookupTable
        {
            CalibrationId = calibrationId,
            CreatedUtc = DateTime.UtcNow,
            OperatorName = operatorName.Trim(),
            MachineName = Environment.MachineName,
            SoftwareVersion = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion ??
                Assembly.GetExecutingAssembly().GetName().Version?.ToString() ??
                "unknown",
            HeadCameraServerName = settings.Acquisition.HeadCamera.ServerName,
            TailCameraServerName = settings.Acquisition.TailCamera.ServerName,
            HeadCameraConfigurationSha256 = ComputeSha256(headCameraSnapshot),
            TailCameraConfigurationSha256 = ComputeSha256(tailCameraSnapshot),
            AcquisitionGeometrySha256 =
                ScannerCalibrationCompatibility.ComputeAcquisitionGeometrySha256(
                    settings.Acquisition),
            ImageWidth = widths[0],
            ImageHeight = heights[0],
            LengthPixelsPerMM = lengthPixelsPerMM,
            MinimumCalibratedRadiusMM = distinctRadii.Min(),
            MaximumCalibratedRadiusMM = distinctRadii.Max(),
            References = [.. references.Select(reference => reference.Summary)],
            Points = [.. references.SelectMany(reference => reference.Points)]
        };

        (double meanRepeatability, double maxRepeatability) =
            EvaluateHeldOutRepeatability(table, references);

        table.Quality = new CalibrationQualitySummary
        {
            ReferenceCount = references.Count,
            LookupPointCount = table.Points.Count,
            MinimumRadiusMM = table.MinimumCalibratedRadiusMM,
            MaximumRadiusMM = table.MaximumCalibratedRadiusMM,
            MeanRepeatabilityErrorMM = meanRepeatability,
            MaximumRepeatabilityErrorMM = maxRepeatability,
            IndependentValidationCompleted = independentValidationCompleted,
            IndependentValidationMeanErrorMM = independentValidationMeanErrorMM,
            IndependentValidationMaximumErrorMM = independentValidationMaximumErrorMM,
            Status = independentValidationCompleted
                ? "ValidatedPendingEngineeringApproval"
                : "CommissioningOnly",
            Notes = notes?.Trim() ?? string.Empty
        };

        if (!table.IsValid)
        {
            throw new InvalidDataException(
                "The generated lookup table failed structural validation.");
        }

        return table;
    }

    /// <summary>
    /// Setup-wizard laser check: runs the production laser detector on each frame
    /// of a capture and summarises how many columns and which rows it found.
    /// </summary>
    /// <param name="batch">The calibration capture batch to analyze.</param>
    /// <returns>A summary of the laser detection results.</returns>
    /// <exception cref="ArgumentNullException">Thrown if the batch is null.</exception>
    public static LaserDetectionSummary AnalyzeLaser(
        CalibrationCaptureBatch batch)
    {
        ArgumentNullException.ThrowIfNull(batch);

        var columnCounts = new List<int>();
        var yValues = new List<double>();
        int validFrames = 0;
        int width = 0;
        int height = 0;

        foreach (Mat frame in batch.ConcatenatedFrames)
        {
            width = frame.Width;
            height = frame.Height;

            try
            {
                using Mat binary = ImagePreprocessingService.BuildBinary(frame);
                var profile = LaserStripeDetector.Detect(binary);
                columnCounts.Add(profile.Count);
                yValues.AddRange(profile.Select(point => point.LaserCenterY));
                validFrames++;
            }
            catch (InvalidOperationException)
            {
                // Invalid frames are reported in the summary rather than
                // terminating the entire hardware-verification capture.
            }
        }

        if (validFrames == 0 || yValues.Count == 0)
        {
            throw new InvalidOperationException(
                "No usable laser line was detected in the verification capture.");
        }

        return new LaserDetectionSummary
        {
            CapturedFrameCount = batch.ConcatenatedFrames.Count,
            ValidLaserFrameCount = validFrames,
            ImageWidth = width,
            ImageHeight = height,
            AverageDetectedColumns = columnCounts.Average(),
            MinimumLaserY = yValues.Min(),
            MaximumLaserY = yValues.Max(),
            MeanLaserY = yValues.Average()
        };
    }
    
    /// <summary>
    /// Evaluates the repeatability of the scanner lookup table using held-out calibration references.
    /// </summary>
    /// <param name="table">The scanner lookup table to evaluate.</param>
    /// <param name="references">The held-out calibration reference captures.</param>
    /// <returns>A tuple containing the mean and maximum repeatability errors in millimeters.</returns>
    private static (double Mean, double Maximum) EvaluateHeldOutRepeatability(
        ScannerLookupTable table,
        IReadOnlyList<CalibrationReferenceCapture> references)
    {
        var errors = new List<double>();

        foreach (CalibrationReferenceCapture reference in references)
        {
            if (reference.HeldOutSamples.Count == 0)
            {
                continue;
            }

            double expectedRadius = reference.Summary.RadiusMM;

            foreach (CalibrationLaserSample sample in reference.HeldOutSamples)
            {
                double predicted = LookupTableInterpolator.GetRadiusMM(
                    table,
                    sample.X,
                    sample.LaserY);

                errors.Add(Math.Abs(predicted - expectedRadius));
            }
        }

        return errors.Count == 0
            ? (double.NaN, double.NaN)
            : (errors.Average(), errors.Max());
    }
    
    /// <summary>
    /// Rejects outliers using the median absolute deviation (MAD): a value is kept
    /// if it is within max(1.5 px, 3.5 x 1.4826 x MAD) of the median. 1.4826 x MAD
    /// estimates the standard deviation for normally distributed data.
    /// </summary>
    /// <param name="values">The array of values to process.</param>
    /// <returns>A new array with outliers removed.</returns>
    private static double[] RejectOutliers(double[] values)
    {
        if (values.Length < 3)
        {
            return values;
        }

        double median = Median(values);
        double[] deviations = [.. values
            .Select(value => Math.Abs(value - median))];

        double medianAbsoluteDeviation = Median(deviations);
        double tolerance = Math.Max(1.5, 3.5 * 1.4826 * medianAbsoluteDeviation);

        return [.. values
            .Where(value => Math.Abs(value - median) <= tolerance)];
    }
    
    /// <summary>
    /// Computes the median of the given values.
    /// </summary>
    /// <param name="values">The values to compute the median for.</param>
    /// <returns>The median value.</returns>
    private static double Median(IEnumerable<double> values)
    {
        double[] sorted = [.. values.OrderBy(value => value)];
        int middle = sorted.Length / 2;

        return sorted.Length % 2 == 0
            ? (sorted[middle - 1] + sorted[middle]) / 2.0
            : sorted[middle];
    }

    /// <summary>
    /// Calculates the standard deviation of a list of double values.
    /// </summary>
    /// <param name="values">The list of double values to calculate the standard deviation for.</param>
    /// <returns>The standard deviation of the values.</returns>
    /// <exception cref="ArgumentNullException">Thrown if the values list is null.</exception>
    private static double StandardDeviation(List<double> values)
    {
        if (values.Count < 2)
        {
            return 0.0;
        }

        double mean = values.Average();
        double variance = values.Sum(value =>
            (value - mean) * (value - mean)) / (values.Count - 1);

        return Math.Sqrt(variance);
    }

    /// <summary>
    /// Computes the SHA-256 hash of the given byte array and returns it as a hexadecimal string.
    /// </summary>
    /// <param name="data">The byte array to compute the SHA-256 hash for.</param>
    /// <returns>The SHA-256 hash as an upper-case hexadecimal string ("" for empty data).</returns>
    private static string ComputeSha256(byte[] data)
    {
        if (data.Length == 0)
        {
            return string.Empty;
        }

        return Convert.ToHexString(SHA256.HashData(data));
    }
}

/// <summary>
/// Represents a summary of the laser detection results from a calibration capture batch,
/// including frame counts, image dimensions, and laser position statistics.
/// </summary>
public sealed class LaserDetectionSummary
{
    /// <summary>Frames in the capture / frames where the laser was found.</summary>
    public int CapturedFrameCount { get; set; }
    public int ValidLaserFrameCount { get; set; }
    /// <summary>Joined image size, pixels.</summary>
    public int ImageWidth { get; set; }
    public int ImageHeight { get; set; }
    /// <summary>Average number of image columns with a detected laser point per frame.</summary>
    public double AverageDetectedColumns { get; set; }
    /// <summary>Lowest / highest / mean laser row seen (pixels).</summary>
    public double MinimumLaserY { get; set; }
    public double MaximumLaserY { get; set; }
    public double MeanLaserY { get; set; }
}
