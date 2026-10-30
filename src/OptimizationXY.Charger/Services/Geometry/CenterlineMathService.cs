
using MathNet.Numerics.LinearAlgebra;
using OptimizationXY.Charger.Models.Geometry;


namespace OptimizationXY.Charger.Services.Geometry;


/// <summary>
/// Fits the log's centreline. Pipeline order:
/// <list type="number">
/// <item><description><see cref="FitPcaAxis"/> - quick straight line through the whole point cloud.</description></item>
/// <item><description><see cref="FitLocalCenterlines"/> - circle centre of each thin slice along the log.</description></item>
/// <item><description><see cref="RefineAxisThroughSliceCenters"/> - straight line fitted through those slice centres.</description></item>
/// </list>
/// All coordinates are millimetres in the point-cloud frame.
/// </summary>
public static class CenterlineMathService
{

    /// <summary>
    /// Fits a centerline to a point cloud using Principal Component Analysis (PCA). The method computes
    /// the mean position of the points and the covariance matrix, then performs eigenvalue decomposition to find the principal axis
    /// of the point cloud. It returns a CenterlineResult containing the origin and direction of the fitted centerline.
    /// The method filters out invalid points and requires a minimum number of valid points to perform the fitting.
    /// </summary>
    /// <param name="points">The list of 3D points to fit the centerline to.</param>
    /// <returns>A CenterlineResult containing the origin and direction of the fitted centerline.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the PCA fitting fails due to insufficient valid points or invalid PCA direction.</exception>
    public static CenterlineResult FitPcaAxis(List<Point3D> points)
    {
        try
        {
            // Use MathNet's pure .NET maths (no native MKL library needed on the PC).
            MathNet.Numerics.Control.UseManaged();

            points = [.. points
                .Where(p =>
                    !double.IsNaN(p.X) && !double.IsInfinity(p.X) &&
                    !double.IsNaN(p.Y) && !double.IsInfinity(p.Y) &&
                    !double.IsNaN(p.Z) && !double.IsInfinity(p.Z))];

            if (points.Count < 10)
                throw new InvalidOperationException("Not enough valid point cloud points.");

            double meanX = points.Average(p => p.X);
            double meanY = points.Average(p => p.Y);
            double meanZ = points.Average(p => p.Z);

            // PCA: build the 3x3 covariance matrix of the points around their mean.
            // The eigenvector with the largest eigenvalue is the direction the points
            // are most spread out in - for a log, its length direction.
            var covariance = Matrix<double>.Build.Dense(3, 3);

            foreach (var p in points)
            {
                double x = p.X - meanX;
                double y = p.Y - meanY;
                double z = p.Z - meanZ;

                covariance[0, 0] += x * x;
                covariance[0, 1] += x * y;
                covariance[0, 2] += x * z;
                covariance[1, 0] += y * x;
                covariance[1, 1] += y * y;
                covariance[1, 2] += y * z;
                covariance[2, 0] += z * x;
                covariance[2, 1] += z * y;
                covariance[2, 2] += z * z;
            }

            covariance /= points.Count;

            var evd = covariance.Evd();

            int bestIndex = 0;
            double bestValue = double.MinValue;

            for (int i = 0; i < evd.EigenValues.Count; i++)
            {
                double value = evd.EigenValues[i].Real;

                if (value > bestValue)
                {
                    bestValue = value;
                    bestIndex = i;
                }
            }

            // NOTE: an eigenvector's sign is arbitrary, so the direction may point
            // toward -Z. Callers that care about direction must check the Z sign.
            double dx = evd.EigenVectors[0, bestIndex];
            double dy = evd.EigenVectors[1, bestIndex];
            double dz = evd.EigenVectors[2, bestIndex];

            double length = System.Math.Sqrt(dx * dx + dy * dy + dz * dz);

            if (length <= 0)
                throw new InvalidOperationException("Invalid PCA direction.");

            dx /= length;
            dy /= length;
            dz /= length;

            return new CenterlineResult
            {
                OriginX = meanX,
                OriginY = meanY,
                OriginZ = meanZ,
                DirectionX = dx,
                DirectionY = dy,
                DirectionZ = dz
            };
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("FitPcaAxis failed: " + ex.Message, ex);
        }
    }

