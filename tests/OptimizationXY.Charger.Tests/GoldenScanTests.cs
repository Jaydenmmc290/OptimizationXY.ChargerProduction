using OptimizationXY.Charger.Models.Settings;
using OptimizationXY.Charger.Services.Replay;
using Xunit;
using Xunit.Abstractions;

namespace OptimizationXY.Charger.Tests;

/// <summary>
/// Golden scans: saved production scans replayed with their own settings and
/// calibration must give exactly the recorded answer (tests/golden-scans/README.md).
/// This is the guard on "the XY method and the virtual peel stay the same".
/// </summary>
[Collection("PipelineState")]
public class GoldenScanTests(ITestOutputHelper output)
{
    private static string? GoldenRoot()
    {
        string? fromEnvironment = Environment.GetEnvironmentVariable(GoldenScans.EnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
            return fromEnvironment;

        for (DirectoryInfo? d = new(AppContext.BaseDirectory); d is not null; d = d.Parent)
        {
            if (File.Exists(Path.Combine(d.FullName, "OptimizationXY.Charger.slnx")))
                return Path.Combine(d.FullName, "tests", "golden-scans");
        }

        return null;
    }

    [Fact]
    public void EveryGoldenScanGivesItsRecordedAnswer()
    {
        string? root = GoldenRoot();
        List<string> scans = root is null ? [] : GoldenScans.FindGoldenScans(root);

        if (scans.Count == 0)
        {
            output.WriteLine($"No golden scans under '{root}'. See tests/golden-scans/README.md to add some.");
            return;
        }

        var failures = new List<string>();

        foreach (string scan in scans)
        {
            AppSettings settings = GoldenScans.LoadSettings(scan);
            GoldenExpectation expected = GoldenScans.LoadExpected(scan);
            GoldenExpectation actual = GoldenScans.Replay(scan, settings);

            List<string> differences = GoldenScans.Compare(expected, actual);
            output.WriteLine($"{scan}: {(differences.Count == 0 ? "same" : string.Join("; ", differences))}");

            if (differences.Count > 0)
                failures.Add($"{scan}: {string.Join("; ", differences)}");
        }

        Assert.True(failures.Count == 0, "Golden scan results changed:\n" + string.Join("\n", failures));
    }

    [Fact]
    public void CompareReportsOnlyRealChanges()
    {
        var a = new GoldenExpectation("Largest Inscribed Cylinder", -1.656, 0.223, -14.931, -30.216, 454.892, 39, 57, 49530);

        Assert.Empty(GoldenScans.Compare(a, a with { TailXMM = -1.650 }));
        Assert.Single(GoldenScans.Compare(a, a with { HeadYMM = -30.0 }));
        Assert.Single(GoldenScans.Compare(a, a with { UsableSheets = 38 }));
        Assert.Single(GoldenScans.Compare(a, a with { Strategy = "Largest Inscribed Oval" }));
    }
}
