namespace OptimizationXY.Charger.Models.Geometry;

public class LocalCenterlineSlice // Represents a slice of the centerline at a specific Z height, including the center point and mean radius of the points in that slice
{
    public double ZMM { get; set; }  // Z height of the slice in millimeters
    public double CenterXMM { get; set; }  // X coordinate of the center point in millimeters
    public double CenterYMM { get; set; }  // Y coordinate of the center point in millimeters
    public double MeanRadiusMM { get; set; }  // Mean radius of the points in this slice from the center point in millimeters
    public int PointCount { get; set; }  //

}