    /// <summary>
    /// Refines the global PCA axis so that it passes through the fitted centres
    /// of the cross-sections.
    ///
    /// The PCA origin is the plain average of every surface point, which is
    /// pulled toward whichever side of the block has more samples. A straight
    /// line fitted through the per-slice circle centres is not affected by that,
    /// so the shape analysis built on the centreline (radius field, ovality,
    /// low-wood map, diameters) is measured from the true middle of the block.
    /// The PCA result is returned unchanged when there are too few slices.
    /// </summary>
    public static CenterlineResult RefineAxisThroughSliceCenters(
        CenterlineResult pcaAxis,
        List<LocalCenterlineSlice> slices,
        int minimumSlices = 10)
    {
        ArgumentNullException.ThrowIfNull(pcaAxis);

        if (slices == null || slices.Count < minimumSlices)
            return pcaAxis;

        // Ordinary least-squares line fit of slice centre X and Y against Z:
        // slope = sum((z - meanZ)(x - meanX)) / sum((z - meanZ)^2), same for Y.
        double meanZ = slices.Average(s => s.ZMM);
        double meanX = slices.Average(s => s.CenterXMM);
        double meanY = slices.Average(s => s.CenterYMM);

        double sumZZ = 0.0;
        double sumZX = 0.0;
        double sumZY = 0.0;

        foreach (LocalCenterlineSlice slice in slices)
        {
            double dz = slice.ZMM - meanZ;
            sumZZ += dz * dz;
            sumZX += dz * (slice.CenterXMM - meanX);
            sumZY += dz * (slice.CenterYMM - meanY);
        }

        if (!double.IsFinite(sumZZ) || sumZZ < 1.0)
            return pcaAxis;

        // X and Y of the centreline change by this much per millimetre of Z.
        double slopeX = sumZX / sumZZ;
        double slopeY = sumZY / sumZZ;

        // Keep the PCA origin's Z so the origin stays near the middle of the log,
        // and put X/Y on the fitted line at that Z.
        double originZ = double.IsFinite(pcaAxis.OriginZ)
            ? pcaAxis.OriginZ
            : meanZ;

        double originX = meanX + slopeX * (originZ - meanZ);
        double originY = meanY + slopeY * (originZ - meanZ);

        // Direction (slopeX, slopeY, 1) normalised to length 1. Because Z is +1
        // before normalising, this direction always points toward +Z (the head).
        double length = Math.Sqrt(slopeX * slopeX + slopeY * slopeY + 1.0);

        if (!double.IsFinite(originX) ||
            !double.IsFinite(originY) ||
            !double.IsFinite(length) ||
            length <= 0.0)
        {
            return pcaAxis;
        }

        return new CenterlineResult
        {
            OriginX = originX,
            OriginY = originY,
            OriginZ = originZ,
            DirectionX = slopeX / length,
            DirectionY = slopeY / length,
            DirectionZ = 1.0 / length
        };
    }

