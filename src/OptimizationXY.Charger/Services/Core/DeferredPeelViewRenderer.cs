using System.Diagnostics;
using System.Text;
using Emgu.CV;
using NLog;
using OptimizationXY.Charger.Models.Analysis;
using OptimizationXY.Charger.Visualization;

namespace OptimizationXY.Charger.Services.Core;

/// <summary>
/// Draws the peel images that the pipeline queued in
/// <see cref="PipelineState.DeferredPeelViews"/>.
///
/// The spindle position is chosen from peel numbers only
/// (UnwrappedPeelViewBuilder.ComputeMetrics), so the PLC does not wait for
/// these large images. They are drawn here, when the scan is saved, which is
/// after the PLC write in the default send-first order.
///
/// Every drawn image also recalculates its numbers. They are compared with the
/// numbers the decision used; a mismatch is written to the diagnostics report
/// and the application log so it can never go unnoticed.
///
/// The images are display-only. A drawing failure is logged and the save
/// continues without that image.
/// </summary>
public static class DeferredPeelViewRenderer
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private const double MetricTolerance = 1e-9;

    public static void RenderPending(PipelineState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state.DeferredPeelViews.Count == 0)
            return;

        var stopwatch = Stopwatch.StartNew();
        var report = new StringBuilder();
        report.Append("\r\nPEEL IMAGES (drawn after the spindle decision)\r\n");

        // Draw the named strategies first so the selected (Unwrapped) view can
        // reuse an identical image instead of drawing it twice.
        var ordered = state.DeferredPeelViews
            .OrderBy(request => request.Slot == PeelViewSlot.Unwrapped ? 1 : 0)
            .ToList();

        var rendered = new List<(DeferredPeelView Request, Mat Image)>();
        int mismatches = 0;

        foreach (DeferredPeelView request in ordered)
        {
            try
            {
                Mat? reusable = rendered
                    .Where(item => SameAxis(item.Request.DecisionMetrics, request.DecisionMetrics))
                    .Select(item => item.Image)
                    .FirstOrDefault();

                Mat image;

                if (reusable is not null && !reusable.IsEmpty)
                {
                    image = reusable.Clone();
                    report.Append($"{request.Slot}: same axis as an image already drawn; copied.\r\n");
                }
                else
                {
                    PeelSimulationResult drawn = DrawFor(state, request.DecisionMetrics);
                    image = drawn.Image;

                    string difference = Compare(request.DecisionMetrics, drawn);

                    if (difference.Length == 0)
                    {
                        report.Append($"{request.Slot}: drawn; sheet counts and wane match the decision.\r\n");
                    }
                    else
                    {
                        mismatches++;
                        report.Append($"{request.Slot}: WARNING drawn image numbers differ from the decision: {difference}\r\n");
                        Logger.Error(
                            $"Peel image {request.Slot} does not match the decision metrics: {difference}");
                    }
                }

                Assign(state, request.Slot, image);
                rendered.Add((request, image));
            }
            catch (Exception ex)
            {
                report.Append($"{request.Slot}: image could not be drawn ({ex.Message}). The scan is saved without it.\r\n");
                Logger.Error(ex, $"Deferred peel image {request.Slot} could not be drawn.");
            }
        }

        stopwatch.Stop();

        report.Append(
            $"Images drawn: {rendered.Count} of {ordered.Count} in {stopwatch.Elapsed.TotalSeconds:F2} s" +
            (mismatches > 0 ? $"; {mismatches} MISMATCH(ES) - see warnings above" : string.Empty) +
            "\r\n");

        state.DiagnosticsReport += report.ToString();
        state.DeferredPeelViews.Clear();
    }

    private static PeelSimulationResult DrawFor(
        PipelineState state,
        PeelSimulationResult metrics)
    {
        return UnwrappedPeelViewBuilder.BuildSimulation(
            state.PointCloud,
            state.Settings.PeelView,
            state.Settings.UsableRadius,
            state.Settings.Wane,
            metrics.AxisTailXMM,
            metrics.AxisTailYMM,
            metrics.AxisHeadXMM,
            metrics.AxisHeadYMM);
    }

    private static bool SameAxis(PeelSimulationResult a, PeelSimulationResult b) =>
        a.AxisTailXMM == b.AxisTailXMM &&
        a.AxisTailYMM == b.AxisTailYMM &&
        a.AxisHeadXMM == b.AxisHeadXMM &&
        a.AxisHeadYMM == b.AxisHeadYMM;

    /// <summary>
    /// Empty when the decision numbers and the drawn numbers agree.
    /// </summary>
    internal static string Compare(PeelSimulationResult decision, PeelSimulationResult drawn)
    {
        var differences = new List<string>();

        void Check(string name, double a, double b)
        {
            if (Math.Abs(a - b) > MetricTolerance)
                differences.Add($"{name} {a:G6} vs {b:G6}");
        }

        Check("TotalFullSheets", decision.TotalFullSheets, drawn.TotalFullSheets);
        Check("UsableFullSheets", decision.UsableFullSheets, drawn.UsableFullSheets);
        Check("RejectedFullSheets", decision.RejectedFullSheets, drawn.RejectedFullSheets);
        Check("UsableVeneerLengthMM", decision.UsableVeneerLengthMM, drawn.UsableVeneerLengthMM);
        Check("TotalVeneerLengthMM", decision.TotalVeneerLengthMM, drawn.TotalVeneerLengthMM);
        Check("TotalWaneAreaPercent", decision.TotalWaneAreaPercent, drawn.TotalWaneAreaPercent);
        Check("SevereWaneAreaPercent", decision.SevereWaneAreaPercent, drawn.SevereWaneAreaPercent);
        Check("Revolutions", decision.Revolutions, drawn.Revolutions);

        return string.Join("; ", differences);
    }

    private static void Assign(PipelineState state, PeelViewSlot slot, Mat image)
    {
        switch (slot)
        {
            case PeelViewSlot.Unwrapped:
                state.UnwrappedPeelView?.Dispose();
                state.UnwrappedPeelView = image;
                break;
            case PeelViewSlot.Cylinder:
                state.CylinderPeelView?.Dispose();
                state.CylinderPeelView = image;
                break;
            case PeelViewSlot.Oval:
                state.OvalPeelView?.Dispose();
                state.OvalPeelView = image;
                break;
            case PeelViewSlot.Baseline:
                state.BaselinePeelView?.Dispose();
                state.BaselinePeelView = image;
                break;
            case PeelViewSlot.Penalty:
                state.PenaltyPeelView?.Dispose();
                state.PenaltyPeelView = image;
                break;
            case PeelViewSlot.DirectRecovery:
                state.DirectRecoveryPeelView?.Dispose();
                state.DirectRecoveryPeelView = image;
                break;
            default:
                image.Dispose();
                throw new ArgumentOutOfRangeException(nameof(slot), slot, "Unknown peel view slot.");
        }
    }
}
