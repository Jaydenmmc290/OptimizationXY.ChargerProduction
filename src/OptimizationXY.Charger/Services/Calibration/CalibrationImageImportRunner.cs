using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Emgu.CV;
using NLog;
using OptimizationXY.Charger.Infrastructure;
using OptimizationXY.Charger.Models.Acquisition;
using OptimizationXY.Charger.Models.Calibration;
using OptimizationXY.Charger.Models.Settings;
using OptimizationXY.Charger.Services.Core;
using OptimizationXY.Charger.Services.Input;

namespace OptimizationXY.Charger.Services.Calibration;

/// <summary>
/// Builds a scanner calibration from saved reference images instead of a live
/// capture, so this program can use the same pictures (and therefore the same
/// coordinate system) as another calibration: a small tube, a large tube and the
/// home-position cable, each as one or more already-concatenated images.
///
/// Start the program with, for example:
///
///   OptimizationXY.Charger.exe --import-calibration-images
///       --small "C:\Cal\small.png" --small-diameter-in 12.5
///       --large "C:\Cal\large.png" --large-diameter-in 23.955
///       --home  "C:\Cal\cable.png"
///       --operator "Jayden" --notes "Manager reference images"
///
/// Without --activate nothing is changed: a report and a preview lookup table
/// are written to the Logs folder for review. With --activate the new
/// calibration replaces the active one through the normal package store (the
/// current calibration is archived first, as with the setup wizard).
///
/// The images must already have the concatenated width this program produces
/// with the CURRENT appsettings crop/overlap values; the import stops if they do
/// not. The camera configurations are not opened or changed: the camera
/// snapshots of the active calibration are reused.
/// </summary>
internal static class CalibrationImageImportRunner
{
    public const string CommandLineSwitch = "--import-calibration-images";

    private const double MillimetersPerInch = 25.4;
    private const int SmallBandId = 1;
    private const int LargeBandId = 2;
    private const int HomeBandId = 3;

    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static bool IsRequested(string[] args) =>
        args.Any(argument => string.Equals(
            argument,
            CommandLineSwitch,
            StringComparison.OrdinalIgnoreCase));

