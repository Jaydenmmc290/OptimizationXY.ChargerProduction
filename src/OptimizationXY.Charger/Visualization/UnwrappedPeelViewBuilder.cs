using Emgu.CV;
using Emgu.CV.Structure;
using OptimizationXY.Charger.Models.Analysis;
using OptimizationXY.Charger.Models.Geometry;
using OptimizationXY.Charger.Models.Settings;


namespace OptimizationXY.Charger.Visualization;

/// <summary>
/// Builds a 2D unwrapped view of a log peel simulation based on point cloud data and various settings.
/// </summary>
public static class UnwrappedPeelViewBuilder
{
    private const double MM_PER_INCH = 25.4;

    /// <summary>
    /// Builds a simulation of the veneer peel from a log based on the provided point cloud and settings.
    /// </summary>
    /// <param name="pointCloud">The point cloud data representing the log.</param>
    /// <param name="settings">The settings for the peel view simulation.</param>
    /// <param name="usableSettings">The settings for usable radius calculations.</param>
    /// <param name="waneSettings">The settings for wane calculations.</param>
    /// <param name="tailXMM">The X coordinate of the tail of the log in millimeters.</param>
    /// <param name="tailYMM">The Y coordinate of the tail of the log in millimeters.</param>
    /// <param name="headXMM">The X coordinate of the head of the log in millimeters.</param>
    /// <param name="headYMM">The Y coordinate of the head of the log in millimeters.</param>
    /// <param name="fixedOuterRadiusMM">The fixed outer radius of the log in millimeters, if specified.</param>
    /// <returns>A PeelSimulationResult containing the simulation results and the generated image.</returns>
    public static PeelSimulationResult BuildSimulation(
            List<Point3D> pointCloud,
            PeelViewSettings settings,
            UsableRadiusSettings usableSettings,
            WaneSettings waneSettings,
            double tailXMM = 0.0,
            double tailYMM = 0.0,
            double headXMM = 0.0,
            double headYMM = 0.0,
            double? fixedOuterRadiusMM = null)
    {
        if (pointCloud == null || pointCloud.Count == 0)
        {
            return new PeelSimulationResult
            {
                Image = BuildEmpty("No point cloud data.")
            };
        }

        SurfaceModel surface = BuildSurfaceModel(
   pointCloud,
   settings,
   tailXMM,
   tailYMM,
   headXMM,
   headYMM);

        if (fixedOuterRadiusMM.HasValue && fixedOuterRadiusMM.Value > 0)
        {
            surface.OuterRadiusMM = fixedOuterRadiusMM.Value;
        }

        double coreRadiusMM = usableSettings.CoreDiameterInches * MM_PER_INCH / 2.0;

        if (surface.OuterRadiusMM <= coreRadiusMM)
        {
            return new PeelSimulationResult
            {
                Image = BuildEmpty("Log is already at or below 4 inch core."),
                OuterDiameterMM = surface.OuterRadiusMM * 2.0,
                CoreDiameterMM = coreRadiusMM * 2.0,
                LogLengthMM = surface.LogLengthMM
            };
        }

        List<double> peelRadii = BuildPeelRadii(
            surface.OuterRadiusMM,
            coreRadiusMM,
            usableSettings.VeneerThicknessMM);

        double totalStripWidthMM = peelRadii.Sum(r => 2.0 * Math.PI * r);

        double pixelsPerMM = settings.PixelsPerMM;

        int stripWidthPixels = Math.Max(900, (int)(totalStripWidthMM * pixelsPerMM));
        int stripHeightPixels = settings.LengthBins;

        int totalWidth =
            settings.LeftMargin +
            stripWidthPixels +
            settings.RightMargin;

        int totalHeight =
            settings.TopMargin +
            stripHeightPixels +
            settings.BottomMargin;

        Image<Bgr, byte> canvas = new(
            totalWidth,
            totalHeight,
            new Bgr(28, 24, 20));

        Mat image = canvas.Mat;

        Rectangle stripRect = new(
            settings.LeftMargin,
            settings.TopMargin,
            stripWidthPixels,
            stripHeightPixels);

        DrawHeader(image, surface, settings, usableSettings, peelRadii.Count);
        DrawStripBackground(image, stripRect);

        long totalSamples = 0;
        long waneSamples = 0;
        long severeWaneSamples = 0;

        var sheetStats = DrawPeelAsContinuousSheets(
            canvas,
            surface,
            settings,
            usableSettings,
            waneSettings,
            stripRect,
            peelRadii,
            ref totalSamples,
            ref waneSamples,
            ref severeWaneSamples);

        PeelSheetSummary sheetSummary =
            DrawSheetBoundariesAndLabels(
                image,
                stripRect,
                settings,
                totalStripWidthMM,
                sheetStats);

        DrawFooter(
            image,
            stripRect,
            settings,
            waneSettings,
            totalSamples,
            waneSamples,
            severeWaneSamples);
        double wanePercent =
            totalSamples > 0
                ? 100.0 * waneSamples / totalSamples
                : 0.0;

        double severeWanePercent =
            totalSamples > 0
                ? 100.0 * severeWaneSamples / totalSamples
                : 0.0;

        return new PeelSimulationResult
        {
            Image = image,
            Revolutions = peelRadii.Count,
            OuterDiameterMM = surface.OuterRadiusMM * 2.0,
            CoreDiameterMM = coreRadiusMM * 2.0,
            LogLengthMM = surface.LogLengthMM,
            TotalWaneAreaPercent = wanePercent,
            SevereWaneAreaPercent = severeWanePercent,
            TotalFullSheets = sheetSummary.TotalFullSheets,
            UsableFullSheets = sheetSummary.UsableFullSheets,
            RejectedFullSheets = sheetSummary.RejectedFullSheets,
            TotalVeneerLengthMM = sheetSummary.TotalVeneerLengthMM,
            UsableVeneerLengthMM = sheetSummary.UsableVeneerLengthMM,
            PartialSheetLengthMM = sheetSummary.PartialSheetLengthMM,
            AverageUsableCoveragePercent =
                sheetSummary.AverageUsableCoveragePercent
        };
    }

