
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
