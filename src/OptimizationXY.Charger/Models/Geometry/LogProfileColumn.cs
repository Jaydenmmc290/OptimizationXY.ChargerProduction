
/// <summary>
/// Represents a column in the log profile, containing geometric information about a specific X coordinate.
/// </summary>
namespace OptimizationXY.Charger.Models.Geometry;

public class LogProfileColumn 
{
    /// <summary>
    /// Gets or sets the X coordinate of the column.
    /// </summary>
    public int X { get; set; }  



    /// <summary>
    /// Gets or sets the Y coordinate of the center of the column, calculated as the average of TopY and BottomY, representing the midpoint in the log profile at this X coordinate.
    /// </summary>
    public double CenterY { get; set; }  

    /// <summary>
    /// Gets or sets the diameter of the column in millimeters, calculated from the pixel diameter using the appropriate conversion factor.
    /// </summary>
    public double DiameterMM { get; set; }  

    /// <summary>
    /// Gets or sets the radius of the column in millimeters, calculated as half of the diameter,
    /// representing the distance from the center to the edge of the column in the log profile at this X coordinate.
    /// </summary>
    public double RadiusMM { get; set; } 
}
