using OptimizationXY.Charger.Models.Analysis;
using OptimizationXY.Charger.Models.Core;
using OptimizationXY.Charger.Models.Geometry;
using OptimizationXY.Charger.Models.Optimization.Geometry;
using OptimizationXY.Charger.Models.Settings;
using OptimizationXY.Charger.Services.Acquisition;
using OptimizationXY.Charger.Services.Optimization.Recovery;
using OptimizationXY.Charger.Services.Replay;
using Xunit;

namespace OptimizationXY.Charger.Tests;

/// <summary>
/// Tests for the repeatability options: finding the exact (part-frame) point
/// where the block has turned one full revolution, the cylinder-vs-oval
/// tie-break in the peel comparison, and the batch-replay summary that compares
/// repeat scans of the same block (including older summary.csv files).
/// </summary>
public class RepeatabilityTests
{
    // ------------------------------------------------------------
    // Sub-frame revolution closure
    // ------------------------------------------------------------

    private const int Columns = 200;

    /// <summary>Profile of a block whose radius varies smoothly around it.</summary>
    private static double RadiusAt(int x, double angleDeg)
    {
        double a = angleDeg * Math.PI / 180.0;
        return 200.0 + 10.0 * Math.Sin(a + x * 0.01) + 4.0 * Math.Cos(2.0 * a - x * 0.02);
    }

    private static List<ScanFrame> RotatingFrames(int count, double framesPerRevolution)
    {
        var frames = new List<ScanFrame>();

        for (int index = 0; index < count; index++)
        {
            double angle = index * 360.0 / framesPerRevolution;
            var profile = new List<LogProfileColumn>();

            for (int x = 0; x < Columns; x++)
                profile.Add(new LogProfileColumn { X = x, RadiusMM = RadiusAt(x, angle) });

            frames.Add(new ScanFrame { FrameIndex = index, Profile = profile, IsValid = true });
        }

        return frames;
    }

    [Fact]
    public void RefineClosureOffsetFindsTheFractionOfAFrame()
    {
        const double period = 84.6;   // true revolution: 84.6 frames
        var profiles = new double[]?[95];

        for (int index = 0; index < profiles.Length; index++)
        {
            double angle = index * 360.0 / period;
            profiles[index] = [.. Enumerable.Range(0, Columns).Select(x => RadiusAt(x, angle))];
        }

        // Frame 85 is the nearest whole frame; the true closure is 0.4 frame before it.
        double offset = RevolutionAngleAssigner.RefineClosureOffset(profiles, 0, 85, out double errorMM);

        Assert.InRange(offset, -0.45, -0.35);
        Assert.True(errorMM < 0.05, $"refined error {errorMM}");
    }

    [Fact]
    public void AssignOffByDefaultKeepsWholeFramesOnGivesFractionalRevolution()
    {
        Assert.False(new RotatingScanSettings().SubFrameRevolutionClosure);   // off by default

        // A lower rest tolerance than the default so the small synthetic
        // frame-to-frame change (about 0.8 mm) clearly counts as turning.
        var settings = new RotatingScanSettings { StationaryProfileChangeMM = 0.3 };

        List<ScanFrame> wholeFrames = RotatingFrames(95, 84.6);
        RevolutionAngleResult off = RevolutionAngleAssigner.Assign(wholeFrames, settings);

        Assert.True(off.RevolutionVerified);
        Assert.Equal(85, off.FramesPerRevolution);
        Assert.Equal(85.0, off.RevolutionFrames, 6);
        Assert.Equal(360.0 / 85.0, off.AngleStepDeg, 6);

        settings.SubFrameRevolutionClosure = true;
        List<ScanFrame> refinedFrames = RotatingFrames(95, 84.6);
        RevolutionAngleResult on = RevolutionAngleAssigner.Assign(refinedFrames, settings);

        Assert.True(on.RevolutionVerified);
        Assert.InRange(on.RevolutionFrames, 84.55, 84.65);
        Assert.Equal(85, on.FramesPerRevolution);   // frames 0..84 are below 360 degrees
        Assert.False(refinedFrames[84].ExcludedFromRevolution);
        Assert.True(refinedFrames[85].ExcludedFromRevolution);

        // Frame 42 is half way round: 180 degrees, not 177.9 (= 42 x 360 / 85).
        Assert.InRange(refinedFrames[42].AngleDeg, 178.5, 179.0);   // 42 x 360 / 84.6 = 178.72
    }