    public static void Run(string[] args)
    {
        var report = new StringBuilder();
        string reportPath = Path.Combine(
            ApplicationPaths.LogDirectory,
            $"CalibrationImageImport_{DateTime.Now:yyyyMMdd_HHmmss}.txt");

        try
        {
            ImportOptions options = ImportOptions.Parse(args);
            AppSettings settings = PipelineState.Instance.Settings;
            DualCameraConcatenationSettings layout = settings.Acquisition.Concatenation;

            report.AppendLine("=== CALIBRATION IMPORT FROM REFERENCE IMAGES ===");
            report.AppendLine(CultureInfo.InvariantCulture, $"Run: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            report.AppendLine(options.Activate
                ? "Mode: ACTIVATE (the new calibration replaces the active one; the old one is archived)."
                : "Mode: PREVIEW (nothing is activated; add --activate to use it).");
            report.AppendLine("Cameras: not opened. Camera configurations: not changed.");
            report.AppendLine(CultureInfo.InvariantCulture,
                $"Image layout from appsettings: HeadOnLeft {layout.HeadOnLeft}, " +
                $"head crop L/R {layout.HeadCropLeftPixels}/{layout.HeadCropRightPixels}, " +
                $"tail crop L/R {layout.TailCropLeftPixels}/{layout.TailCropRightPixels}, " +
                $"overlap {layout.OverlapPixels}, tail vertical offset {layout.TailVerticalOffsetPixels}.");

            // The active package must be intact: its camera snapshots and its
            // length scale are reused for the imported calibration.
            CalibrationPackageStore.EnsureActivePackageValid();
            ScannerLookupTable previous = LookupTableStore.Load();

            byte[] headSnapshot = File.ReadAllBytes(
                Path.Combine(ApplicationPaths.CalibrationDirectory, "head-camera-configuration.snapshot"));
            byte[] tailSnapshot = File.ReadAllBytes(
                Path.Combine(ApplicationPaths.CalibrationDirectory, "tail-camera-configuration.snapshot"));

            int? expectedWidth = ExpectedConcatenatedWidth(headSnapshot, tailSnapshot, layout);

            double lengthPixelsPerMM = options.LengthPixelsPerMM ?? previous.LengthPixelsPerMM;

            report.AppendLine(CultureInfo.InvariantCulture,
                $"Active calibration before import: {previous.CalibrationId} " +
                $"({previous.ImageWidth}x{previous.ImageHeight}, radius {previous.MinimumCalibratedRadiusMM:F3}-{previous.MaximumCalibratedRadiusMM:F3} mm).");
            report.AppendLine(
                $"LengthPixelsPerMM: {lengthPixelsPerMM:F10}" +
                (options.LengthPixelsPerMM.HasValue ? " (from --length-pixels-per-mm)" : " (kept from the active calibration; cropping does not change it)"));
            report.AppendLine(expectedWidth.HasValue
                ? string.Create(CultureInfo.InvariantCulture, $"Concatenated width these settings produce: {expectedWidth.Value} px")
                : "Concatenated width these settings produce: unknown (camera snapshot has no ReadbackWidth).");
            report.AppendLine();

            var references = new List<CalibrationReferenceCapture>
            {
                ImportReference(report, "Small tube", options.SmallImages, options.SmallDiameterMM, SmallBandId, isHome: false, expectedWidth),
                ImportReference(report, "Large tube", options.LargeImages, options.LargeDiameterMM, LargeBandId, isHome: false, expectedWidth)
            };

            if (options.HomeImages.Count > 0)
            {
                references.Add(ImportReference(
                    report, "Home position (cable)", options.HomeImages, 0.0, HomeBandId, isHome: true, expectedWidth));
            }
            else
            {
                report.AppendLine("Home position (cable): no image given; the table has no radius-0 band.");
                report.AppendLine();
            }

            ScannerLookupTable table = ScannerCalibrationBuilder.BuildTable(
                settings,
                options.OperatorName,
                lengthPixelsPerMM,
                references,
                headSnapshot,
                tailSnapshot,
                independentValidationCompleted: false,
                independentValidationMeanErrorMM: null,
                independentValidationMaximumErrorMM: null,
                notes: "Imported from reference images. " + options.Notes);

            // Single images have no held-out frames, so the builder's
            // repeatability is NaN (which cannot be saved). Use the in-sample
            // error of the table against its own reference points instead.
            bool heldOutRepeatabilityMeasured =
                double.IsFinite(table.Quality.MeanRepeatabilityErrorMM) &&
                double.IsFinite(table.Quality.MaximumRepeatabilityErrorMM);

            if (!heldOutRepeatabilityMeasured)
            {
                (double mean, double maximum) = InSampleError(table);
                table.Quality.MeanRepeatabilityErrorMM = mean;
                table.Quality.MaximumRepeatabilityErrorMM = maximum;
                table.Quality.Notes += " Repeatability values are the in-sample fit of the reference images (no held-out frames).";
            }

            AppendTableSummary(report, table, previous);

            string previewDirectory = Path.Combine(
                ApplicationPaths.LogDirectory,
                "CalibrationImageImport_" + table.CalibrationId);
            Directory.CreateDirectory(previewDirectory);
            string previewPath = Path.Combine(previewDirectory, "scanner-lookup-table.preview.json");
            File.WriteAllText(previewPath, JsonSerializer.Serialize(table, JsonOptions), Encoding.UTF8);
            report.AppendLine(CultureInfo.InvariantCulture, $"Preview lookup table: {previewPath}");

            if (options.Activate)
            {
                string activePath = CalibrationPackageStore.Activate(table, headSnapshot, tailSnapshot);

                settings.Setup.ActiveCalibrationId = table.CalibrationId;
                settings.Setup.ScannerCalibrationCreated = true;
                settings.Setup.PendingLengthPixelsPerMM = table.LengthPixelsPerMM;
                // Same rule as the setup wizard, but an in-sample fit of single
                // images is not a repeatability check, so it is not marked verified.
                settings.Setup.ScannerCalibrationRepeatabilityVerified = heldOutRepeatabilityMeasured;
                settings.Setup.LastSetupCompletedUtc = DateTime.UtcNow;
                AppSettingsStoreSaveIfAvailable(settings);

                report.AppendLine(CultureInfo.InvariantCulture, $"ACTIVATED: {table.CalibrationId} -> {activePath}");
                report.AppendLine(CultureInfo.InvariantCulture, $"Previous calibration {previous.CalibrationId} was archived.");
                if (!heldOutRepeatabilityMeasured)
                {
                    report.AppendLine("Repeatability is NOT marked verified: give three or more images per reference " +
                        "(different rotations) for a held-out check, or verify in the Production Setup wizard.");
                }
            }
            else
            {
                report.AppendLine("Nothing was activated. Re-run with --activate to use this calibration.");
            }

            Finish(report, reportPath, failed: false);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "[CAL-IMPORT] Calibration image import failed.");
            report.AppendLine();
            report.AppendLine("IMPORT FAILED: " + ex.Message);
            Finish(report, reportPath, failed: true);
        }
    }

    private static CalibrationReferenceCapture ImportReference(
        StringBuilder report,
        string name,
        List<string> imagePaths,
        double diameterMM,
        int bandId,
        bool isHome,
        int? expectedWidth)
    {
        if (imagePaths.Count == 0)
        {
            throw new ArgumentException($"No image was given for the {name} reference.");
        }

        var frames = new List<Mat>();

        try
        {
            foreach (string path in imagePaths)
            {
                Mat image = ImageInputService.LoadGrayImage(path);
                frames.Add(image);

                if (expectedWidth.HasValue && image.Width != expectedWidth.Value)
                {
                    throw new InvalidDataException(
                        $"{name} image '{path}' is {image.Width} px wide, but the current appsettings crop/overlap " +
                        $"produce {expectedWidth.Value} px. Set the image layout to match these images before importing.");
                }
            }

            DateTime capturedUtc = imagePaths
                .Select(path => File.GetLastWriteTimeUtc(path))
                .Min();

            CalibrationReferenceCapture capture = ScannerCalibrationBuilder.ExtractReferenceFromImages(
                frames,
                capturedUtc,
                name,
                diameterMM,
                bandId,
                isHome);

            CalibrationReferenceSummary summary = capture.Summary;
            int firstX = capture.Points.Min(point => point.X);
            int lastX = capture.Points.Max(point => point.X);

            report.AppendLine(CultureInfo.InvariantCulture,
                $"{name}: band {bandId}, diameter {diameterMM:F3} mm ({diameterMM / MillimetersPerInch:F3} in), radius {summary.RadiusMM:F3} mm");
            foreach (string path in imagePaths)
                report.AppendLine("  image: " + path);
            report.AppendLine(CultureInfo.InvariantCulture,
                $"  laser found in {summary.AcceptedPointCount} columns (X {firstX}-{lastX} of {summary.ImageWidth}), " +
                $"mean laser Y {summary.MeanDetectedLaserY:F2} px, std {summary.LaserYStandardDeviation:F2} px");
            report.AppendLine();

            return capture;
        }
        finally
        {
            foreach (Mat frame in frames)
                frame.Dispose();
        }
    }

    /// <summary>
    /// Width of the concatenated image the current settings produce, from the
    /// camera widths recorded in the camera snapshots. Null when unknown.
    /// </summary>
    private static int? ExpectedConcatenatedWidth(
        byte[] headSnapshot,
        byte[] tailSnapshot,
        DualCameraConcatenationSettings layout)
    {
        int? headWidth = ReadbackWidth(headSnapshot);
        int? tailWidth = ReadbackWidth(tailSnapshot);

        if (!headWidth.HasValue || !tailWidth.HasValue)
            return null;

        int head = headWidth.Value - layout.HeadCropLeftPixels - layout.HeadCropRightPixels;
        int tail = tailWidth.Value - layout.TailCropLeftPixels - layout.TailCropRightPixels;
        int overlap = Math.Clamp(layout.OverlapPixels, 0, Math.Min(head, tail) - 1);

        return head + tail - overlap;
    }

    private static int? ReadbackWidth(byte[] snapshot)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(snapshot);

            if (!document.RootElement.TryGetProperty("ReadbackWidth", out JsonElement value))
                return null;

            string? text = value.ValueKind == JsonValueKind.Number
                ? value.GetRawText()
                : value.GetString();

            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int width) && width > 0
                ? width
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static (double Mean, double Maximum) InSampleError(ScannerLookupTable table)
    {
        var errors = new List<double>();

        foreach (LookupPoint point in table.Points)
        {
            try
            {
                double predicted = LookupTableInterpolator.GetRadiusMM(table, point.X, point.LaserY);
                errors.Add(Math.Abs(predicted - point.RadiusMM));
            }
            catch (InvalidOperationException)
            {
                // Column without two bands nearby; not counted.
            }
        }

        return errors.Count == 0
            ? (0.0, 0.0)
            : (errors.Average(), errors.Max());
    }

    private static void AppendTableSummary(
        StringBuilder report,
        ScannerLookupTable table,
        ScannerLookupTable previous)
    {
        report.AppendLine(CultureInfo.InvariantCulture,
            $"New calibration {table.CalibrationId}: {table.ImageWidth}x{table.ImageHeight}, " +
            $"{table.Points.Count} points, radius {table.MinimumCalibratedRadiusMM:F3}-{table.MaximumCalibratedRadiusMM:F3} mm, " +
            $"fit error mean {table.Quality.MeanRepeatabilityErrorMM:F3} / max {table.Quality.MaximumRepeatabilityErrorMM:F3} mm");
        report.AppendLine();

        report.AppendLine("Laser row of each reference across the image (X: small / large / home):");
        int step = Math.Max(1, table.ImageWidth / 8);
        for (int x = 0; x < table.ImageWidth; x += step)
        {
            string Row(int band)
            {
                var near = table.Points
                    .Where(point => point.RadiusBandId == band && Math.Abs(point.X - x) <= 5)
                    .Select(point => point.LaserY)
                    .ToList();
                return near.Count == 0
                    ? "  -   "
                    : near.Average().ToString("F1", CultureInfo.InvariantCulture);
            }

            report.AppendLine(CultureInfo.InvariantCulture,
                $"  X {x,4}: {Row(SmallBandId),6} / {Row(LargeBandId),6} / {Row(HomeBandId),6}");
        }
        report.AppendLine();

        // How differently the two calibrations read the same laser row, near the
        // middle of the image. The image widths differ, so the old table is read
        // at the same fraction of its width.
        report.AppendLine("Radius the OLD and NEW calibration give for the same laser row (image centre):");
        int newX = table.ImageWidth / 2;
        int oldX = previous.ImageWidth / 2;

        foreach (CalibrationReferenceSummary reference in table.References)
        {
            double row = reference.MeanDetectedLaserY;
            string newRadius = TryRadius(table, newX, row);
            string oldRadius = TryRadius(previous, oldX, row);
            report.AppendLine(CultureInfo.InvariantCulture,
                $"  {reference.Name} row {row:F1}: new {newRadius} mm, old {oldRadius} mm (known {reference.RadiusMM:F3} mm)");
        }
        report.AppendLine();
    }

    private static string TryRadius(ScannerLookupTable table, int x, double laserY)
    {
        try
        {
            return LookupTableInterpolator
                .GetRadiusMM(table, x, laserY)
                .ToString("F2", CultureInfo.InvariantCulture);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentOutOfRangeException)
        {
            return "out of range";
        }
    }

    private static void AppSettingsStoreSaveIfAvailable(AppSettings settings)
    {
        try
        {
            OptimizationXY.Charger.Services.Configuration.AppSettingsStore.Save(settings);
        }
        catch (Exception ex)
        {
            Logger.Warn(ex, "[CAL-IMPORT] Calibration activated, but the setup status in appsettings could not be saved.");
        }
    }

    private static void Finish(StringBuilder report, string reportPath, bool failed)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
            File.WriteAllText(reportPath, report.ToString(), Encoding.UTF8);
            Logger.Info($"[CAL-IMPORT] Report written to '{reportPath}'.");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "[CAL-IMPORT] Report could not be written.");
        }

        MessageBox.Show(
            (failed ? "Calibration image import FAILED.\r\n\r\n" : "Calibration image import finished.\r\n\r\n") +
            "Full report:\r\n" + reportPath,
            "Optimization XY Charger - Calibration Import",
            MessageBoxButtons.OK,
            failed ? MessageBoxIcon.Error : MessageBoxIcon.Information);

        try
        {
            Process.Start(new ProcessStartInfo(reportPath) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Logger.Warn(ex, "[CAL-IMPORT] The report could not be opened automatically.");
        }
    }

    private sealed class ImportOptions
    {
        public List<string> SmallImages { get; } = [];
        public List<string> LargeImages { get; } = [];
        public List<string> HomeImages { get; } = [];
        public double SmallDiameterMM { get; private set; }
        public double LargeDiameterMM { get; private set; }
        public double? LengthPixelsPerMM { get; private set; }
        public string OperatorName { get; private set; } = Environment.UserName;
        public string Notes { get; private set; } = string.Empty;
        public bool Activate { get; private set; }

        public static ImportOptions Parse(string[] args)
        {
            var options = new ImportOptions();

            for (int index = 0; index < args.Length; index++)
            {
                string name = args[index].ToLowerInvariant();

                string Value()
                {
                    if (index + 1 >= args.Length)
                        throw new ArgumentException($"{args[index]} needs a value.");
                    return args[++index];
                }

                switch (name)
                {
                    case "--small": options.SmallImages.Add(Value()); break;
                    case "--large": options.LargeImages.Add(Value()); break;
                    case "--home": options.HomeImages.Add(Value()); break;
                    case "--small-diameter-in": options.SmallDiameterMM = Number(Value()) * MillimetersPerInch; break;
                    case "--large-diameter-in": options.LargeDiameterMM = Number(Value()) * MillimetersPerInch; break;
                    case "--small-diameter-mm": options.SmallDiameterMM = Number(Value()); break;
                    case "--large-diameter-mm": options.LargeDiameterMM = Number(Value()); break;
                    case "--length-pixels-per-mm": options.LengthPixelsPerMM = Number(Value()); break;
                    case "--operator": options.OperatorName = Value(); break;
                    case "--notes": options.Notes = Value(); break;
                    case "--activate": options.Activate = true; break;
                }
            }

            if (options.SmallDiameterMM <= 0 || options.LargeDiameterMM <= 0)
                throw new ArgumentException("Both --small-diameter-in and --large-diameter-in (or the -mm forms) are required.");

            if (options.LargeDiameterMM <= options.SmallDiameterMM)
                throw new ArgumentException("The large tube diameter must be larger than the small tube diameter.");

            if (options.LengthPixelsPerMM is <= 0)
                throw new ArgumentException("--length-pixels-per-mm must be positive.");

            if (string.IsNullOrWhiteSpace(options.OperatorName))
                options.OperatorName = "unknown";

            return options;
        }

        private static double Number(string text) =>
            double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) &&
            double.IsFinite(value)
                ? value
                : throw new ArgumentException($"'{text}' is not a number.");
    }
}
