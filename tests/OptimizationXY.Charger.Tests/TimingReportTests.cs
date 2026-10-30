using OptimizationXY.Charger.Models.Diagnostics;
using OptimizationXY.Charger.Services.Core;
using Xunit;

namespace OptimizationXY.Charger.Tests;

/// <summary>
/// Tests for the timing report saved with each scan: the total is the real
/// elapsed time (frames run in parallel, so per-frame times must not be added
/// up), and the scan timeline lists each step with a running total.
/// </summary>
public class TimingReportTests
{
    [Fact]
    public void Report_UsesRealElapsedTime_NotTheSumOfParallelFrames()
    {
        var entries = new List<PipelineTimingEntry>
        {
            // Two frames processed in parallel inside a 0.6 s stage.
            new() { StepName = "Frame 0 - Build Radius Profile", ElapsedMilliseconds = 500, StartOffsetMilliseconds = 10, Depth = 1 },
            new() { StepName = "Frame 1 - Build Radius Profile", ElapsedMilliseconds = 500, StartOffsetMilliseconds = 10, Depth = 1 },
            new() { StepName = "Process Rotating Scan Frames", ElapsedMilliseconds = 600, StartOffsetMilliseconds = 5, Depth = 0 },
            new() { StepName = "Build Raw Rotating Scan Point Cloud", ElapsedMilliseconds = 80, StartOffsetMilliseconds = 610, Depth = 1 },
            new() { StepName = "Build Rotating Scan Point Cloud", ElapsedMilliseconds = 100, StartOffsetMilliseconds = 605, Depth = 0 }
        };

        string report = LogProcessingPipeline.BuildTimingReport(entries, 750);

        Assert.Contains("frames in hand to position chosen: 0.75 s", report);

        // Stages in run order, nested step indented under its parent.
        int process = report.IndexOf("  Process Rotating Scan Frames", StringComparison.Ordinal);
        int cloud = report.IndexOf("  Build Rotating Scan Point Cloud", StringComparison.Ordinal);
        int raw = report.IndexOf("    Build Raw Rotating Scan Point Cloud", StringComparison.Ordinal);
        Assert.True(process >= 0 && cloud > process && raw > cloud, report);

        // Only top-level stages count toward the total: 750 - 600 - 100 = 50 ms between stages.
        Assert.Contains("Between stages", report);
        Assert.Contains("0.05 s", report);

        // Per-frame work is reported as CPU time, aggregated.
        Assert.Contains("Build Radius Profile (2 frames): 1.00 s CPU, 500.0 ms per frame", report);
    }

    [Fact]
    public void ScanTimeline_ReportsEachStepAndRunningTotal()
    {
        ScanTimeline.Start("PLC ScanComplete received");
        ScanTimeline.Mark("Scan processed, position chosen");
        ScanTimeline.Mark("PLC write + readback verified");
        ScanTimeline.Stop();
        ScanTimeline.Mark("ignored after stop");

        string report = ScanTimeline.BuildReport();

        Assert.Contains("SCAN-TO-PLC TIMELINE", report);
        Assert.Contains("PLC ScanComplete received", report);
        Assert.Contains("PLC write + readback verified", report);
        Assert.DoesNotContain("ignored after stop", report);
    }
}