    [Fact]
    public void AssignUnverifiedReportsClosestReturnAndEndRest()
    {
        // The block turns only about 300 degrees, then rests: no closure.
        var frames = new List<ScanFrame>();
        for (int index = 0; index < 82; index++)
        {
            double angle = Math.Min(index, 70) * 360.0 / 84.6;
            var profile = new List<LogProfileColumn>();

            for (int x = 0; x < Columns; x++)
                profile.Add(new LogProfileColumn { X = x, RadiusMM = RadiusAt(x, angle) });

            frames.Add(new ScanFrame { FrameIndex = index, Profile = profile, IsValid = true });
        }

        var settings = new RotatingScanSettings { StationaryProfileChangeMM = 0.3 };
        RevolutionAngleResult result = RevolutionAngleAssigner.Assign(frames, settings);

        Assert.False(result.RevolutionVerified);
        Assert.Equal("RotatingFramesEvenly", result.Method);   // a short turn is never taken as an approximate closure
        Assert.True(result.BestClosureFrame >= 20);
        Assert.True(result.BestClosureErrorMM > settings.RevolutionClosureToleranceMM);
        Assert.True(result.EndRestDifferenceMM > 1.0);
        Assert.Contains("Closest return to the start profile", result.Message);
    }

    [Fact]
    public void AssignApproximateClosureUsesTheClosestReturnInsteadOfTheRestStart()
    {
        // One full turn in 86 frames, then rest. From frame 80 on, the profile
        // carries +/-2 mm of roughness, so it never returns within 1.5 mm.
        static List<ScanFrame> Frames()
        {
            var frames = new List<ScanFrame>();
            for (int index = 0; index < 96; index++)
            {
                double angle = Math.Min(index, 86) * 360.0 / 86.0;
                var profile = new List<LogProfileColumn>();

                for (int x = 0; x < Columns; x++)
                {
                    double roughness = index >= 80 ? (x % 2 == 0 ? 2.0 : -2.0) : 0.0;
                    profile.Add(new LogProfileColumn { X = x, RadiusMM = RadiusAt(x, angle) + roughness });
                }

                frames.Add(new ScanFrame { FrameIndex = index, Profile = profile, IsValid = true });
            }

            return frames;
        }

        Assert.Equal(2.5, new RotatingScanSettings().ApproximateClosureToleranceMM);   // on by default

        var off = new RotatingScanSettings { StationaryProfileChangeMM = 0.3, ApproximateClosureToleranceMM = 0.0 };

        RevolutionAngleResult before = RevolutionAngleAssigner.Assign(Frames(), off);
        Assert.Equal("RotatingFramesEvenly", before.Method);
        Assert.NotEqual(86, before.FramesPerRevolution);
        Assert.Equal(86, before.BestClosureFrame);
        Assert.InRange(before.BestClosureErrorMM, 1.99, 2.01);

        var on = new RotatingScanSettings { StationaryProfileChangeMM = 0.3, ApproximateClosureToleranceMM = 2.5 };
        RevolutionAngleResult after = RevolutionAngleAssigner.Assign(Frames(), on);

        Assert.Equal("ApproximateProfileClosure", after.Method);
        Assert.False(after.RevolutionVerified);   // still reported as not verified
        Assert.Equal(86, after.FramesPerRevolution);
        Assert.Equal(360.0 / 86.0, after.AngleStepDeg, 6);

        // Too far off for the approximate tolerance: falls back as before.
        var tight = new RotatingScanSettings { StationaryProfileChangeMM = 0.3, ApproximateClosureToleranceMM = 1.9 };
        Assert.Equal("RotatingFramesEvenly", RevolutionAngleAssigner.Assign(Frames(), tight).Method);
    }

