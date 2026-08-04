using OptimizationXY.Charger.Models.Settings;

namespace OptimizationXY.Charger.Models.Optimization.Scoring
{
    public sealed class AdaptiveScoringProfile
    {
        public bool Enabled { get; set; } = true;

        // Examples: Clean / Swept / Low-Wood Risk / Oval / Severe / Balanced
        public string LogClass { get; set; } = "Balanced";

        // This is important for diagnostics. It tells the operator/engineer
        // why the scoring was changed for this log.
        public List<string> Reasons { get; set; } = new();

        // These are per-log copies. Do NOT assign them back into AppSettings.
        public ScoringSettings Scoring { get; set; } = new();
        public DefectSettings Defects { get; set; } = new();
        public OptimizationSettings Optimization { get; set; } = new();
        public WaneSettings Wane { get; set; } = new();
    }
}