    /// <summary>
    /// Builds a surface model from the provided point cloud data, representing the log's outer surface.
    /// </summary>
    /// <param name="pointCloud">The point cloud data representing the log.</param>
    /// <param name="settings">The settings for the peel view simulation.</param>
    /// <param name="tailXMM">The X coordinate of the tail of the log in millimeters.</param>
    /// <param name="tailYMM">The Y coordinate of the tail of the log in millimeters.</param>
    /// <param name="headXMM">The X coordinate of the head of the log in millimeters.</param>
    /// <param name="headYMM">The Y coordinate of the head of the log in millimeters.</param>
    /// <returns>A SurfaceModel representing the log's outer surface.</returns>
    private static SurfaceModel BuildSurfaceModel(
            List<Point3D> pointCloud,
            PeelViewSettings settings,
            double tailXMM,
            double tailYMM,
            double headXMM,
            double headYMM)
    {
        int zBins = Math.Max(20, settings.LengthBins);
        int angleBins = Math.Max(36, settings.AngleBins);

        double minZ = pointCloud.Min(p => p.Z);
        double maxZ = pointCloud.Max(p => p.Z);
        double zRange = Math.Max(maxZ - minZ, 0.0001);

        double[,] radiusGrid = new double[zBins, angleBins];
        for (int z = 0; z < zBins; z++)
        {
            for (int a = 0; a < angleBins; a++)
                radiusGrid[z, a] = double.NaN;
        }

        List<double> allRadii = [];

        foreach (var p in pointCloud)
        {
            double t = (p.Z - minZ) / zRange;
            t = Math.Clamp(t, 0.0, 1.0);

            double spindleX = tailXMM + (headXMM - tailXMM) * t;
            double spindleY = tailYMM + (headYMM - tailYMM) * t;

            // Point coordinates are measured from the current fitted centerline.
            // The candidate spindle coordinates define the new axis in that same
            // frame, so surface-to-axis distance is point minus axis.
            double dx = p.X - spindleX;
            double dy = p.Y - spindleY;

            double radius = Math.Sqrt(dx * dx + dy * dy);

            if (radius <= 1)
                continue;

            double angle = Math.Atan2(dy, dx);

            if (angle < 0)
                angle += 2.0 * Math.PI;

            int zIndex = ClampInt(
                (int)Math.Round((p.Z - minZ) / zRange * (zBins - 1)),
                0,
                zBins - 1);

            int angleIndex = ClampInt(
                (int)Math.Round(angle / (2.0 * Math.PI) * (angleBins - 1)),
                0,
                angleBins - 1);

            // Important:
            // Use the maximum radius in a cell because this model needs the OUTSIDE surface.
            if (double.IsNaN(radiusGrid[zIndex, angleIndex]) || radius > radiusGrid[zIndex, angleIndex])
                radiusGrid[zIndex, angleIndex] = radius;

            allRadii.Add(radius);
        }

        double averageRadius = allRadii.Count > 0 ? allRadii.Average() : 0.0;
        double outerRadius = Percentile(allRadii, settings.OuterRadiusPercentile);

        FillMissingCells(radiusGrid, averageRadius);
        SmoothSurface(radiusGrid, settings.SurfaceSmoothPasses);

        return new SurfaceModel
        {
            RadiusByZAndAngle = radiusGrid,
            MinZ = minZ,
            MaxZ = maxZ,
            OuterRadiusMM = outerRadius,
            AverageRadiusMM = averageRadius
        };
    }

