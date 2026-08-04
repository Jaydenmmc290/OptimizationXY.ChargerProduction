using OptimizationXY.Charger.Models.Analysis;
using OptimizationXY.Charger.Models.Optimization.Geometry;

namespace OptimizationXY.Charger.Services.Optimization.Recovery;

/// <summary>
/// Selects the geometry-only spindle result using actual fixed-axis circular
/// virtual-peel recovery. Geometry size is used only after peel-recovery ties.
/// </summary>
public static class GeometryPeelSelector
{
    public static GeometryPeelComparisonResult Select(
        InscribedGeometryResult cylinder,
        PeelSimulationResult cylinderPeel,
        InscribedGeometryResult oval,
        PeelSimulationResult ovalPeel)
    {
        ArgumentNullException.ThrowIfNull(cylinder);
        ArgumentNullException.ThrowIfNull(cylinderPeel);
        ArgumentNullException.ThrowIfNull(oval);
        ArgumentNullException.ThrowIfNull(ovalPeel);

        int comparison = ComparePeels(cylinderPeel, ovalPeel);

        InscribedGeometryKind selectedKind;
        string reason;

        if (comparison > 0)
        {
            selectedKind = InscribedGeometryKind.Cylinder;
            reason = BuildReason(
                cylinder.StrategyName,
                cylinderPeel,
                oval.StrategyName,
                ovalPeel);
        }
        else if (comparison < 0)
        {
            selectedKind = InscribedGeometryKind.Oval;
            reason = BuildReason(
                oval.StrategyName,
                ovalPeel,
                cylinder.StrategyName,
                cylinderPeel);
        }
        else
        {
            // A conventional rotary lathe can always realize the oval's minor
            // radius as a circle. In a complete peel tie, prefer the geometry
            // with the larger guaranteed circular radius, then less movement.
            bool cylinderWins =
                cylinder.GuaranteedCircularRadiusMM >
                    oval.GuaranteedCircularRadiusMM + 0.001 ||
                (Math.Abs(
                    cylinder.GuaranteedCircularRadiusMM -
                    oval.GuaranteedCircularRadiusMM) <= 0.001 &&
                 cylinder.MaxSpindleMovementMM <=
                    oval.MaxSpindleMovementMM);

            selectedKind = cylinderWins
                ? InscribedGeometryKind.Cylinder
                : InscribedGeometryKind.Oval;

            reason =
                "The virtual peels tied on usable full sheets, usable veneer " +
                "length, rejected sheets, and wane. The tie was resolved by " +
                "the larger guaranteed circular radius and then lower spindle movement.";
        }

        return new GeometryPeelComparisonResult
        {
            Cylinder = cylinder,
            CylinderPeel = cylinderPeel,
            Oval = oval,
            OvalPeel = ovalPeel,
            SelectedKind = selectedKind,
            SelectionReason = reason
        };
    }

    private static int ComparePeels(
        PeelSimulationResult left,
        PeelSimulationResult right)
    {
        int compare =
            left.UsableFullSheets.CompareTo(
                right.UsableFullSheets);

        if (compare != 0)
            return compare;

        compare =
            left.UsableVeneerLengthMM.CompareTo(
                right.UsableVeneerLengthMM);

        if (compare != 0)
            return compare;

        // Fewer rejected full sheets is better.
        compare =
            right.RejectedFullSheets.CompareTo(
                left.RejectedFullSheets);

        if (compare != 0)
            return compare;

        // Lower severe wane is better.
        compare =
            right.SevereWaneAreaPercent.CompareTo(
                left.SevereWaneAreaPercent);

        if (compare != 0)
            return compare;

        // Lower total wane is better.
        return
            right.TotalWaneAreaPercent.CompareTo(
                left.TotalWaneAreaPercent);
    }

    private static string BuildReason(
        string winnerName,
        PeelSimulationResult winner,
        string otherName,
        PeelSimulationResult other)
    {
        if (winner.UsableFullSheets != other.UsableFullSheets)
        {
            return
                $"{winnerName} produced {winner.UsableFullSheets} usable " +
                $"full 50-inch sheets versus {other.UsableFullSheets} for " +
                $"{otherName}.";
        }

        if (Math.Abs(
            winner.UsableVeneerLengthMM -
            other.UsableVeneerLengthMM) > 0.001)
        {
            return
                $"{winnerName} tied on usable full-sheet count but produced " +
                "more usable veneer length.";
        }

        if (winner.RejectedFullSheets != other.RejectedFullSheets)
        {
            return
                $"{winnerName} tied on usable recovery but rejected fewer " +
                "full 50-inch sheets.";
        }

        return
            $"{winnerName} tied on sheet recovery and was selected because " +
            "its simulated severe/total wane was lower.";
    }
}
