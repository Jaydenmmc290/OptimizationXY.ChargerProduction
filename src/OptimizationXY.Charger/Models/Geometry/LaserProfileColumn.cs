

namespace OptimizationXY.Charger.Models.Geometry // This class represents a single column of laser profile data, including the X coordinate,
                                                  // the Y coordinates of the laser center, top, and bottom, the height of the stripe
                                                  // in pixels, and a confidence value for the measurement.
{
    public class LaserProfileColumn // This class represents a single column of laser profile data, including the X coordinate,
                                    // the Y coordinates of the laser center, top, and bottom, the height of the stripe
                                    // in pixels, and a confidence value for the measurement.
    {
        public int X { get; set; }  // The X coordinate of the column in the image.

        public double LaserCenterY { get; set; }  // The Y coordinate of the laser center in the image, which may be a floating-point value for sub-pixel accuracy.

        public int TopY { get; set; }  // The Y coordinate of the top of the laser stripe in the image.

        public int BottomY { get; set; }  // The Y coordinate of the bottom of the laser stripe in the image.

        public int StripeHeightPixels { get; set; }  // The height of the laser stripe in pixels, calculated as BottomY - TopY.

        public double Confidence { get; set; }  // A confidence value for the measurement, which could be based on factors
                                                // such as the intensity of the laser reflection or the quality of the edge detection.
    }
}
