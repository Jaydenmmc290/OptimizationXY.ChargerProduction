namespace OptimizationXY.Charger.Models.Diagnostics
{
    public sealed class PipelineTimingEntry
    {
        public string StepName { get; set; } = string.Empty;
        public double ElapsedMilliseconds { get; set; }

        public double ElapsedSeconds => ElapsedMilliseconds / 1000.0;
    }
}