    /// <summary>
    /// Draws the peel simulation as continuous sheets on the provided canvas, calculating wane and severe wane statistics.
    /// </summary>
    /// <param name="canvas">The canvas on which to draw the peel simulation.</param>
    /// <param name="surface">The surface model representing the unwrapped peel view.</param>
    /// <param name="settings">The settings for the peel view.</param>
    /// <param name="usableSettings">The settings for usable radius.</param>
    /// <param name="waneSettings">The settings for wane calculation.</param>
    /// <param name="rect">The rectangle defining the drawing area.</param>
    /// <param name="peelRadii">The list of peel radii.</param>
    /// <param name="totalSamples">The total number of samples.</param>
    /// <param name="waneSamples">The number of wane samples.</param>
    /// <param name="severeWaneSamples">The number of severe wane samples.</param>
    /// <returns>A dictionary mapping sheet indices to their quality statistics.</returns>
    private static Dictionary<int, SheetQualityStats> DrawPeelAsContinuousSheets(
            Image<Bgr, byte> canvas,
            SurfaceModel surface,
            PeelViewSettings settings,
            UsableRadiusSettings usableSettings,
            WaneSettings waneSettings,
            Rectangle rect,
            List<double> peelRadii,
            ref long totalSamples,
            ref long waneSamples,
            ref long severeWaneSamples)
    {
        var sheetStats = new Dictionary<int, SheetQualityStats>();

        if (peelRadii == null || peelRadii.Count == 0)
            return sheetStats;

        int zBins = surface.RadiusByZAndAngle.GetLength(0);
        int angleBins = surface.RadiusByZAndAngle.GetLength(1);

        double[] revolutionStartMM = new double[peelRadii.Count];
        double[] revolutionWidthMM = new double[peelRadii.Count];

        double totalVeneerLengthMM = 0.0;

        for (int rev = 0; rev < peelRadii.Count; rev++)
        {
            revolutionStartMM[rev] = totalVeneerLengthMM;

            double circumferenceMM = 2.0 * Math.PI * peelRadii[rev];

            revolutionWidthMM[rev] = circumferenceMM;
            totalVeneerLengthMM += circumferenceMM;
        }

        double sheetLengthMM =
            Math.Max(1.0, settings.VeneerMarkerSpacingInches) * MM_PER_INCH;

        int[] revByX = new int[rect.Width];
        int[] angleByX = new int[rect.Width];
        int[] sheetByX = new int[rect.Width];
        double[] angleRatioByX = new double[rect.Width];

        int currentRev = 0;

        for (int px = 0; px < rect.Width; px++)
        {
            double xRatio =
                px / (double)Math.Max(1, rect.Width - 1);

            double veneerDistanceMM =
                xRatio * totalVeneerLengthMM;

            while (currentRev < peelRadii.Count - 1 &&
                   veneerDistanceMM >= revolutionStartMM[currentRev] + revolutionWidthMM[currentRev])
            {
                currentRev++;
            }

            double localDistanceMM =
                veneerDistanceMM - revolutionStartMM[currentRev];

            double angleRatio =
                revolutionWidthMM[currentRev] > 0.0001
                    ? localDistanceMM / revolutionWidthMM[currentRev]
                    : 0.0;

            angleRatio = Math.Clamp(angleRatio, 0.0, 1.0);

            int angleIndex = ClampInt(
                (int)Math.Round(angleRatio * (angleBins - 1)),
                0,
                angleBins - 1);

            int sheetIndex =
                (int)Math.Floor(veneerDistanceMM / sheetLengthMM);

            revByX[px] = currentRev;
            angleByX[px] = angleIndex;
            sheetByX[px] = sheetIndex;
            angleRatioByX[px] = angleRatio;
        }

        for (int y = rect.Top; y < rect.Bottom; y++)
        {
            double zRatio =
                (y - rect.Top) / (double)Math.Max(1, rect.Height - 1);

            int zIndex = ClampInt(
                (int)Math.Round(zRatio * (zBins - 1)),
                0,
                zBins - 1);

            for (int x = rect.Left; x < rect.Right; x++)
            {
                int px = x - rect.Left;

                int rev = revByX[px];
                int angleIndex = angleByX[px];
                int sheetIndex = sheetByX[px];

                double peelRadiusMM = peelRadii[rev];

                double scannedOuterRadiusMM =
                    surface.RadiusByZAndAngle[zIndex, angleIndex];

                double missingWoodMM =
                    peelRadiusMM - scannedOuterRadiusMM;

                double lowSpotMM =
                    surface.OuterRadiusMM - scannedOuterRadiusMM;

                Bgr color = GetPeelColor(
                    peelRadiusMM,
                    scannedOuterRadiusMM,
                    missingWoodMM,
                    lowSpotMM,
                    settings,
                    usableSettings,
                    waneSettings,
                    zRatio,
                    angleRatioByX[px],
                    rev,
                    (x - rect.Left) / Math.Max(1.0, rect.Width - 1.0));

                canvas.Data[y, x, 0] = (byte)Math.Clamp((int)color.Blue, 0, 255);
                canvas.Data[y, x, 1] = (byte)Math.Clamp((int)color.Green, 0, 255);
                canvas.Data[y, x, 2] = (byte)Math.Clamp((int)color.Red, 0, 255);

                totalSamples++;

                if (!sheetStats.TryGetValue(sheetIndex, out var stats))
                {
                    stats = new SheetQualityStats();
                    sheetStats[sheetIndex] = stats;
                }

                stats.TotalCells++;

                bool isWane =
                    missingWoodMM > waneSettings.WaneToleranceMM;

                bool isSevere =
                    missingWoodMM > waneSettings.SevereWaneMM;

                if (isWane)
                {
                    waneSamples++;
                    stats.WaneCells++;
                }
                else
                {
                    stats.UsableCells++;
                }

                if (isSevere)
                {
                    severeWaneSamples++;
                    stats.SevereWaneCells++;
                }
            }
        }

        return sheetStats;
    }

