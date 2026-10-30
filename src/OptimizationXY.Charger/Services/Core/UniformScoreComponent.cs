

namespace OptimizationXY.Charger.Services.Core;

/// <summary>
/// One line of the legacy penalty/risk score (e.g. "centreline sweep"): the
/// measured value, its good/bad limits and weight. Risk goes linearly from 0%
/// at the good limit to 100% at the bad limit; weighted points = risk x weight.
/// Example: good 0.1 in, bad 0.5 in, measured 0.3 in -> 50% risk; with a 10%
/// weight that adds 5 points.
/// </summary>
public sealed class UniformScoreComponent
{
    /// <summary>
    /// Gets or sets the name of the uniform score component.
    /// </summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>The measured value, in <see cref="Unit"/>.</summary>
    public double MeasuredValue { get; set; }
    public string Unit { get; set; } = string.Empty;
    /// <summary>At or better than this the risk is 0%.</summary>
    public double GoodLimit { get; set; }
    /// <summary>At or worse than this the risk is 100%.</summary>
    public double BadLimit { get; set; }
    /// <summary>How much this component counts in the total (percent).</summary>
    public double WeightPercent { get; set; }
    /// <summary>Plain-language description for the diagnostics report.</summary>
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
