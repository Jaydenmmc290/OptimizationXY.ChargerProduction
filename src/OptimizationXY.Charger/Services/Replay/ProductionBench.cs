using System.Diagnostics;
using System.Globalization;
using System.Text;
using NLog;
using OptimizationXY.Charger.Models.Calibration;
using OptimizationXY.Charger.Models.Core;
using OptimizationXY.Charger.Models.Operator;
using OptimizationXY.Charger.Models.Plc;
using OptimizationXY.Charger.Models.Production;
using OptimizationXY.Charger.Services.Acquisition;
using OptimizationXY.Charger.Services.Core;
using OptimizationXY.Charger.Services.Operator;
using OptimizationXY.Charger.Services.Plc;
using OptimizationXY.Charger.Services.Scan;

namespace OptimizationXY.Charger.Services.Replay;

// =============================================================================
// ProductionBench - the production scan cycle on the development computer.
// -----------------------------------------------------------------------------
//   OptimizationXY.Charger.exe --bench "C:\path\to\SavedScans"   (one scan or many)
//
// For each saved scan folder it runs what MainForm runs on the production
// computer from ScanStart to the PLC write, against an in-memory PLC
// (FakeXyPlcConnection) instead of the real one:
//   ScanStart edge -> ScanComplete handshake -> processing (the same pipeline)
//   -> scan-quality gate -> PLC payload + stroke guard + readiness checks
//   -> write + readback through XyRestrictedPlcService -> timing vs the PLC's
//   result window.
// The real XyRestrictedPlcService code runs; only the connection is fake.
//
// What it does NOT cover: the cameras and frame pairing (saved scans hold the
// already-joined frames), the real PLC's timing, SQL and the scan save. Nothing
// is sent to a real PLC, nothing is written to SQL, the production block
// counter is not touched (bench blocks are numbered from 900001).
// Same idea as the Rebuild program's XyBench.
// =============================================================================

/// <summary>What one bench scan did.</summary>
public sealed class BenchScanResult
{
    public string ScanFolder { get; init; } = string.Empty;
    public int BlockNumber { get; init; }
    public int Frames { get; set; }
    public bool Sent { get; set; }
    public string Outcome { get; set; } = string.Empty;
    public string ScanQuality { get; set; } = string.Empty;
    public string StrokeGuard { get; set; } = string.Empty;
    public string ResultWindow { get; set; } = string.Empty;
    public float[] ScannedBlockDataReceived { get; set; } = [];
    public string Timeline { get; set; } = string.Empty;
    public string Readiness { get; set; } = string.Empty;
    public double ProcessingSeconds { get; set; }
    public double WriteMilliseconds { get; set; }
}

/// <summary>The production cycle against a fake PLC. See the file header.</summary>
public static class ProductionBench
{
    public const string CommandLineSwitch = "--bench";

