using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;
using OptimizationXY.Charger.Models.Analysis;
using OptimizationXY.Charger.Models.Geometry;

namespace OptimizationXY.Charger.Services.Analysis;

public static class OvalityAnalyzer
{

    // Analyzes the ovality of a spindle based on binned radius field data.
    public static OvalityResult Analyze(List<BinnedRadiusFieldPoint> binnedField,int minAngleBinsPerSlice = 36)
    {
        if (binnedField == null || binnedField.Count == 0)
            throw new InvalidOperationException("Binned radius field is empty.");

        var slices = binnedField
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

    // Analyzes a single slice of binned radius field points to determine ovality and related parameters.
    private static OvalityResult AnalyzeSlice(List<BinnedRadiusFieldPoint> slicePoints,double z)
    {
        int n = slicePoints.Count;

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

        double secondAmp = Math.Sqrt(a2 * a2 + b2 * b2);

        double majorRadius = a0 + secondAmp;
        double minorRadius = a0 - secondAmp;

        double majorDiameter = majorRadius * 2.0;
        double minorDiameter = minorRadius * 2.0;
        double ovality = majorDiameter - minorDiameter;

        double ovalityPercent =
            majorDiameter > 0
                ? ovality / majorDiameter * 100.0
                : 0.0;

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
