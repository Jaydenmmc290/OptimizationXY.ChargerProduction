namespace OptimizationXY.Charger.Models.Optimization.Scoring
{
    public class OptimizationWeights
    {
        public double WaneWeight { get; set; } = 0.35;
        public double DefectWeight { get; set; } = 0.30;

        public double MovementWeight { get; set; } = 0.15;
    }
}
