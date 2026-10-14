using OptimizationXY.Charger.Models.Geometry;
using OptimizationXY.Charger.Models.Plc;
using OptimizationXY.Charger.Services.Core;

namespace OptimizationXY.Charger.Services.Production;

/// <summary>
/// Builds a PLC command payload from the current pipeline state, ensuring that all required data is present and valid.
/// </summary>
public static class PlcPayloadBuilder
{
    private const int MaximumExactlyRepresentablePlcBlockCount = 16_000_000;
    private const double MM_TO_INCHES = 1.0 / 25.4;  // Convert millimeters to inches for PLC transmission

    /// <summary>
    /// Builds a PLC command payload from the provided pipeline state.
    /// </summary>
    /// <param name="state">The current pipeline state containing all necessary information for building the PLC payload.</param>
    /// <returns>A PlcCommandPayload object representing the command to be sent to the PLC.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the pipeline state is invalid or incomplete.</exception>
    public static PlcCommandPayload Build(PipelineState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var recommendation = state.FinalSpindleRecommendation
            ?? throw new InvalidOperationException(
                "A final spindle recommendation is required before building the PLC payload.");

        if (!recommendation.MachineMoveAllowed)
        {
            throw new InvalidOperationException(
                "The final spindle recommendation is blocked by machine safety limits.");
        }

        if (state.BinnedRadiusField.Count == 0)
        {
            throw new InvalidOperationException(
                "The binned radius field is required before building the PLC payload.");
        }

        double largestRadiusMM = state.BinnedRadiusField
            .Where(point => point.Count > 0)
            .Select(point => point.MaxRadiusMM)
            .DefaultIfEmpty(state.Model.MaxDiameterMM / 2.0)
            .Max();

        double smallestRadiusMM = state.BinnedRadiusField
            .Where(point => point.Count > 0 && point.MinRadiusMM > 0)
            .Select(point => point.MinRadiusMM)
            .DefaultIfEmpty(state.Model.MinDiameterMM / 2.0)
            .Min();

        double knifeClearanceMM =
            state.Settings.Safety.ComputeKnifeClearanceMM(
                state.Settings.UsableRadius.VeneerThicknessMM);

        // The carriage must clear the block as it actually turns. largestRadiusMM
        // above is measured from the fitted PCA centreline, but the block spins
        // around the spindle axis, which is offset from that centreline by up to
        // the commanded move. Use the largest measured distance from either the
        // current spindle axis or the commanded one, so the retract radius is
        // never smaller than it was and covers the real swing.
        double swingRadiusMM = LargestSwingRadiusMM(
            state.PointCloud,
            recommendation.CommandTailXMM,
            recommendation.CommandTailYMM,
            recommendation.CommandHeadXMM,
            recommendation.CommandHeadYMM);

        double retractBaseRadiusMM = double.IsFinite(swingRadiusMM)
            ? Math.Max(largestRadiusMM, swingRadiusMM)
            : largestRadiusMM;

        ValidateFinite(
            recommendation.CommandHeadXMM,
            recommendation.CommandHeadYMM,
            recommendation.CommandTailXMM,
            recommendation.CommandTailYMM,
            state.Model.MinDiameterMM,
            state.Model.MaxDiameterMM,
            largestRadiusMM,
            smallestRadiusMM,
            knifeClearanceMM);

        if (state.Model.MinDiameterMM <= 0 ||
            state.Model.MaxDiameterMM <= 0 ||
            largestRadiusMM <= 0 ||
            smallestRadiusMM <= 0)
        {
            throw new InvalidOperationException(
                "PLC geometry values must be positive.");
        }

        if (state.Model.MaxDiameterMM < state.Model.MinDiameterMM ||
            largestRadiusMM < smallestRadiusMM)
        {
            throw new InvalidOperationException(
                "PLC geometry ranges are internally inconsistent.");
        }

        int blockCount = state.ProductionRunSequence > 0
            ? state.ProductionRunSequence
            : ProductionSequenceStore.GetNext();

        if (blockCount <= 0 ||
            blockCount > MaximumExactlyRepresentablePlcBlockCount)
        {
            throw new InvalidOperationException(
                $"PLC block count must be between 1 and {MaximumExactlyRepresentablePlcBlockCount} so it remains exactly representable in the FLOAT payload.");
        }

        state.ProductionRunSequence = blockCount;

        PlcCommandPayload payload = new()
        {
            BlockCount = blockCount,
            HeadXMM = recommendation.CommandHeadXMM * MM_TO_INCHES,
            HeadYMM = recommendation.CommandHeadYMM * MM_TO_INCHES,
            TailXMM = recommendation.CommandTailXMM * MM_TO_INCHES,
            TailYMM = recommendation.CommandTailYMM * MM_TO_INCHES,
            SmallDiameterMM = state.Model.MinDiameterMM * MM_TO_INCHES,
            LargeDiameterMM = state.Model.MaxDiameterMM * MM_TO_INCHES,
            LargestMeasuredRadiusMM = largestRadiusMM * MM_TO_INCHES,
            SmallestMeasuredRadiusMM = smallestRadiusMM * MM_TO_INCHES,
            KnifeClearanceMM = knifeClearanceMM * MM_TO_INCHES,
            SafeCarriageRetractRadiusMM = (retractBaseRadiusMM + knifeClearanceMM) * MM_TO_INCHES
        };

        ValidateFinite(payload.SafeCarriageRetractRadiusMM);
        return payload;
    }