    /// <summary>
    /// Draws the boundaries and labels for each veneer sheet on the provided image, calculating summary statistics for usable and rejected sheets.
    /// </summary>
    /// <param name="image">The image on which to draw the sheet boundaries and labels.</param>
    /// <param name="stripRect">The rectangle defining the area of the strip to draw.</param>
    /// <param name="settings">The settings for the peel view.</param>
    /// <param name="totalVeneerLengthMM">The total length of the veneer in millimeters.</param>
    /// <param name="sheetStats">A dictionary mapping sheet indices to their quality statistics.</param>
    /// <returns>A summary of the peel sheet statistics.</returns>
    private static PeelSheetSummary DrawSheetBoundariesAndLabels(
        Mat image,
        Rectangle stripRect,
        PeelViewSettings settings,
        double totalVeneerLengthMM,
        Dictionary<int, SheetQualityStats> sheetStats)
    {
        double sheetLengthMM =
            Math.Max(1.0, settings.VeneerMarkerSpacingInches) *
            MM_PER_INCH;

        var summary = new PeelSheetSummary
        {
            TotalVeneerLengthMM = Math.Max(0.0, totalVeneerLengthMM)
        };

        if (sheetLengthMM <= 0 || totalVeneerLengthMM <= 0)
            return summary;

        int displayedSheetCount =
            Math.Max(
                1,
                (int)Math.Ceiling(
                    totalVeneerLengthMM /
                    sheetLengthMM));

        int fullSheetCount =
            Math.Max(
                0,
                (int)Math.Floor(
                    totalVeneerLengthMM /
                    sheetLengthMM));

        summary.TotalFullSheets = fullSheetCount;
        summary.PartialSheetLengthMM =
            Math.Max(
                0.0,
                totalVeneerLengthMM -
                fullSheetCount * sheetLengthMM);

        double fullSheetCoverageSum = 0.0;

        for (int sheet = 0;
             sheet < displayedSheetCount;
             sheet++)
        {
            double startMM = sheet * sheetLengthMM;
            double endMM = Math.Min(
                totalVeneerLengthMM,
                startMM + sheetLengthMM);

            int x1 = stripRect.Left +
                (int)Math.Round(
                    (startMM / totalVeneerLengthMM) *
                    stripRect.Width);

            int x2 = stripRect.Left +
                (int)Math.Round(
                    (endMM / totalVeneerLengthMM) *
                    stripRect.Width);

            if (x2 <= stripRect.Left ||
                x1 >= stripRect.Right)
            {
                continue;
            }

            x1 = ClampInt(
                x1,
                stripRect.Left,
                stripRect.Right - 1);

            x2 = ClampInt(
                x2,
                stripRect.Left,
                stripRect.Right - 1);

            sheetStats.TryGetValue(
                sheet,
                out SheetQualityStats? stats);

            double wanePercent =
                stats is not null &&
                stats.TotalCells > 0
                    ? 100.0 *
                      stats.WaneCells /
                      stats.TotalCells
                    : 0.0;

            double severePercent =
                stats is not null &&
                stats.TotalCells > 0
                    ? 100.0 *
                      stats.SevereWaneCells /
                      stats.TotalCells
                    : 0.0;

            double coveragePercent =
                Math.Clamp(
                    100.0 - wanePercent,
                    0.0,
                    100.0);

            bool isFullSheet = sheet < fullSheetCount;

            bool usable =
                isFullSheet &&
                coveragePercent >=
                    settings.MinimumUsableSheetCoveragePercent &&
                severePercent <=
                    settings.MaximumUsableSheetSevereWanePercent;

            bool caution =
                isFullSheet &&
                !usable &&
                coveragePercent >=
                    Math.Max(
                        0.0,
                        settings.MinimumUsableSheetCoveragePercent -
                        5.0) &&
                severePercent <=
                    settings.MaximumUsableSheetSevereWanePercent +
                    1.5;

            if (isFullSheet)
            {
                fullSheetCoverageSum += coveragePercent;

                if (usable)
                    summary.UsableFullSheets++;
                else
                    summary.RejectedFullSheets++;
            }

            MCvScalar lineColor =
                !isFullSheet
                    ? new MCvScalar(180, 180, 180)
                    : usable
                        ? new MCvScalar(80, 220, 80)
                        : caution
                            ? new MCvScalar(0, 220, 255)
                            : new MCvScalar(0, 0, 255);

            CvInvoke.Line(
                image,
                new Point(x1, stripRect.Top),
                new Point(x1, stripRect.Bottom),
                lineColor,
                2);

            if (x2 - x1 > 50)
            {
                CvInvoke.Rectangle(
                    image,
                    new Rectangle(
                        x1,
                        stripRect.Top,
                        Math.Max(1, x2 - x1),
                        stripRect.Height),
                    lineColor,
                    1);
            }

            string status =
                !isFullSheet
                    ? "PARTIAL"
                    : usable
                        ? "USABLE"
                        : caution
                            ? "CHECK"
                            : "TRASH";

            string label =
                $"Sheet {sheet + 1} {status}";

            CvInvoke.PutText(
                image,
                label,
                new Point(
                    x1 + 5,
                    stripRect.Top - 28),
                Emgu.CV.CvEnum.FontFace.HersheySimplex,
                0.34,
                lineColor,
                1);

            CvInvoke.PutText(
                image,
                $"{coveragePercent:F1}% wood",
                new Point(
                    x1 + 5,
                    stripRect.Top - 10),
                Emgu.CV.CvEnum.FontFace.HersheySimplex,
                0.32,
                lineColor,
                1);
        }

        summary.UsableVeneerLengthMM =
            summary.UsableFullSheets *
            sheetLengthMM;

        summary.AverageUsableCoveragePercent =
            fullSheetCount > 0
                ? fullSheetCoverageSum /
                  fullSheetCount
                : 0.0;

        CvInvoke.Line(
            image,
            new Point(
                stripRect.Right - 1,
                stripRect.Top),
            new Point(
                stripRect.Right - 1,
                stripRect.Bottom),
            new MCvScalar(255, 255, 255),
            1);

        return summary;
    }

