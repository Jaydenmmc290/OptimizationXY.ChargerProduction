namespace OptimizationXY.Charger.Models.Optimization.Scoring
{
    public class OptimizationPenaltyBreakdown
    {
        public double WaneScore { get; set; }
        public double DefectScore { get; set; }
        public double RotationScore { get; set; }
        public double MovementScore { get; set; }

        public double WaneContribution { get; set; }
        public double DefectContribution { get; set; }
        public double RotationContribution { get; set; }
        public double MovementContribution { get; set; }

        public double WeightedTotal { get; set; }
    }
}
