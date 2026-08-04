namespace OptimizationXY.Charger.Models.Geometry;

public class CenterlineResult //    Represents the result of a centerline calculation, including the origin and direction of the centerline.
{
    public double OriginX { get; set; }  //    The X coordinate of the origin point of the centerline.
    public double OriginY { get; set; }  //    The Y coordinate of the origin point of the centerline.
    public double OriginZ { get; set; }   //    The Z coordinate of the origin point of the centerline.

    public double DirectionX { get; set; }  //    The X component of the direction vector of the centerline.
    public double DirectionY { get; set; }  //    The Y component of the direction vector of the centerline.
    public double DirectionZ { get; set; }  //    The Z component of the direction vector of the centerline.
}
