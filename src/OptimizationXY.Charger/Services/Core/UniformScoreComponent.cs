

namespace OptimizationXY.Charger.Services.Core;

/// <summary>
/// Represents a component of a uniform score, including its measured value, limits, weight, and risk calculation.
/// </summary>
public sealed class UniformScoreComponent
{
    /// <summary>
    /// Gets or sets the name of the uniform score component.
    /// </summary>
    public string Name { get; set; } = string.Empty;
    public double MeasuredValue { get; set; }
    public string Unit { get; set; } = string.Empty;
    public double GoodLimit { get; set; }
    public double BadLimit { get; set; }
    public double WeightPercent { get; set; }
    public string Meaning { get; set; } = string.Empty;

    /// <summary>
    /// Gets the risk percentage based on the measured value and the defined good and bad limits.
    /// </summary>
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

    /// <summary>
    /// Gets the weighted points for the uniform score component, calculated as the product of the risk percentage and the weight percentage.
    /// </summary>
    public double WeightedPoints =>
        RiskPercent * (WeightPercent / 100.0);
}
