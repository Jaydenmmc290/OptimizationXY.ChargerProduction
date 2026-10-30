using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;
using OptimizationXY.Charger.Models.Analysis;
using OptimizationXY.Charger.Models.Geometry;

namespace OptimizationXY.Charger.Services.Analysis;

/// <summary>
/// Finds the most out-of-round cross-section of the LOG from the binned radius
/// field. Diagnostic only - reported and saved, it does not move the spindles.
/// </summary>
/// <remarks>
/// For each Z slice the radius-versus-angle data is fitted with a short
/// Fourier series (see <see cref="AnalyzeSlice"/>). The second harmonic of
/// that fit is the oval part of the shape. The slice with the largest
/// ovality (major minus minor diameter) is returned.
/// </remarks>
public static class OvalityAnalyzer
{

    /// <summary>
    /// Analyzes every Z slice of the binned radius field and returns the one with the most ovality.
    /// </summary>
    /// <param name="binnedField">The list of binned radius field points.</param>
    /// <param name="minAngleBinsPerSlice">The minimum number of angle bins required per slice for analysis.</param>
    /// <returns>The result of the ovality analysis.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the binned radius field is empty or if no valid slices are available for analysis.</exception>
    public static OvalityResult Analyze(List<BinnedRadiusFieldPoint> binnedField,int minAngleBinsPerSlice = 36)
    {
        if (binnedField == null || binnedField.Count == 0)
            throw new InvalidOperationException("Binned radius field is empty.");

        var slices = binnedField
            // Group the field into cross-section slices (one per Z bin), ordered along the log.
            .GroupBy(p => p.ZBinMM)
            .OrderBy(g => g.Key)
            .ToList();

        OvalityResult? worst = null;
        int validSlices = 0;

        foreach (var slice in slices)
        {
            var points = slice
                .Where(p =>
                    p.MeanRadiusMM > 0 &&
                    !double.IsNaN(p.MeanRadiusMM) &&
                    !double.IsInfinity(p.MeanRadiusMM))
                .OrderBy(p => p.AngleBinDeg)
                .ToList();

            // Skip slices with too few valid angle bins - a fit through a few points
            // would report false ovality. Default 36 bins (every 10 degrees).
            if (points.Count < minAngleBinsPerSlice)
                continue;

            var result = AnalyzeSlice(points, slice.Key);
            validSlices++;

            if (worst == null || result.OvalityMM > worst.OvalityMM)
                worst = result;
        }

        if (worst == null)
            throw new InvalidOperationException("No valid slices for ovality analysis.");

        worst.SliceCount = validSlices;
        return worst;
    }

    /// <summary>
    /// Least-squares fit of r(theta) = A0 + A1 cos t + B1 sin t + A2 cos 2t + B2 sin 2t
    /// to one slice and converts the coefficients into diameters and an orientation.
    /// </summary>
    /// <param name="slicePoints">The list of binned radius field points for the slice.</param>
    /// <param name="z">The Z-coordinate of the slice.</param>
    /// <returns>The result of the ovality analysis for the slice.</returns>
    private static OvalityResult AnalyzeSlice(List<BinnedRadiusFieldPoint> slicePoints,double z)
    {
        int n = slicePoints.Count;

        // Build the least-squares system: one row per angle bin, one column per
        // term of the series [1, cos t, sin t, cos 2t, sin 2t]. Solving with QR
        // gives the coefficients that best match the measured radii.
        Matrix<double> design = DenseMatrix.Create(n, 5, 0.0);
        Vector<double> radii = DenseVector.Create(n, 0.0);

        for (int i = 0; i < n; i++)
        {
            double theta = slicePoints[i].AngleBinDeg * Math.PI / 180.0;

            design[i, 0] = 1.0;
            design[i, 1] = Math.Cos(theta);
            design[i, 2] = Math.Sin(theta);
            design[i, 3] = Math.Cos(2.0 * theta);
            design[i, 4] = Math.Sin(2.0 * theta);

            radii[i] = slicePoints[i].MeanRadiusMM;
        }

        Vector<double> coeff =
            design.QR().Solve(radii);

        double a0 = coeff[0];
        double a1 = coeff[1];
        double b1 = coeff[2];
        double a2 = coeff[3];
        double b2 = coeff[4];

        // The second harmonic is the "oval" part: it adds secondAmp on the long
        // axis and subtracts it on the short axis. (A1/B1 only move the centre.)
        double secondAmp = Math.Sqrt(a2 * a2 + b2 * b2);

        double majorRadius = a0 + secondAmp;
        double minorRadius = a0 - secondAmp;

        double majorDiameter = majorRadius * 2.0;
        double minorDiameter = minorRadius * 2.0;
        double ovality = majorDiameter - minorDiameter;

        // Percentage of the MAJOR diameter.
        double ovalityPercent =
            majorDiameter > 0
                ? ovality / majorDiameter * 100.0
                : 0.0;

        // Angle of the long axis. cos(2t) repeats every 180 degrees, so the
        // angle is halved and folded into 0..180 degrees.
        double orientationRad = 0.5 * Math.Atan2(b2, a2);
        double orientationDeg = orientationRad * 180.0 / Math.PI;

        if (orientationDeg < 0)
            orientationDeg += 180.0;

        return new OvalityResult
        {
            WorstZMM = z,

            MeanRadiusMM = a0,
            MajorDiameterMM = majorDiameter,
            MinorDiameterMM = minorDiameter,
            OvalityMM = ovality,
            OvalityPercent = ovalityPercent,

            HarmonicA0 = a0,
            HarmonicA1 = a1,
            HarmonicB1 = b1,
            HarmonicA2 = a2,
            HarmonicB2 = b2,
            SecondHarmonicAmplitudeMM = secondAmp,
            OrientationDeg = orientationDeg,

            WorstSlicePointCount = n
        };
    }
}
