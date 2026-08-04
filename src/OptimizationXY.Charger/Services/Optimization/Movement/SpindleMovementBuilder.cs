using OptimizationXY.Charger.Models.Optimization.Spindle;

namespace OptimizationXY.Charger.Services.Optimization.Movement
{
    public static class SpindleMovementBuilder
    {
        public static OptimizedSpindleMovement BuildFromSweep(
            double offsetXMM,
            double offsetYMM,
            double sweepXMM,
            double sweepYMM)
        {
            double halfSweepX = sweepXMM / 2.0;
            double halfSweepY = sweepYMM / 2.0;

            return new OptimizedSpindleMovement
            {
                CenterMoveXMM = offsetXMM,
                CenterMoveYMM = offsetYMM,

                SweepCorrectionXMM = sweepXMM,
                SweepCorrectionYMM = sweepYMM,

                TailMoveXMM = offsetXMM + halfSweepX,
                TailMoveYMM = offsetYMM + halfSweepY,

                HeadMoveXMM = offsetXMM - halfSweepX,
                HeadMoveYMM = offsetYMM - halfSweepY
            };
        }
    }
}