    /// <summary>
    /// Determines the color to use for a given peel radius based on the scanned outer radius, missing wood, low spots, and various settings.
    /// </summary>
    /// <param name="peelRadiusMM">The radius of the peel in millimeters.</param>
    /// <param name="scannedOuterRadiusMM">The scanned outer radius in millimeters.</param>
    /// <param name="missingWoodMM">The amount of missing wood in millimeters.</param>
    /// <param name="lowSpotMM">The depth of low spots in millimeters.</param>
    /// <param name="settings">The settings for the peel view.</param>
    /// <param name="usableSettings">The settings for usable radius.</param>
    /// <param name="waneSettings">The settings for wane detection.</param>
    /// <param name="zRatio">The ratio of the current z position to the total height.</param>
    /// <param name="angleRatio">The ratio of the current angle to the total angle.</param>
    /// <param name="revolutionIndex">The index of the current revolution.</param>
    /// <param name="veneerLengthInches">The length of the veneer in inches.</param>
    /// <returns>The color to use for the given peel radius.</returns>
    private static Bgr GetPeelColor(
        double peelRadiusMM,
        double scannedOuterRadiusMM,
        double missingWoodMM,
        double lowSpotMM,
        PeelViewSettings settings,
        UsableRadiusSettings usableSettings,
        WaneSettings waneSettings,
        double zRatio,
        double angleRatio,
        int revolutionIndex,
        double veneerLengthInches)
    {
        // This is not random art.
        // This base wood color is only the background for valid wood areas.
        // Wane/low wood overrides it using the real missingWoodMM calculation.

        double grain =
            18.0 * Math.Sin(zRatio * 70.0 + revolutionIndex * 0.4) +
            9.0 * Math.Sin(zRatio * 210.0 + angleRatio * 14.0);

        double revDarkening =
Math.Min((veneerLengthInches / settings.VeneerLengthPerRevolutionInches) * 1.2, 35.0);

        int b = (int)(82 + grain * 0.25 - revDarkening * 0.10);
        int g = (int)(132 + grain * 0.65 - revDarkening * 0.25);
        int r = (int)(184 + grain * 0.85 - revDarkening * 0.20);

        // Severe missing veneer
        if (missingWoodMM > waneSettings.SevereWaneMM)
        {
            return new Bgr(0, 0, 0);
        }

        // Moderate missing veneer
        if (missingWoodMM > waneSettings.WaneToleranceMM)
        {
            return new Bgr(15, 15, 15);
        }

        // Thin face warning.
        // This means the peel cylinder is very close to the scanned outside surface.
        // It has wood, but not much cleanup.
        if (missingWoodMM > -usableSettings.ThinCleanupWarningMM)
        {
            return new Bgr(70, 185, 235);
        }

        // Low area on the original outside surface.
        // It may not be missing at this inner radius, but it tells the operator
        // that this section came from a depressed/wane-prone part of the scan.
        if (lowSpotMM > settings.LowSpotVisualWarningMM && revolutionIndex < settings.LowSpotVisualMaxRevolutions)
        {
            return new Bgr(
                Math.Clamp(b - 20, 0, 255),
                Math.Clamp(g - 20, 0, 255),
                Math.Clamp(r + 25, 0, 255));
        }

        return new Bgr(
            Math.Clamp(b, 0, 255),
            Math.Clamp(g, 0, 255),
            Math.Clamp(r, 0, 255));
    }

