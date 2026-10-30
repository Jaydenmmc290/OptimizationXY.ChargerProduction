
// SurfaceModel.cs - the log surface as a regular radius grid.
namespace OptimizationXY.Charger.Models.Geometry;


/// <summary>
/// The log surface as a regular grid: radius (mm) for each Z row and angle
/// column. Used by the peel simulation, which "turns" this surface around a
/// chosen axis.
/// </summary>
public sealed class SurfaceModel
{

    /// <summary>
    /// Gets or sets the radius grid, indexed [zIndex, angleIndex], in millimetres.
    /// </summary>
    public double[,] RadiusByZAndAngle { get; set; } = new double[1, 1];

    /// <summary>
    /// Gets or sets the minimum Z value of the surface model.
    /// </summary>
    public double MinZ { get; set; }

    /// <summary>
    /// Gets or sets the maximum Z value of the surface model.
    /// </summary>
    public double MaxZ { get; set; }

    /// <summary>
    /// Gets the length of the surface model in millimeters, ensuring a minimum value of 0.0001 to avoid zero-length issues.
    /// </summary>
    public double LogLengthMM => Math.Max(MaxZ - MinZ, 0.0001);

    /// <summary>
    /// Gets or sets the outer radius of the surface model in millimeters.
    /// </summary>
    public double OuterRadiusMM { get; set; }

    /// <summary>
    /// Gets or sets the average radius of the surface model in millimeters.
    /// </summary>
    public double AverageRadiusMM { get; set; }
}
