
using MathNet.Numerics.LinearAlgebra;
using OptimizationXY.Charger.Models.Geometry;


namespace OptimizationXY.Charger.Services.Geometry;


/// <summary>
/// Provides mathematical services for fitting centerlines to point clouds and analyzing local centerline slices.
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
    /// Fits local centerline slices to a point cloud by dividing the points into slices based on their Z-coordinate and computing the
    /// average position and mean radius for each slice. The method filters out invalid points and requires a minimum
    /// number of valid points per slice to include it in the result.
    /// </summary>
    /// <param name="points">The list of 3D points representing the point cloud.</param>
    /// <param name="sliceThicknessMM">The thickness of each slice in millimeters.</param>
    /// <param name="minPointsPerSlice">The minimum number of valid points required per slice.</param>
    /// <returns>A list of LocalCenterlineSlice objects representing the fitted local centerline slices.</returns>
    /// <summary>
    /// Least-squares (algebraic) circle fit of one cross-section. Returns false
    /// when the fit is degenerate so the caller can keep the centroid.
    /// </summary>
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
