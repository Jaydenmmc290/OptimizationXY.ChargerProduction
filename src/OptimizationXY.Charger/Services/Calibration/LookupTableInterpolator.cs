using OptimizationXY.Charger.Models.Calibration;


// =============================================================================
// LookupTableInterpolator.cs - laser row -> real radius (the core calibration maths)
// -----------------------------------------------------------------------------
// The cameras look down at the log at an angle, so where the laser line
// appears in the image (its row, "laser Y") depends on how far the surface is
// from the spindle axis. Calibration photographs reference objects of known
// radius (two tubes and the home cable at radius 0) and records, for every
// image column X, which laser row each radius produced. Each reference gives
// one "band" per column.
//
// At run time, for a detected laser point (X, laserY):
//   1. take the bands near column X (within 24 columns), each band reduced to
//      its median laser row and its known radius;
//   2. find the two bands whose laser rows bracket laserY;
//   3. interpolate the radius in a straight line between them.
// Points slightly outside the calibrated rows (2 px tolerance) are clamped.
// Points beyond the LARGEST reference may be extrapolated a little (see
// GetRadiusMMFromValidatedTable); anything else outside the range is rejected.
// =============================================================================
namespace OptimizationXY.Charger.Services.Calibration;

/// <summary>
/// Converts a detected laser point (image column, laser row) into a radius in
/// millimetres using the active scanner lookup table.
/// </summary>
public static class LookupTableInterpolator
{
    // At most this many calibration points are used for one column.
    private const int MaximumNearbyPoints = 600;
    // Calibration points within this many columns of X count as "near".
    private const int NearbyXWindowPixels = 24;
    // Laser rows up to this far outside the calibrated rows are accepted (clamped).
    private const double BoundaryTolerancePixels = 2.0;


    /// <summary>
    /// Interpolates the radius in millimeters from a scanner lookup table based on the provided image X coordinate and laser Y coordinate.
    /// </summary>
    /// <param name="table">The scanner lookup table.</param>
    /// <param name="x">The image X coordinate.</param>
    /// <param name="laserY">The detected laser row (pixels, sub-pixel allowed).</param>
    /// <param name="maximumLargeRadiusExtrapolationPixels">
    /// How many laser pixels beyond the LARGEST reference may be extrapolated
    /// (RotatingScan.MaximumCalibrationExtrapolationPixels). 0 = no extrapolation.
    /// </param>
    /// <returns>The interpolated radius in millimeters.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the lookup table is invalid or does not contain enough data for interpolation.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if the provided coordinates are outside the valid range.</exception>
    public static double GetRadiusMM(
        ScannerLookupTable table,
        int x,
        double laserY,
        double maximumLargeRadiusExtrapolationPixels = 0.0)
    {
        ArgumentNullException.ThrowIfNull(table);

        if (!table.IsValid)
        {
            throw new InvalidOperationException(
                "Lookup table is invalid.");
        }

        return GetRadiusMMFromValidatedTable(
            table,
            x,
            laserY,
            maximumLargeRadiusExtrapolationPixels);
    }

