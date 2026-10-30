
// ProductionReadinessReport.cs - the production readiness checklist for one scan.
// ProductionReadinessEvaluator fills it in before every PLC write.

namespace OptimizationXY.Charger.Models.Production;


/// <summary>
/// Represents the status of a production readiness check.
/// </summary>
public enum ProductionCheckStatus
{
    /// <summary>Check passed.</summary>
    Pass = 0,
    /// <summary>Check found something worth noting; never stops the PLC write.</summary>
    Warning = 1,
    /// <summary>Check failed and is enforced: the PLC write is stopped.</summary>
    Block = 2
}

/// <summary>
/// Represents a single production readiness check with its name, status, and message.
/// </summary>
public sealed class ProductionReadinessCheck
{
    /// <summary>Short check name (shown in the report).</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>Pass, Warning or Block.</summary>
    public ProductionCheckStatus Status { get; set; }
    /// <summary>Plain-language result of the check.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// True when this check failed but is advisory only: it is reported and
    /// logged, and it does not stop the PLC write.
    /// </summary>
    public bool NotEnforced { get; set; }
}

/// <summary>
/// The list of readiness checks for one scan and whether the PLC write may go ahead.
/// <para>
/// <see cref="CanSendToPlc"/> is true when the PLC is connected and no check
/// has Status = Block. A failed check marked NotEnforced is reported in the
/// log but does not stop the write (most production checks are advisory, so
/// a real scan is not stopped by a non-safety condition).
/// </para>
/// </summary>
public sealed class ProductionReadinessReport
{
    public DateTime EvaluatedUtc { get; set; } = DateTime.UtcNow;
    public bool PlcConnected { get; set; }
    public List<ProductionReadinessCheck> Checks { get; set; } = [];

    /// <summary>True when the PLC is connected and no check is blocking.</summary>
    public bool CanSendToPlc =>
        PlcConnected &&
        Checks.All(check => check.Status != ProductionCheckStatus.Block);

    /// <summary>Number of checks that block the write.</summary>
    public int BlockingCount =>
        Checks.Count(check => check.Status == ProductionCheckStatus.Block);

    /// <summary>Number of warning checks.</summary>
    public int WarningCount =>
        Checks.Count(check => check.Status == ProductionCheckStatus.Warning);

    /// <summary>Number of checks that failed but are advisory only.</summary>
    public int NotEnforcedCount =>
        Checks.Count(check => check.NotEnforced);

    /// <summary>
    /// Builds a summary string of the production readiness report.
    /// </summary>
    /// <returns>A string representing the summary of the report.</returns>
    public string BuildSummary()
    {
        var lines = new List<string>
        {
            "=== PRODUCTION READINESS ===",
            $"PLC Connected: {(PlcConnected ? "Yes" : "No")}",
            $"Machine Command Allowed: {(CanSendToPlc ? "Yes" : "No")}",
            $"Blocking Checks: {BlockingCount}",
            $"Warnings: {WarningCount}",
            $"Failed But Not Enforced: {NotEnforcedCount}"
        };

        foreach (ProductionReadinessCheck check in Checks)
        {
            lines.Add($"[{check.Status.ToString().ToUpperInvariant()}] {check.Name}: {check.Message}");
        }

        return string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }
}
