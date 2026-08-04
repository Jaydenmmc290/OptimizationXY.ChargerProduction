using OptimizationXY.Charger.Services.Optimization.Scoring;

namespace OptimizationXY.Charger.Services.Core
{
    public sealed class UniformScoreComponent
    {
        public string Name { get; set; } = string.Empty;
        public double MeasuredValue { get; set; }
        public string Unit { get; set; } = string.Empty;
        public double GoodLimit { get; set; }
        public double BadLimit { get; set; }
        public double WeightPercent { get; set; }
        public string Meaning { get; set; } = string.Empty;

        public double RiskPercent
        {
            get
            {
                if (BadLimit <= GoodLimit)
                    return 0.0;

                double risk =
                    ((MeasuredValue - GoodLimit) / (BadLimit - GoodLimit)) * 100.0;

                return Math.Clamp(risk, 0.0, 100.0);
            }
        }

        public double WeightedPoints =>
            RiskPercent * (WeightPercent / 100.0);
    }
}