    // ------------------------------------------------------------
    // Cylinder / oval tie-break
    // ------------------------------------------------------------

    private static PeelSimulationResult Peel(int usable, double severe, double total) => new()
    {
        UsableFullSheets = usable,
        TotalFullSheets = 40,
        RejectedFullSheets = 40 - usable,
        UsableVeneerLengthMM = usable * 1270.0,
        SevereWaneAreaPercent = severe,
        TotalWaneAreaPercent = total
    };

    private static InscribedGeometryResult Geometry(InscribedGeometryKind kind) => new() { Kind = kind };

    [Fact]
    public void TieBreakIsOnByDefaultAtOnePercent()
    {
        Assert.Equal(1.0, new InscribedGeometrySettings().OvalMinimumWaneAdvantagePercent);
    }

    [Fact]
    public void TieBreakOffOvalWinsOnAnyWaneDifference()
    {
        var result = GeometryPeelSelector.Select(
            Geometry(InscribedGeometryKind.Cylinder), Peel(33, 1.0, 6.0),
            Geometry(InscribedGeometryKind.Oval), Peel(33, 0.8, 5.7));

        Assert.Equal(InscribedGeometryKind.Oval, result.SelectedKind);
    }

    [Fact]
    public void TieBreakOnKeepsCylinderForSmallWaneDifferenceButNotForASheet()
    {
        var small = GeometryPeelSelector.Select(
            Geometry(InscribedGeometryKind.Cylinder), Peel(33, 1.0, 6.0),
            Geometry(InscribedGeometryKind.Oval), Peel(33, 0.8, 5.7),
            ovalMinimumWaneAdvantagePercent: 1.0);

        Assert.Equal(InscribedGeometryKind.Cylinder, small.SelectedKind);
        Assert.Contains("OvalMinimumWaneAdvantagePercent", small.SelectionReason);

        var bigWane = GeometryPeelSelector.Select(
            Geometry(InscribedGeometryKind.Cylinder), Peel(33, 1.0, 6.0),
            Geometry(InscribedGeometryKind.Oval), Peel(33, 0.9, 4.5),
            ovalMinimumWaneAdvantagePercent: 1.0);

        Assert.Equal(InscribedGeometryKind.Oval, bigWane.SelectedKind);

        var extraSheet = GeometryPeelSelector.Select(
            Geometry(InscribedGeometryKind.Cylinder), Peel(33, 1.0, 6.0),
            Geometry(InscribedGeometryKind.Oval), Peel(34, 1.0, 5.9),
            ovalMinimumWaneAdvantagePercent: 1.0);

        Assert.Equal(InscribedGeometryKind.Oval, extraSheet.SelectedKind);

        // The rule never takes a sheet away from the cylinder either.
        var cylinderBetter = GeometryPeelSelector.Select(
            Geometry(InscribedGeometryKind.Cylinder), Peel(34, 1.0, 6.0),
            Geometry(InscribedGeometryKind.Oval), Peel(33, 0.1, 1.0),
            ovalMinimumWaneAdvantagePercent: 1.0);

        Assert.Equal(InscribedGeometryKind.Cylinder, cylinderBetter.SelectedKind);
    }

    // ------------------------------------------------------------
    // Batch summary: repeat scans compared inside the block
    // ------------------------------------------------------------

    private static BatchReplayRunner.Row Row(string scan, double axis, double diameter, int usable, string strategy) => new(
        scan, Path.GetFileName(scan), "OK", 90, 84, true, 85, strategy,
        1.0, 2.0, 3.0, 4.0, diameter, usable, 36, usable * 1270.0, "Good", 1.2, string.Empty,
        84.6, axis);

