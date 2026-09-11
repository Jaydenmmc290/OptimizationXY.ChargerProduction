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
            SafeCarriageRetractRadiusMM = (largestRadiusMM + knifeClearanceMM) * MM_TO_INCHES,

            // Populate spindle rotation from kinematics calculations
            SpindleRotationDegrees = state.KinematicVisualizationState?.SpindleRotationDeg ?? 0.0
        };

        ValidateFinite(payload.SafeCarriageRetractRadiusMM);
        return payload;
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
