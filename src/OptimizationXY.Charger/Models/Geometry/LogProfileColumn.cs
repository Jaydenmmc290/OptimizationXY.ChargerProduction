using System;
using System.Collections.Generic;
using System.Text;

namespace OptimizationXY.Charger.Models.Geometry  // Namespace for geometric models related to the optimization of the XY charger, including log profile columns.
{
    public class LogProfileColumn // Represents a column in the log profile, containing geometric information about a specific X coordinate.
    {
        public int X { get; set; }  // The X coordinate of the column.

        public double TopY { get; set; }  // The Y coordinate of the top of the column, representing the highest point in the log profile at this X coordinate.

        public double BottomY { get; set; }  // The Y coordinate of the bottom of the column, representing the lowest point in the log profile at this X coordinate.

        public double CenterY { get; set; }  // The Y coordinate of the center of the column, calculated as the
                                             // average of TopY and BottomY, representing the midpoint in the log profile at this X coordinate.

        public double DiameterMM { get; set; }  // The diameter of the column in millimeters, calculated from the pixel diameter using the appropriate conversion factor.

        public double RadiusMM { get; set; } // The radius of the column in millimeters, calculated as half of the diameter,
                                             // representing the distance from the center to the edge of the column in the log profile at this X coordinate.
    }
}
