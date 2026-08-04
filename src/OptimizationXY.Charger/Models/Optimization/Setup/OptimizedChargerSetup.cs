using OptimizationXY.Charger.Models.Optimization.Scoring;
using OptimizationXY.Charger.Models.Optimization.Spindle;

namespace OptimizationXY.Charger.Models.Optimization.Setup
{
    public class OptimizedChargerSetup
    {
        public OptimizedSpindleAxis OptimizedSpindleAxis { get; set; } = new();

        public OptimizationPenaltyBreakdown ScoreBreakdown { get; set; } = new();

        public double FinalScore => ScoreBreakdown.WeightedTotal;
    }
}