    /// <summary>
    /// Least-squares (algebraic) circle fit of one cross-section. Returns false
    /// when the fit is degenerate so the caller can keep the centroid.
    /// </summary>
    /// <remarks>
    /// "Kasa" algebraic fit: a circle satisfies x^2 + y^2 = A x + B y + C, which
    /// is linear in A, B, C, so it can be solved directly with a 3x3 system
    /// (Cramer's rule below). Coordinates are shifted by the point average first
    /// (u, v) for numerical accuracy; the centre is mean + (A/2, B/2).
    /// </remarks>
    private static bool TryFitCircleCenter(
        List<Point3D> points,
        double meanX,
        double meanY,
        out double centerX,
        out double centerY)
    {
        centerX = meanX;
        centerY = meanY;

        int n = points.Count;
        if (n < 6)
            return false;

        double suu = 0.0, suv = 0.0, svv = 0.0;
        double su = 0.0, sv = 0.0;
        double suz = 0.0, svz = 0.0, sz = 0.0;

        foreach (Point3D p in points)
        {
            double u = p.X - meanX;
            double v = p.Y - meanY;
            double z = u * u + v * v;

            suu += u * u;
            suv += u * v;
            svv += v * v;
            su += u;
            sv += v;
            suz += u * z;
            svz += v * z;
            sz += z;
        }

        // Solve   | suu suv su | |A|   | suz |
        //         | suv svv sv | |B| = | svz |     centre = mean + (A/2, B/2)
        //         | su  sv  n  | |C|   | sz  |
        double det =
            suu * (svv * n - sv * sv) -
            suv * (suv * n - sv * su) +
            su * (suv * sv - svv * su);

        double scale = Math.Max(1.0, suu + svv);
        if (!double.IsFinite(det) || Math.Abs(det) < 1e-9 * scale * scale)
            return false;

        double detA =
            suz * (svv * n - sv * sv) -
            suv * (svz * n - sv * sz) +
            su * (svz * sv - svv * sz);

        double detB =
            suu * (svz * n - sv * sz) -
            suz * (suv * n - sv * su) +
            su * (suv * sz - svz * su);

        double fittedX = meanX + 0.5 * detA / det;
        double fittedY = meanY + 0.5 * detB / det;

        if (!double.IsFinite(fittedX) || !double.IsFinite(fittedY))
            return false;

        // A centre further from the centroid than the mean radius is not a
        // believable fit for a log cross-section.
        double meanRadius = Math.Sqrt(sz / n);
        double shift = Math.Sqrt(
            (fittedX - meanX) * (fittedX - meanX) +
            (fittedY - meanY) * (fittedY - meanY));

        if (shift > meanRadius)
            return false;

        centerX = fittedX;
        centerY = fittedY;
        return true;
    }

    /// <summary>
    /// Cuts the point cloud into slices of <paramref name="sliceThicknessMM"/>
    /// along Z and finds each slice's centre (least-squares circle fit, falling
    /// back to the point average) and mean radius about that centre. Slices with
    /// fewer than <paramref name="minPointsPerSlice"/> points are skipped.
    /// </summary>
    /// <param name="points">The point cloud.</param>
    /// <param name="sliceThicknessMM">Thickness of each slice, millimetres.</param>
    /// <param name="minPointsPerSlice">Minimum points a slice needs to be used.</param>
    /// <returns>The slices, ordered from the tail end (low Z) to the head end.</returns>
    public static List<LocalCenterlineSlice> FitLocalCenterlines(
List<Point3D> points,
double sliceThicknessMM,
int minPointsPerSlice)
    {
        if (points == null || points.Count < minPointsPerSlice)
            return [];

        var validPoints = points
            .Where(p =>
                !double.IsNaN(p.X) && !double.IsInfinity(p.X) &&
                !double.IsNaN(p.Y) && !double.IsInfinity(p.Y) &&
                !double.IsNaN(p.Z) && !double.IsInfinity(p.Z))
            .ToList();

        if (validPoints.Count < minPointsPerSlice)
            return [];

        var slices = validPoints
            .GroupBy(p => Math.Floor(p.Z / sliceThicknessMM))
            .Where(g => g.Count() >= minPointsPerSlice)
            .OrderBy(g => g.Key)
            .ToList();

        List<LocalCenterlineSlice> result = [];

        foreach (var slice in slices)
        {
            var slicePoints = slice.ToList();

            double centerX = slicePoints.Average(p => p.X);
            double centerY = slicePoints.Average(p => p.Y);
            double centerZ = slicePoints.Average(p => p.Z);

            // The plain average of surface points is pulled toward whichever side
            // of the log has more samples (dropped frames, uneven coverage). A
            // least-squares circle fit locates the centre from the shape instead.
            if (TryFitCircleCenter(
                    slicePoints,
                    centerX,
                    centerY,
                    out double fittedX,
                    out double fittedY))
            {
                centerX = fittedX;
                centerY = fittedY;
            }

            double meanRadius = slicePoints
                .Select(p =>
                {
                    double dx = p.X - centerX;
                    double dy = p.Y - centerY;
                    return Math.Sqrt(dx * dx + dy * dy);
                })
                .Average();

            result.Add(new LocalCenterlineSlice
            {
                ZMM = centerZ,
                CenterXMM = centerX,
                CenterYMM = centerY,
                MeanRadiusMM = meanRadius,
                PointCount = slicePoints.Count
            });
        }

        return result;
    }
}