    [Fact]
    public void SummaryUsesSameBlockListsAndReportsInBlockMetrics()
    {
        var rows = new List<BatchReplayRunner.Row>
        {
            Row("B01", 2.0, 420.5, 33, "Largest Inscribed Cylinder"),
            Row("B02", 3.0, 426.1, 35, "Largest Inscribed Cylinder"),
            Row("B04", 0.0, 423.0, 34, "Largest Inscribed Oval"),
            Row("B12", 0.0, 368.0, 25, "Largest Inscribed Oval")
        };

        List<IReadOnlyList<string>> groups =
            BatchReplayRunner.ParseSameBlockGroups(["--replay-batch", "x", "--same-block", "B01, B02,B04", "--same-block", "B12,B13"]);

        Assert.Equal(2, groups.Count);

        string summary = BatchReplayRunner.BuildSummary("root", [], rows, null, null, 0.5, groups);

        Assert.Contains("B01,B02,B04 (3 scans): axis 0.0 to 3.0 mm, inscribed diameter spread 5.60 mm, usable sheets 33 to 35", summary);
        Assert.Contains("cylinder/oval 2/1", summary);
        Assert.DoesNotContain("B12,B13", summary);   // only one of its scans is present
    }

    [Fact]
    public void SameBlockMatchesDateAndBlockFolders()
    {
        var rows = new List<BatchReplayRunner.Row>
        {
            Row(Path.Combine("09", "11", "Block_0001", "Scan_0001"), 2.0, 423.2, 33, "Largest Inscribed Cylinder"),
            Row(Path.Combine("09", "11", "Block_0002", "Scan_0001"), 4.7, 425.2, 34, "Largest Inscribed Cylinder"),
            Row(Path.Combine("10", "05", "Block_0001", "Scan_0001"), 2.9, 377.6, 25, "Largest Inscribed Cylinder")
        };

        var groups = BatchReplayRunner.GroupRepeatScans(rows, [["09\\11\\Block_0001", "09/11/Block_0002"]]);

        Assert.Single(groups);
        Assert.Equal(2, groups[0].Scans.Count);   // 10\05\Block_0001 is a different block
    }

    [Fact]
    public void ReadCsvAcceptsSummariesWrittenBeforeTheNewColumns()
    {
        string folder = Path.Combine(Path.GetTempPath(), "batch-old-csv-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);

        try
        {
            string csv = Path.Combine(folder, "summary.csv");
            File.WriteAllLines(csv,
            [
                "ScanFolder,Block,Status,Frames,FramesUsed,RevolutionVerified,FramesPerRevolution,Strategy,TailXMM,TailYMM,HeadXMM,HeadYMM,InscribedDiameterMM,UsableSheets,TotalSheets,UsableVeneerLengthMM,ScanQuality,ProcessingSeconds,Error",
                "\"B03\",\"B03\",\"OK\",88,83,true,83,\"Largest Inscribed Cylinder\",-0.1,2.68,-19.41,-16.43,439.47,36,45,45720,\"Good\",1.5,\"\""
            ]);

            List<BatchReplayRunner.Row> rows = BatchReplayRunner.ReadCsv(csv);

            Assert.Single(rows);
            Assert.Equal(-19.41, rows[0].HeadXMM, 3);
            Assert.Equal(36, rows[0].UsableSheets);
            Assert.True(double.IsNaN(rows[0].AxisFromCenterlineMM));

            // And the new format round-trips.
            BatchReplayRunner.WriteCsv(csv, [Row("B01", 2.0, 420.5, 33, "Largest Inscribed Cylinder")]);
            BatchReplayRunner.Row back = BatchReplayRunner.ReadCsv(csv)[0];

            Assert.Equal(2.0, back.AxisFromCenterlineMM, 3);
            Assert.Equal(84.6, back.RevolutionFrames, 3);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
