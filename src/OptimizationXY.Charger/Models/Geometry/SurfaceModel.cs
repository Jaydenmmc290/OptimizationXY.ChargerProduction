using System;
using System.Collections.Generic;
using System.Text;

namespace OptimizationXY.Charger.Models.Geometry
{
    public sealed class SurfaceModel
    {
        public double[,] RadiusByZAndAngle { get; set; } = new double[1, 1];
        public double MinZ { get; set; }
        public double MaxZ { get; set; }
        public double LogLengthMM => Math.Max(MaxZ - MinZ, 0.0001);
        public double OuterRadiusMM { get; set; }
        public double AverageRadiusMM { get; set; }
    }
}
