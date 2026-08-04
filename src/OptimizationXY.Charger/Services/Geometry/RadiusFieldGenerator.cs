using OptimizationXY.Charger.Models.Geometry;

namespace OptimizationXY.Charger.Services.Geometry;

public static class RadiusFieldGenerator
{
    /// <summary>
    /// Generates a radius field from a point cloud and fitted centerline.
    /// Each point contains the original XY coordinates, source Z position,
    /// scanner-plane circumferential angle, and distance from the fitted centerline.
    /// The scanner-plane angle basis must be verified during commissioning.
    /// </summary>
    public static List<RadiusFieldPoint> Build(
        List<Point3D> pointCloud,
        CenterlineResult centerline)
    {
        if (pointCloud == null || pointCloud.Count == 0)
        {
            return new List<RadiusFieldPoint>();
        }

        if (centerline == null)
        {
            throw new InvalidOperationException("Fit centerline first.");
        }

        List<RadiusFieldPoint> field = new();

        double ox = centerline.OriginX;
        double oy = centerline.OriginY;
        double oz = centerline.OriginZ;

        double dxAxis = centerline.DirectionX;
        double dyAxis = centerline.DirectionY;
        double dzAxis = centerline.DirectionZ;

        double axisLength = Math.Sqrt(
            dxAxis * dxAxis +
            dyAxis * dyAxis +
            dzAxis * dzAxis);

        if (!double.IsFinite(axisLength) || axisLength <= 0)
        {
            throw new InvalidOperationException("Invalid centerline direction.");
        }

        dxAxis /= axisLength;
        dyAxis /= axisLength;
        dzAxis /= axisLength;

        foreach (var p in pointCloud)
        {
            double vx = p.X - ox;
            double vy = p.Y - oy;
            double vz = p.Z - oz;

            // Distance along fitted centerline
            double t =
                vx * dxAxis +
                vy * dyAxis +
                vz * dzAxis;

            // Closest point on centerline
            double cx = ox + t * dxAxis;
            double cy = oy + t * dyAxis;
            double cz = oz + t * dzAxis;

            // Radial vector from centerline to surface point
            double rx = p.X - cx;
            double ry = p.Y - cy;
            double rz = p.Z - cz;

            double radius = Math.Sqrt(
                rx * rx +
                ry * ry +
                rz * rz);

            // Circumferential angle is referenced to the scanner XY plane.
            // Commissioning must verify this basis for the physical scanner geometry.
            double angle = Math.Atan2(ry, rx) * 180.0 / Math.PI;

            if (angle < 0)
            {
                angle += 360.0;
            }

            field.Add(new RadiusFieldPoint
            {
                Z = p.Z,
                X = p.X,
                Y = p.Y,
                RadiusMM = radius,
                AngleDeg = angle
            });
        }

        return field;
    }
}