    /// <summary>
    /// Same as <see cref="GetRadiusMM"/> without re-checking
    /// <see cref="ScannerLookupTable.IsValid"/>. IsValid walks every calibration
    /// point and builds a string for each (about 1,900 per call); checking it for
    /// every stripe column of every frame cost about 0.3 s per frame. Callers
    /// must have checked IsValid for this table themselves (once per frame).
    /// The radius returned is identical.
    /// </summary>
    internal static double GetRadiusMMFromValidatedTable(
        ScannerLookupTable table,
        int x,
        double laserY,
        double maximumLargeRadiusExtrapolationPixels = 0.0)
    {
        ArgumentNullException.ThrowIfNull(table);

        if (x < 0 || x >= table.ImageWidth)
        {
            throw new ArgumentOutOfRangeException(
                nameof(x),
                $"Image X must be between 0 and {table.ImageWidth - 1} for the active calibration.");
        }

        if (!double.IsFinite(laserY) || laserY < 0 || laserY >= table.ImageHeight)
        {
            throw new ArgumentOutOfRangeException(
                nameof(laserY),
                $"Laser Y must be finite and between 0 and {table.ImageHeight - 1} for the active calibration.");
        }

        // The calibration bands for a column depend only on the calibration
        // table, never on the frame, so they are worked out once per loaded
        // table and reused for every frame of the scan.
        CalibrationBand[] bands = GetColumnBands(table, x);

        if (bands.Length < 2)
        {
            throw new InvalidOperationException(
                "Lookup table needs at least two physical radius bands near the requested image column.");
        }

        double minimumCalibratedLaserY = bands[0].LaserY;
        double maximumCalibratedLaserY = bands[^1].LaserY;

        // Optional, bounded extension past the LARGEST measured reference so
        // blocks slightly bigger than that reference can still be scanned. The
        // line through the two references nearest that end is continued for at
        // most the configured number of pixels. Nothing changes inside the
        // calibrated range, on the small-radius side, or when the allowance is 0.
        double extrapolationPixels =
            double.IsFinite(maximumLargeRadiusExtrapolationPixels)
                ? Math.Max(0.0, maximumLargeRadiusExtrapolationPixels)
                : 0.0;

        // Which end of the laser-row range is the big-radius end depends on the
        // camera geometry; work it out from the bands instead of assuming.
        bool largestRadiusAtLowLaserY =
            bands[0].RadiusMM >= bands[^1].RadiusMM;

        double lowLaserYLimit =
            minimumCalibratedLaserY -
            BoundaryTolerancePixels -
            (largestRadiusAtLowLaserY ? extrapolationPixels : 0.0);

        double highLaserYLimit =
            maximumCalibratedLaserY +
            BoundaryTolerancePixels +
            (largestRadiusAtLowLaserY ? 0.0 : extrapolationPixels);

        if (extrapolationPixels > 0.0 &&
            laserY >= lowLaserYLimit &&
            laserY <= highLaserYLimit)
        {
            bool beyondLargestReference = largestRadiusAtLowLaserY
                ? laserY < minimumCalibratedLaserY
                : laserY > maximumCalibratedLaserY;

            if (beyondLargestReference)
            {
                CalibrationBand edge = largestRadiusAtLowLaserY
                    ? bands[0]
                    : bands[^1];
                CalibrationBand inner = largestRadiusAtLowLaserY
                    ? bands[1]
                    : bands[^2];

                double edgeDeltaY = edge.LaserY - inner.LaserY;

                if (Math.Abs(edgeDeltaY) < 0.0001)
                    return edge.RadiusMM;

                // Continue the straight line through the two outermost (largest) references.
                double slopeMMPerPixel =
                    (edge.RadiusMM - inner.RadiusMM) / edgeDeltaY;

                return edge.RadiusMM +
                       (laserY - edge.LaserY) * slopeMMPerPixel;
            }
        }

        if (laserY < lowLaserYLimit ||
            laserY > highLaserYLimit)
        {
            throw new InvalidOperationException(
                $"Detected laser Y={laserY:F3} at image X={x} is outside the active calibration range " +
                $"{minimumCalibratedLaserY:F3} to {maximumCalibratedLaserY:F3}. " +
                "Use a reference range that covers this block or recalibrate before production processing.");
        }

        // Inside the tolerance: clamp to the calibrated rows, then find the
        // bracketing pair of bands (bands are sorted by laser row).
        double clampedLaserY = Math.Clamp(
            laserY,
            minimumCalibratedLaserY,
            maximumCalibratedLaserY);

        CalibrationBand lower = bands[0];
        CalibrationBand upper = bands[^1];

        for (int index = 1; index < bands.Length; index++)
        {
            if (bands[index].LaserY >= clampedLaserY)
            {
                lower = bands[index - 1];
                upper = bands[index];
                break;
            }
        }

        double deltaY = upper.LaserY - lower.LaserY;
        if (Math.Abs(deltaY) < 0.0001)
        {
            return (lower.RadiusMM + upper.RadiusMM) / 2.0;
        }

        // Linear interpolation: 0 at the lower band, 1 at the upper band.
        double interpolation =
            (clampedLaserY - lower.LaserY) / deltaY;

        return lower.RadiusMM +
               interpolation * (upper.RadiusMM - lower.RadiusMM);
    }


    // ------------------------------------------------------------
    // Per-column calibration bands (worked out once per loaded table)
    // ------------------------------------------------------------

    /// <summary>
    /// Calibration bands of every image column of one calibration table. They
    /// are all worked out together, once, the first time the table is used.
    /// </summary>
    private sealed class ColumnBandCache
    {
        public ColumnBandCache(ScannerLookupTable table)
        {
            Points = table.Points;
            PointCount = table.Points.Count;
            ImageWidth = table.ImageWidth;

            // ExecutionAndPublication: when several frames ask at the same time,
            // one of them builds the bands and the others wait for that result.
            AllColumns = new Lazy<CalibrationBand[][]>(
                () => BuildAllColumnBands(table),
                LazyThreadSafetyMode.ExecutionAndPublication);
        }

        // Copies of the table's points, point count and image width, plus the bands
        // of every column (built on first use).
        public List<LookupPoint> Points { get; }
        public int PointCount { get; }
        public int ImageWidth { get; }
        public Lazy<CalibrationBand[][]> AllColumns { get; }
    }

    // Keyed on the table object itself. The calibration file is read into a new
    // table object for every scan, so every scan starts from an empty cache and
    // can never use bands from a different or older calibration. A table that is
    // no longer used is released together with its cache.
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ScannerLookupTable, ColumnBandCache> BandCaches = new();