    /// <summary>Bench block numbers start here so they can never be mistaken for production blocks.</summary>
    public const int FirstBenchBlockNumber = 900001;

    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>True when the command line contains --bench.</summary>
    public static bool IsRequested(string[] args) =>
        args.Any(a => string.Equals(a, CommandLineSwitch, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Command-line entry: runs every saved scan folder under the given folder
    /// and writes BenchReport_yyyyMMdd_HHmmss.txt there.
    /// </summary>
    public static void Run(string[] args)
    {
        try
        {
            int index = Array.FindIndex(args, a => string.Equals(a, CommandLineSwitch, StringComparison.OrdinalIgnoreCase));
            string? root = index >= 0 && index + 1 < args.Length ? args[index + 1].Trim('"') : null;

            if (root is null && OperatingSystem.IsWindowsVersionAtLeast(6, 1))
            {
                using var dialog = new FolderBrowserDialog
                {
                    Description = "Bench: choose a saved scan folder (or a folder of saved scans)",
                    UseDescriptionForTitle = true
                };

                if (dialog.ShowDialog() == DialogResult.OK)
                    root = dialog.SelectedPath;
            }

            if (string.IsNullOrWhiteSpace(root))
                return;

            if (!Directory.Exists(root))
                throw new DirectoryNotFoundException($"Bench folder was not found: {root}");

            List<string> folders = BatchReplayRunner.FindScanFolders(root);

            if (folders.Count == 0)
                throw new InvalidOperationException($"No saved scan folder (one with a Frames folder) under {root}.");

            List<BenchScanResult> results = RunFoldersAsync(folders).GetAwaiter().GetResult();

            string reportPath = Path.Combine(root, $"BenchReport_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
            File.WriteAllText(reportPath, BuildReport(root, results), Encoding.UTF8);
            Logger.Info($"[BENCH] Report written to '{reportPath}'.");

            if (OperatingSystem.IsWindowsVersionAtLeast(6, 1))
            {
                int sent = results.Count(r => r.Sent);
                MessageBox.Show(
                    $"Bench complete: {results.Count} scan(s), {sent} sent to the fake PLC.\r\n\r\nReport:\r\n{reportPath}",
                    "Optimization XY Charger - Bench",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                try
                {
                    Process.Start(new ProcessStartInfo(reportPath) { UseShellExecute = true });
                }
                catch (Exception openException)
                {
                    Logger.Warn(openException, "[BENCH] The report could not be opened automatically.");
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "[BENCH] Bench run failed.");

            if (OperatingSystem.IsWindowsVersionAtLeast(6, 1))
            {
                MessageBox.Show(
                    $"Bench failed:\r\n\r\n{ex.Message}",
                    "Optimization XY Charger - Bench",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
        finally
        {
            PipelineState.Instance.Clear();
        }
    }

    /// <summary>Runs each folder in turn through one fake PLC, as consecutive blocks.</summary>
    public static async Task<List<BenchScanResult>> RunFoldersAsync(IReadOnlyList<string> scanFolders)
    {
        var settings = PipelineState.Instance.Settings;

        // In memory for this run only (the settings file is not saved): the bench
        // must never stop on an operator dialog, and it always talks to its fake
        // PLC even where PLC integration is switched off (the development PC).
        settings.Kinematic.EnableKinematicPositionSelection = false;
        settings.Plc.Enabled = true;

        var fake = new FakeXyPlcConnection();
        using var plc = new XyRestrictedPlcService(settings.Plc, () => fake);
        plc.DiagnosticsMessage += (_, message) => Logger.Info($"[BENCH][PLC] {message}");

        await StartAsync(plc, fake).ConfigureAwait(false);

        var results = new List<BenchScanResult>();
        int block = FirstBenchBlockNumber;

        foreach (string folder in scanFolders)
        {
            BenchScanResult result;

            try
            {
                ScannerLookupTable table =
                    OfflineReplayRunner.LoadSavedCalibration(folder)
                    ?? throw new InvalidDataException("No valid scanner-calibration.json saved with this scan.");

                List<ScanFrame> frames = ScanFrameLoader.LoadFromFolder(Path.Combine(folder, "Frames"));
                result = await RunScanAsync(folder, frames, table, plc, fake, block).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"[BENCH] {folder} failed.");
                result = new BenchScanResult
                {
                    ScanFolder = folder,
                    BlockNumber = block,
                    Outcome = "FAILED: " + ex.Message
                };
            }
            finally
            {
                PipelineState.Instance.Clear();
            }

            results.Add(result);
            block++;
        }

        return results;
    }

    /// <summary>Connects the service to the fake PLC and waits for its start-up baseline reads.</summary>
    public static async Task StartAsync(XyRestrictedPlcService plc, FakeXyPlcConnection fake)
    {
        ArgumentNullException.ThrowIfNull(plc);
        ArgumentNullException.ThrowIfNull(fake);

        await plc.StartAsync().ConfigureAwait(false);

        // The start-up reads of ScanStart / ScanComplete answer after the fake's
        // read delay; they set the baseline that the first edge is compared with.
        await Task.Delay(Math.Max(50, fake.ReadDelayMilliseconds * 4)).ConfigureAwait(false);
    }

    /// <summary>
    /// One production cycle for one scan: the same steps MainForm runs from
    /// ScanStart to the PLC write. The PLC service must be started on
    /// <paramref name="fake"/> (see <see cref="StartAsync"/>).
    /// </summary>
    public static async Task<BenchScanResult> RunScanAsync(
        string scanFolder,
        List<ScanFrame> frames,
        ScannerLookupTable table,
        XyRestrictedPlcService plc,
        FakeXyPlcConnection fake,
        int blockNumber)
    {
        ArgumentNullException.ThrowIfNull(frames);
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(plc);
        ArgumentNullException.ThrowIfNull(fake);

        var result = new BenchScanResult { ScanFolder = scanFolder, BlockNumber = blockNumber, Frames = frames.Count };
        PipelineState state = PipelineState.Instance;
        var coordinator = new LogProcessingCoordinator();
        coordinator.DiagnosticsMessage += (_, message) => Logger.Info($"[BENCH] {message}");

        try
        {
            // 1. The PLC starts the turn, then reports it complete.
            fake.SetScanStart(true);
            fake.SetScanComplete(true);
            await plc.WaitForScanCompleteAsync().ConfigureAwait(false);
            ScanTimeline.Start("PLC ScanComplete received (fake PLC)");

            // 2. Processing: the same pipeline as production, on the saved frames.
            state.Clear();
            state.ScanFrames = frames;
            var processing = Stopwatch.StartNew();
            await Task.Run(() => LogProcessingPipeline.Instance.RunCurrentFramesPipeline(
                table,
                CancellationToken.None,
                message => Logger.Info($"[BENCH] {message}"))).ConfigureAwait(false);
            result.ProcessingSeconds = processing.Elapsed.TotalSeconds;
            ScanTimeline.Mark("Scan processed, position chosen");

            // 3. The scan-quality gate (still blocking).
            OperatorScanQualityResult quality = OperatorScanQualityAnalyzer.Analyze(state);
            state.OperatorScanQuality = quality;
            result.ScanQuality = $"{quality.Status}: {quality.OperatorMessage}";

            // 4. Payload, stroke guard and readiness. Send-then-save, as in production.
            state.ProductionRunSequence = blockNumber;
            state.ResultSaveDeferredUntilAfterPlcWrite = true;
            ProductionReadinessReport readiness = coordinator.PrepareProductionCommand(plc.IsConnected);
            ScanTimeline.Mark("Payload built, readiness checked");

            result.StrokeGuard = state.PlcStrokeGuard?.Summary() ?? "(no payload)";
            result.Readiness = readiness.BuildSummary();

            if (!readiness.CanSendToPlc || state.PlcPayload is null)
            {
                string blockedBy = string.Join("; ", readiness.Checks
                    .Where(c => c.Status == ProductionCheckStatus.Block)
                    .Select(c => $"{c.Name}: {c.Message}"));

                result.Outcome = "NOT SENT - blocked by " + (blockedBy.Length > 0 ? blockedBy : "no payload");
                return result;
            }

            // 5. The PLC write + readback, through the real service code.
            var write = Stopwatch.StartNew();
            PlcTransmissionRecord transmission = await plc.SendPayloadAsync(state.PlcPayload).ConfigureAwait(false);
            result.WriteMilliseconds = write.Elapsed.TotalMilliseconds;
            ScanTimeline.Mark(transmission.Success ? "PLC write + readback verified" : "PLC write attempted (failed)");

            result.ResultWindow = PlcResultWindow.Describe(
                ScanTimeline.ElapsedSeconds,
                state.Settings.Plc.LateResultWarningSeconds,
                state.Settings.Plc.PlcResultWindowSeconds,
                out _);

            ScanTimeline.Stop();
            result.Timeline = ScanTimeline.BuildReport();
            result.Sent = transmission.Success;
            result.ScannedBlockDataReceived = fake.LastScannedBlockData;
            result.Outcome = transmission.Success
                ? "SENT and read back"
                : "WRITE FAILED: " + transmission.Message;

            return result;
        }
        finally
        {
            ScanTimeline.Stop();

            // End of the PLC cycle: both signals drop before the next block.
            fake.SetScanComplete(false);
            fake.SetScanStart(false);
        }
    }

    /// <summary>The bench report text.</summary>
    public static string BuildReport(string root, IReadOnlyList<BenchScanResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);

        CultureInfo ci = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();

        sb.AppendLine("=== PRODUCTION BENCH (fake PLC) ===");
        sb.AppendLine(ci, $"Run: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine(ci, $"Folder: {root}");
        sb.AppendLine("Real PLC: not contacted. SQL: not written. Cameras: not opened. Scan save: skipped.");
        sb.AppendLine("Settings: the appsettings.json on THIS computer. Copy the production one here first to match production.");
        sb.AppendLine();
        sb.AppendLine(ci, $"Scans: {results.Count}; sent: {results.Count(r => r.Sent)}; not sent / failed: {results.Count(r => !r.Sent)}");

        var processed = results.Where(r => r.ProcessingSeconds > 0).ToList();
        if (processed.Count > 0)
        {
            sb.AppendLine(ci,
                $"Processing time on this computer: median {Median(processed.Select(r => r.ProcessingSeconds)):F2} s, " +
                $"max {processed.Max(r => r.ProcessingSeconds):F2} s (production PC timing differs).");
        }

        sb.AppendLine();

        foreach (BenchScanResult r in results)
        {
            sb.AppendLine(ci, $"--- Block {r.BlockNumber}: {r.ScanFolder}");
            sb.AppendLine(ci, $"Outcome: {r.Outcome}");
            sb.AppendLine(ci, $"Frames: {r.Frames}; processing {r.ProcessingSeconds:F2} s; write + readback {r.WriteMilliseconds:F0} ms");

            if (r.ScanQuality.Length > 0) sb.AppendLine("Scan quality: " + r.ScanQuality);
            if (r.StrokeGuard.Length > 0) sb.AppendLine(r.StrokeGuard);
            if (r.ResultWindow.Length > 0) sb.AppendLine(r.ResultWindow);

            if (r.ScannedBlockDataReceived.Length > 0)
            {
                sb.AppendLine("Fake PLC received ScannedBlockData: [" +
                    string.Join(", ", r.ScannedBlockDataReceived.Select(v => v.ToString("0.0000", ci))) + "]");
            }

            if (r.Timeline.Length > 0) sb.Append(r.Timeline);
            if (r.Readiness.Length > 0) sb.Append(r.Readiness);
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static double Median(IEnumerable<double> values)
    {
        List<double> sorted = [.. values.OrderBy(v => v)];
        int m = sorted.Count / 2;
        return sorted.Count % 2 == 1 ? sorted[m] : (sorted[m - 1] + sorted[m]) / 2.0;
    }
}
