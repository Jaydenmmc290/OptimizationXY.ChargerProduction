using OptimizationXY.Charger.Models.Settings;
using OptimizationXY.Charger.Services.Replay;
using Xunit;

namespace OptimizationXY.Charger.Tests;

/// <summary>
/// Tests for the --replay-batch tool: --set overrides change numbers, true/false
/// and enum settings and refuse unknown names or bad values; summary.csv reads
/// back exactly what was written; and summary.txt counts changed scans against
/// a baseline and reports the spread between repeat scans of one block.
/// </summary>
public class BatchReplayTests
{
    [Fact]
    public void SettingsOverride_ChangesNumbersBoolsAndEnums()
    {
        var settings = new AppSettings();

        SettingsOverride.Apply(settings, "InscribedGeometry.CenterlinePullWeight=1.25");
        SettingsOverride.Apply(settings, "rotatingscan.keepallstripesegments=true");
        SettingsOverride.Apply(settings, "OptimizationMode=RecoveryOptimized");

        Assert.Equal(1.25, settings.InscribedGeometry.CenterlinePullWeight);
        Assert.True(settings.RotatingScan.KeepAllStripeSegments);
        Assert.Equal(OptimizationXY.Charger.Models.Optimization.SpindleOptimizationMode.RecoveryOptimized, settings.OptimizationMode);
    }

    [Fact]
    public void SettingsOverride_RejectsUnknownSettingsAndBadValues()
    {
        var settings = new AppSettings();

        Assert.Throws<ArgumentException>(() => SettingsOverride.Apply(settings, "Nope.Missing=1"));
        Assert.Throws<ArgumentException>(() => SettingsOverride.Apply(settings, "InscribedGeometry.CenterlinePullWeight=abc"));
        Assert.Throws<ArgumentException>(() => SettingsOverride.Apply(settings, "no-equals-sign"));
    }

    [Fact]
    public void Csv_RoundTrips_AndSummaryFlagsChangesAndRepeatSpread()
    {
        string folder = Path.Combine(Path.GetTempPath(), "batch-replay-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);

        try
        {
            BatchReplayRunner.Row Row(string scan, double tailX, int usable) => new(
                scan, "Block_0003", "OK", 88, 83, true, 83, "Largest Inscribed Cylinder",
                tailX, 2.68, -19.41, -16.43, 439.47, usable, 45, usable * 1270.0, "Good", 1.5, string.Empty);

            var before = new List<BatchReplayRunner.Row>
            {
                Row(Path.Combine("Block_0003", "Scan_0001"), -0.10, 36),
                Row(Path.Combine("Block_0003", "Scan_0002"), 0.40, 36)
            };

            string csv = Path.Combine(folder, "summary.csv");
            BatchReplayRunner.WriteCsv(csv, before);
            List<BatchReplayRunner.Row> read = BatchReplayRunner.ReadCsv(csv);

            Assert.Equal(2, read.Count);
            Assert.Equal(-0.10, read[0].TailXMM, 3);
            Assert.Equal(36, read[0].UsableSheets);

            var after = new List<BatchReplayRunner.Row>
            {
                Row(Path.Combine("Block_0003", "Scan_0001"), 1.10, 37),   // moved 1.2 mm, +1 sheet
                Row(Path.Combine("Block_0003", "Scan_0002"), 0.45, 36)    // within tolerance
            };

            string summary = BatchReplayRunner.BuildSummary("root", [], after, read, csv, 0.5);

            Assert.Contains("Scans compared: 2   Changed: 1   Usable sheets gained: 1   lost: 0", summary);
            Assert.Contains("tail X 0.65", summary);   // repeat-scan spread between Scan_0001 and Scan_0002
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
