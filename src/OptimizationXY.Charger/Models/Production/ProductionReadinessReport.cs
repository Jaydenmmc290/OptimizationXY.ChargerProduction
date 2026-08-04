namespace OptimizationXY.Charger.Models.Production;

public enum ProductionCheckStatus
{
    Pass = 0,
    Warning = 1,
    Block = 2
}

public sealed class ProductionReadinessCheck
{
    public string Name { get; set; } = string.Empty;
    public ProductionCheckStatus Status { get; set; }
    public string Message { get; set; } = string.Empty;
}

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

    public string BuildSummary()
    {
        var lines = new List<string>
        {
            "=== PRODUCTION READINESS ===",
            $"PLC Connected: {(PlcConnected ? "Yes" : "No")}",
            $"Machine Command Allowed: {(CanSendToPlc ? "Yes" : "No")}",
            $"Blocking Checks: {BlockingCount}",
            $"Warnings: {WarningCount}"
        };

        foreach (ProductionReadinessCheck check in Checks)
        {
            lines.Add($"[{check.Status.ToString().ToUpperInvariant()}] {check.Name}: {check.Message}");
        }

        return string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }
}
