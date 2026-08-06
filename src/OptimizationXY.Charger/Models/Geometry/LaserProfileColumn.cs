
/// <summary>
/// Represents a single column of laser profile data, including the X coordinate, the Y coordinates of the laser center, top, and bottom,
/// the height of the stripe in pixels, and a confidence value for the measurement.
/// </summary>
namespace OptimizationXY.Charger.Models.Geometry;


/// <summary>
/// Represents a single column of laser profile data, including the X coordinate,
/// the Y coordinates of the laser center, top, and bottom,
/// </summary>
public class LaserProfileColumn 
{

    /// <summary>
    /// Initializes a new instance of the <see cref="LaserProfileColumn"/> class.
    /// </summary>
    public LaserProfileColumn() { }


    /// <summary>
    /// Gets or sets the X coordinate of the laser profile column in the image.
    /// </summary>
    public int X { get; set; } 

    /// <summary>
    /// Gets or sets the Y coordinate of the laser center in the image, which may be a floating-point value for sub-pixel accuracy.
    /// </summary>
    public double LaserCenterY { get; set; }  

    /// <summary>
    /// Gets or sets the Y coordinate of the top of the laser stripe in the image.
    /// </summary>
    public int TopY { get; set; }  

    /// <summary>
    /// Gets or sets the Y coordinate of the bottom of the laser stripe in the image.
    /// </summary>
    public int BottomY { get; set; }  

    /// <summary>
    /// Gets or sets the height of the laser stripe in pixels, calculated as BottomY - TopY.
    /// </summary>
    public int StripeHeightPixels { get; set; }  

    /// <summary>
    /// Gets or sets a confidence value for the measurement, which could be based on factors
    /// such as the intensity of the laser reflection or the quality of the edge detection.
    /// </summary>
    public double Confidence { get; set; }  
}