    /// <summary>
    /// Returns the calibration bands for one image column, ordered by laser Y.
    /// The result is exactly what <see cref="BuildColumnBands"/> returns; it is
    /// only remembered so the same table search is not repeated for every frame.
    /// Safe to call from several threads at once.
    /// </summary>
    private static CalibrationBand[] GetColumnBands(
        ScannerLookupTable table,
        int x)
    {
        ColumnBandCache cache = BandCaches.GetValue(
            table,
            static key => new ColumnBandCache(key));

        // If the table was given a different point list or size after the cache
        // was created, the remembered bands no longer describe it. Start again.
        if (!ReferenceEquals(cache.Points, table.Points) ||
            cache.PointCount != table.Points.Count ||
            cache.ImageWidth != table.ImageWidth)
        {
            cache = new ColumnBandCache(table);
            BandCaches.AddOrUpdate(table, cache);
        }

        CalibrationBand[][] allColumns = cache.AllColumns.Value;

        if (x >= 0 && x < allColumns.Length)
        {
            CalibrationBand[] bands = allColumns[x];

            if (bands is not null)
                return bands;
        }

        // Column not prepared (it could not be built ahead of time): work it out
        // directly, exactly as before, so any error is raised here as it always was.
        return BuildColumnBands(table, x);
    }

    /// <summary>
    /// Works out the bands of every image column of a table. Columns are
    /// independent, so they are built in parallel. A column that cannot be built
    /// is left empty and is worked out directly when it is asked for.
    /// </summary>
    private static CalibrationBand[][] BuildAllColumnBands(ScannerLookupTable table)
    {
        int width = Math.Max(0, table.ImageWidth);
        CalibrationBand[][] allColumns = new CalibrationBand[width][];

        Parallel.For(0, width, x =>
        {
            try
            {
                allColumns[x] = BuildColumnBands(table, x);
            }
            catch (Exception)
            {
                // Left empty on purpose; see GetColumnBands.
            }
        });

        return allColumns;
    }

    /// <summary>
    /// Works out the calibration bands near one image column from the full
    /// calibration table. This is the original, unchanged calculation.
    /// </summary>
    private static CalibrationBand[] BuildColumnBands(
        ScannerLookupTable table,
        int x)
    {
        List<LookupPoint> nearbyByX = [.. table.Points
            .Where(point => Math.Abs(point.X - x) <= NearbyXWindowPixels)
            .OrderBy(point => Math.Abs(point.X - x))
            .Take(MaximumNearbyPoints)];

        // Fewer than two calibration points near this column (e.g. at the image
        // edge): take the closest points from each reference band instead.
        if (nearbyByX.Count < 2)
        {
            // Fallback: Get points from different bands, prioritizing band diversity over proximity
            var pointsByBand = table.Points
                .GroupBy(point => point.RadiusBandId > 0
                    ? point.RadiusBandId
                    : StableLegacyBandId(point.RadiusMM))
                .ToList();

            if (pointsByBand.Count < 2)
            {
                // Not enough bands in the entire table; use all closest points
                List<LookupPoint> value = [.. table.Points
                    .OrderBy(point => Math.Abs(point.X - x))
                    .Take(MaximumNearbyPoints)];
                nearbyByX = value;
            }
            else
            {
                // Get closest points from each band to ensure we have multiple bands
                var pointsFromEachBand = pointsByBand
                    .SelectMany(band => band
                        .OrderBy(point => Math.Abs(point.X - x))
                        .Take(MaximumNearbyPoints / pointsByBand.Count))
                    .ToList();

                nearbyByX = pointsFromEachBand.Count >= 2 
                    ? pointsFromEachBand 
                    : [.. table.Points
                        .OrderBy(point => Math.Abs(point.X - x))
                        .Take(MaximumNearbyPoints)];
            }
        }

        // One band per reference: median laser row (robust to a few bad points)
        // and the reference's radius, sorted by laser row.
        return [.. nearbyByX
            .GroupBy(point => point.RadiusBandId > 0
                ? point.RadiusBandId
                : StableLegacyBandId(point.RadiusMM))
            .Select(group => new CalibrationBand(
                LaserY: Median(group.Select(point => point.LaserY)),
                RadiusMM: group.Average(point => point.RadiusMM)))
            .OrderBy(band => band.LaserY)];
    }

    /// <summary>
    /// Band id for old calibration points that have no RadiusBandId: the radius in
    /// thousandths of a millimetre, so points of the same reference group together.
    /// </summary>
    /// <param name="radiusMM">The radius in millimeters.</param>
    /// <returns>The stable legacy band ID.</returns>
    private static int StableLegacyBandId(double radiusMM) =>
        checked((int)Math.Round(radiusMM * 1000.0));
    
    /// <summary>
    /// Calculates the median of a sequence of double values.
    /// </summary>
    /// <param name="values">The sequence of double values.</param>
    /// <returns>The median value.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the sequence is empty.</exception>
    private static double Median(IEnumerable<double> values)
    {
        double[] sorted = [.. values.OrderBy(value => value)];

        if (sorted.Length == 0)
        {
            throw new InvalidOperationException(
                "Cannot calculate a median from an empty calibration band.");
        }

        int middle = sorted.Length / 2;
        return sorted.Length % 2 == 0
            ? (sorted[middle - 1] + sorted[middle]) / 2.0
            : sorted[middle];
    }

    /// <summary>
    /// Represents a calibration band with a laser Y coordinate and a corresponding radius in millimeters.
    /// </summary>
    /// <param name="LaserY">The laser Y coordinate.</param>
    /// <param name="RadiusMM">The radius in millimeters.</param>
    private sealed record CalibrationBand(
        double LaserY,
        double RadiusMM);
}
