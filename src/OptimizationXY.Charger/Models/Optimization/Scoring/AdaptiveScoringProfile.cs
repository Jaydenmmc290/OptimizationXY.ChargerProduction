using OptimizationXY.Charger.Models.Settings;


/// <summary >
/// Represents the adaptive scoring profile for a log, including scoring settings,
/// defect settings, optimization settings, and wane settings.
/// </summary>
namespace OptimizationXY.Charger.Models.Optimization.Scoring;

public sealed class AdaptiveScoringProfile
{

    /// <summary>
    /// Gets or sets a value indicating whether the adaptive scoring profile is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the log class for the adaptive scoring profile, which can be used to categorize logs based on their characteristics.
    /// </summary>
    public string LogClass { get; set; } = "Balanced";

    /// <summary>
    /// Gets or sets the reasons for the adaptive scoring profile, which can be used for diagnostics to explain why the scoring was changed for this log.
    /// </summary>
    public List<string> Reasons { get; set; } = [];

    /// <summary>
    /// Gets or sets the scoring settings for the adaptive scoring profile.
    /// </summary>
    public ScoringSettings Scoring { get; set; } = new();

    /// <summary>
    /// Gets or sets the defect settings for the adaptive scoring profile.
    /// </summary>  
    public DefectSettings Defects { get; set; } = new();

    /// <summary>
    /// Gets or sets the optimization settings for the adaptive scoring profile.
    /// </summary>
    public OptimizationSettings Optimization { get; set; } = new();

    /// <summary>
    /// Gets or sets the wane settings for the adaptive scoring profile.
    /// </summary>
    public WaneSettings Wane { get; set; } = new();
}
