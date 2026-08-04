namespace OptimizationXY.Charger.Models.Optimization.Spindle
{
    public class OptimizedSpindleMovement
    {
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
