
/// <summary>
/// Represents the optimized spindle movement in millimeters for the tail, head, and center of
/// the log, as well as any sweep correction needed to achieve the optimal cutting path.
namespace OptimizationXY.Charger.Models.Optimization.Spindle
{
    public class OptimizedSpindleMovement
    {

        /// <summary>
        /// Gets or sets the optimized spindle movement in millimeters for the tail, head, and center of the log,
        /// as well as any sweep correction needed to achieve the optimal cutting path.
        /// </summary>
        public double TailMoveXMM { get; set; }
        public double TailMoveYMM { get; set; }

        public double HeadMoveXMM { get; set; }
        public double HeadMoveYMM { get; set; }

        public double CenterMoveXMM { get; set; }
        public double CenterMoveYMM { get; set; }

        public double SweepCorrectionXMM { get; set; }
        public double SweepCorrectionYMM { get; set; }
    }
}