    private static List<double> BuildPeelRadii(
        double outerRadiusMM,
        double coreRadiusMM,
        double veneerThicknessMM)
    {
        List<double> radii = [];

        if (veneerThicknessMM <= 0)
            veneerThicknessMM = 3.2;

        for (double r = outerRadiusMM; r > coreRadiusMM; r -= veneerThicknessMM)
            radii.Add(r);

        return radii;
    }

    /// <summary>
    /// Fills missing cells in the grid with the average of their neighbors or a fallback radius.
    /// </summary>
    /// <param name="grid">The grid of radii.</param>
    /// <param name="fallbackRadius">The fallback radius to use if no neighbors are available.</param>
    private static void FillMissingCells(double[,] grid, double fallbackRadius)
    {
        int zBins = grid.GetLength(0);
        int angleBins = grid.GetLength(1);

        for (int z = 0; z < zBins; z++)
        {
            for (int a = 0; a < angleBins; a++)
            {
                if (!double.IsNaN(grid[z, a]))
                    continue;

                double sum = 0;
                int count = 0;

                for (int dz = -2; dz <= 2; dz++)
                {
                    for (int da = -3; da <= 3; da++)
                    {
                        int zz = z + dz;
                        int aa = WrapAngleIndex(a + da, angleBins);

                        if (zz < 0 || zz >= zBins)
                            continue;

                        if (double.IsNaN(grid[zz, aa]))
                            continue;

                        sum += grid[zz, aa];
                        count++;
                    }
                }

                grid[z, a] = count > 0
                    ? sum / count
                    : fallbackRadius;
            }
        }
    }

