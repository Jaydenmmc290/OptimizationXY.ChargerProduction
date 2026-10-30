using OptimizationXY.Charger.Models.Settings;


// AdaptiveScoringProfile.cs - per-log copy of the scoring settings.
namespace OptimizationXY.Charger.Models.Optimization.Scoring;

/// <summary>
/// The scoring, defect, optimization and wane settings to use for THIS log in
/// the legacy PenaltyScored search. AdaptiveScoringProfileBuilder starts from
/// the saved settings and adjusts weights for the kind of log measured (for
/// example a crooked log or an oval log). Not used to choose the position in
/// the inscribed-geometry modes.
/// </summary>
public sealed class AdaptiveScoringProfile
{

    /// <summary>
    /// Gets or sets a value indicating whether the adaptive scoring profile is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the class name the builder gave this log (e.g. "Balanced"); shown in diagnostics.
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
