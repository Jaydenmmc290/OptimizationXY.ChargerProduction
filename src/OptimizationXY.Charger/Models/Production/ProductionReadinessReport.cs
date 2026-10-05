
/// <summary>
/// This file contains classes and enumerations related to production readiness checks and reports.
/// </summary>

namespace OptimizationXY.Charger.Models.Production;


/// <summary>
/// Represents the status of a production readiness check.
/// </summary>
public enum ProductionCheckStatus
{
    Pass = 0,
    Warning = 1,
    Block = 2
}

/// <summary>
/// Represents a single production readiness check with its name, status, and message.
/// </summary>
public sealed class ProductionReadinessCheck
{
    public string Name { get; set; } = string.Empty;
    public ProductionCheckStatus Status { get; set; }
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// True when this check failed but is advisory only: it is reported and
    /// logged, and it does not stop the PLC write.
    /// </summary>
    public bool NotEnforced { get; set; }
}

/// <summary>
/// Represents a report of production readiness, including the evaluation time, PLC connection status, and a list of checks.
/// </summary>
public sealed class ProductionReadinessReport
{
    public DateTime EvaluatedUtc { get; set; } = DateTime.UtcNow;
    public bool PlcConnected { get; set; }
    public List<ProductionReadinessCheck> Checks { get; set; } = [];

    public bool CanSendToPlc =>
        PlcConnected &&
        Checks.All(check => check.Status != ProductionCheckStatus.Block);

    public int BlockingCount =>
        Checks.Count(check => check.Status == ProductionCheckStatus.Block);

    public int WarningCount =>
        Checks.Count(check => check.Status == ProductionCheckStatus.Warning);

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