    /// <summary>
    /// Smooths the surface grid by averaging each cell with its neighbors for a specified number of passes.
    /// </summary>
    /// <param name="grid">The grid of radii to smooth.</param>
    /// <param name="passes">The number of smoothing passes to perform.</param>
    private static void SmoothSurface(double[,] grid, int passes)
    {
        if (passes <= 0)
            return;

        int zBins = grid.GetLength(0);
        int angleBins = grid.GetLength(1);

        for (int pass = 0; pass < passes; pass++)
        {
            double[,] copy = new double[zBins, angleBins];

            for (int z = 0; z < zBins; z++)
            {
                for (int a = 0; a < angleBins; a++)
                {
                    double sum = 0;
                    int count = 0;

                    for (int dz = -1; dz <= 1; dz++)
                    {
                        for (int da = -1; da <= 1; da++)
                        {
                            int zz = z + dz;
                            int aa = WrapAngleIndex(a + da, angleBins);

                            if (zz < 0 || zz >= zBins)
                                continue;

                            sum += grid[zz, aa];
                            count++;
                        }
                    }

                    copy[z, a] = sum / Math.Max(1, count);
                }
            }

            for (int z = 0; z < zBins; z++)
            {
                for (int a = 0; a < angleBins; a++)
                    grid[z, a] = copy[z, a];
            }
        }
    }

    /// <summary>
    /// Draws the header information on the provided image, including the title, core diameter, outer diameter, veneer thickness,
    /// log length, and number of revolutions.
    /// </summary>
    /// <param name="image">The image on which to draw the header.</param>
    /// <param name="surface">The surface model containing the outer radius and log length.</param>
    /// <param name="settings">The peel view settings.</param>
    /// <param name="usableSettings">The usable radius settings.</param>
    /// <param name="revolutions">The number of revolutions.</param>
    private static void DrawHeader(
        Mat image,
        SurfaceModel surface,
        PeelViewSettings settings,
        UsableRadiusSettings usableSettings,
        int revolutions)
    {
        double coreDiameterMM = usableSettings.CoreDiameterInches * MM_PER_INCH;

        CvInvoke.PutText(
            image,
            "Point-Cloud Veneer Peel Simulation",
            new Point(20, 32),
            Emgu.CV.CvEnum.FontFace.HersheySimplex,
            0.75,
            new MCvScalar(255, 255, 255),
            2);

        CvInvoke.PutText(
            image,
            $"Uses scanned radius at each Z/angle. Red = wane/missing wood. Yellow = thin cleanup. Stop core = {usableSettings.CoreDiameterInches:F1} in.",
            new Point(20, 60),
            Emgu.CV.CvEnum.FontFace.HersheySimplex,
            0.45,
            new MCvScalar(220, 220, 220),
            1);

        CvInvoke.PutText(
            image,
            $"Outer Dia: {(surface.OuterRadiusMM * 2.0 / MM_PER_INCH):F2} in   Core Dia: {(coreDiameterMM / MM_PER_INCH):F2} in   Veneer: {(usableSettings.VeneerThicknessMM / MM_PER_INCH):F3} in   Length: {(surface.LogLengthMM / MM_PER_INCH):F2} in   Revs: {revolutions}",
            new Point(20, 82),
            Emgu.CV.CvEnum.FontFace.HersheySimplex,
            0.42,
            new MCvScalar(200, 200, 200),
            1);
    }

    /// <summary>
    /// Draws the background for the peel strip area on the provided image, filling it with a specified color.
    /// </summary>
    /// <param name="image">The image on which to draw the background.</param>
    /// <param name="stripRect">The rectangle defining the peel strip area.</param>
    private static void DrawStripBackground(Mat image, Rectangle stripRect)
    {
        CvInvoke.Rectangle(
            image,
            stripRect,
            new MCvScalar(60, 48, 35),
            -1);
    }