    /// <summary>
    /// Largest distance of any measured point from the spindle axis, checked for
    /// both the current axis (0,0 at both ends) and the commanded axis, in mm.
    ///
    /// PointCloud X/Y are measured from the current spindle axis, Z = 0 is the
    /// tail end. Every optimization mode builds the PLC command as the negative
    /// of the selected axis position (Command = -SpindleMovement), so in
    /// point-cloud coordinates the commanded axis is at -Command. A command of
    /// zero is the current axis. The axis is interpolated from
    /// tail (minimum Z) to head (maximum Z), the same way as the optimizer and
    /// the virtual peel.
    ///
    /// Returns NaN when there are no usable points.
    /// </summary>
    internal static double LargestSwingRadiusMM(
        List<Point3D>? pointCloud,
        double commandTailXMM,
        double commandTailYMM,
        double commandHeadXMM,
        double commandHeadYMM)
    {
        if (pointCloud is null || pointCloud.Count == 0)
            return double.NaN;

        double minZ = double.PositiveInfinity;
        double maxZ = double.NegativeInfinity;

        foreach (Point3D point in pointCloud)
        {
            if (!double.IsFinite(point.Z))
                continue;

            if (point.Z < minZ)
                minZ = point.Z;

            if (point.Z > maxZ)
                maxZ = point.Z;
        }

        if (!double.IsFinite(minZ) || !double.IsFinite(maxZ))
            return double.NaN;

        double length = Math.Max(maxZ - minZ, 0.0001);

        double axisTailX = -commandTailXMM;
        double axisTailY = -commandTailYMM;
        double axisHeadX = -commandHeadXMM;
        double axisHeadY = -commandHeadYMM;

        double largestSquared = double.NaN;

        foreach (Point3D point in pointCloud)
        {
            if (!double.IsFinite(point.X) ||
                !double.IsFinite(point.Y) ||
                !double.IsFinite(point.Z))
            {
                continue;
            }

            double t = Math.Clamp((point.Z - minZ) / length, 0.0, 1.0);

            double currentSquared = point.X * point.X + point.Y * point.Y;

            double dx = point.X - (axisTailX + (axisHeadX - axisTailX) * t);
            double dy = point.Y - (axisTailY + (axisHeadY - axisTailY) * t);
            double commandedSquared = dx * dx + dy * dy;

            double pointLargest = Math.Max(currentSquared, commandedSquared);

            if (double.IsNaN(largestSquared) || pointLargest > largestSquared)
                largestSquared = pointLargest;
        }

        return double.IsNaN(largestSquared)
            ? double.NaN
            : Math.Sqrt(largestSquared);
    }

    /// <summary>
    /// Validates that all provided double values are finite and within the range representable by a FLOAT in the PLC payload.
    /// </summary>
    /// <param name="values">The double values to validate.</param>
    /// <exception cref="InvalidOperationException">Thrown if any value is not finite or outside the FLOAT range.</exception>
    private static void ValidateFinite(params double[] values)
    {
        if (values.Any(value =>
                !double.IsFinite(value) ||
                value > float.MaxValue ||
                value < -float.MaxValue))
        {
            throw new InvalidOperationException(
                "PLC payload contains a non-finite value or a value outside the FLOAT range.");
        }
    }
}