    /// <summary>
    /// Draws the footer information on the provided image, including estimated peel wane area, severe wane percentage,
    /// </summary>
    /// <param name="image">The image on which to draw the footer.</param>
    /// <param name="stripRect">The rectangle defining the peel strip area.</param>
    /// <param name="settings">The peel view settings.</param>
    /// <param name="waneSettings">The wane settings.</param>
    /// <param name="totalSamples">The total number of samples.</param>
    /// <param name="waneSamples">The number of wane samples.</param>
    /// <param name="severeWaneSamples">The number of severe wane samples.</param>
    private static void DrawFooter(
        Mat image,
        Rectangle stripRect,
        PeelViewSettings settings,
        WaneSettings waneSettings,
        long totalSamples,
        long waneSamples,
        long severeWaneSamples)
    {
        double wanePercent =
            totalSamples > 0
                ? 100.0 * waneSamples / totalSamples
                : 0.0;

        double severePercent =
            totalSamples > 0
                ? 100.0 * severeWaneSamples / totalSamples
                : 0.0;

        CvInvoke.PutText(
            image,
            $"Estimated peel wane area: {wanePercent:F1}%   Severe wane: {severePercent:F1}%   Wane threshold: {waneSettings.WaneToleranceMM:F2} mm   Severe: {waneSettings.SevereWaneMM:F2} mm",
            new Point(20, stripRect.Bottom + 35),
            Emgu.CV.CvEnum.FontFace.HersheySimplex,
            0.45,
            new MCvScalar(255, 255, 255),
            1);

        CvInvoke.PutText(
            image,
            "Each vertical box is one 50 inch veneer sheet. Green = usable, yellow = check, red = trash/missing wood.",
            new Point(20, stripRect.Bottom + 60),
            Emgu.CV.CvEnum.FontFace.HersheySimplex,
            0.42,
            new MCvScalar(220, 220, 220),
            1);
    }

    /// <summary>
    /// Builds an empty image with a specified message, used when there is no data to display.
    /// </summary>
    /// <param name="message">The message to display on the empty image.</param>
    /// <returns>The created empty image with the specified message.</returns>
    private static Mat BuildEmpty(string message)
    {
        Mat image = new(260, 1000, Emgu.CV.CvEnum.DepthType.Cv8U, 3);
        image.SetTo(new MCvScalar(25, 25, 25));

        CvInvoke.PutText(
            image,
            message,
            new Point(30, 120),
            Emgu.CV.CvEnum.FontFace.HersheySimplex,
            0.7,
            new MCvScalar(255, 255, 255),
            2);

        return image;
    }

    /// <summary>
    /// Clamps an integer value between a specified minimum and maximum range.
    /// </summary>
    /// <param name="value">The value to clamp.</param>
    /// <param name="min">The minimum allowable value.</param>
    /// <param name="max">The maximum allowable value.</param>
    /// <returns>The clamped value.</returns>
    private static int ClampInt(int value, int min, int max)
    {
        if (value < min)
            return min;

        if (value > max)
            return max;

        return value;
    }

    /// <summary>
    /// Wraps an angle index to ensure it falls within the valid range of angle bins, handling negative and out-of-bounds values.
    /// </summary>
    /// <param name="value">The angle index to wrap.</param>
    /// <param name="angleBins">The total number of angle bins.</param>
    /// <returns>The wrapped angle index.</returns>
    private static int WrapAngleIndex(int value, int angleBins)
    {
        while (value < 0)
            value += angleBins;

        while (value >= angleBins)
            value -= angleBins;

        return value;
    }


    /// <summary>
    /// Calculates the specified percentile value from a list of double values. If the list is empty or null, returns 0.
    /// </summary>
    /// <param name="values">The list of double values from which to calculate the percentile.</param>
    /// <param name="percentile">The desired percentile (between 0.0 and 1.0).</param>
    /// <returns>The calculated percentile value.</returns>
    private static double Percentile(List<double> values, double percentile)
    {
        if (values == null || values.Count == 0)
            return 0;

        percentile = Math.Clamp(percentile, 0.0, 1.0);

        var sorted = values.OrderBy(v => v).ToList();

        double position = percentile * (sorted.Count - 1);
        int lower = (int)Math.Floor(position);
        int upper = (int)Math.Ceiling(position);

        if (lower == upper)
            return sorted[lower];

        double weight = position - lower;

        return sorted[lower] * (1.0 - weight) +
               sorted[upper] * weight;
    }
}
