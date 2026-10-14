using OptimizationXY.Charger.Models.Analysis;
using OptimizationXY.Charger.Models.Optimization.Geometry;

namespace OptimizationXY.Charger.Services.Optimization.Recovery;

/// <summary>
/// Selects the geometry-only spindle result using actual fixed-axis circular
/// virtual-peel recovery. Geometry size is used only after peel-recovery ties.
/// </summary>
public static class GeometryPeelSelector
{
    /// <summary>
    /// Selects the geometry-only spindle result using actual fixed-axis circular
    /// virtual-peel recovery. Geometry size is used only after peel-recovery ties.
    /// </summary>
    /// <param name="cylinder">The inscribed geometry result for the cylinder.</param>
    /// <param name="cylinderPeel">The peel simulation result for the cylinder.</param>
    /// <param name="oval">The inscribed geometry result for the oval.</param>
    /// <param name="ovalPeel">The peel simulation result for the oval.</param>
    /// <returns>The result of the geometry peel comparison.</returns>
    public static GeometryPeelComparisonResult Select(
        InscribedGeometryResult cylinder,
        PeelSimulationResult cylinderPeel,
        InscribedGeometryResult oval,
        PeelSimulationResult ovalPeel,
        double ovalMinimumWaneAdvantagePercent = 0.0)
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
        else if (comparison < 0 &&
                 OvalWinIsOnlyMarginalWane(cylinderPeel, ovalPeel, ovalMinimumWaneAdvantagePercent))
        {
            // The oval only won on a small wane difference. That decision flips
            // between repeat scans of the same block and moves the axis for no
            // extra veneer, so the cylinder is kept.
            selectedKind = InscribedGeometryKind.Cylinder;
            reason =
                $"{oval.StrategyName} matched {cylinder.StrategyName} on usable sheets, usable veneer length " +
                $"and rejected sheets, and was better on wane by less than {ovalMinimumWaneAdvantagePercent:0.##}% " +
                $"(severe {cylinderPeel.SevereWaneAreaPercent - ovalPeel.SevereWaneAreaPercent:0.##}%, " +
                $"total {cylinderPeel.TotalWaneAreaPercent - ovalPeel.TotalWaneAreaPercent:0.##}%), " +
                $"so {cylinder.StrategyName} was kept (InscribedGeometry.OvalMinimumWaneAdvantagePercent).";
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

    /// <summary>
    /// Compares two peel simulation results to determine which is better based on
    /// usable full sheets, usable veneer length, rejected full sheets, and wane.
    /// </summary>
    /// <param name="left">The first peel simulation result to compare.</param>
    /// <param name="right">The second peel simulation result to compare.</param>
    /// <returns>
    /// A positive value if the first result is better, a negative value if the second result is better, or zero if they are equal.
    /// </returns>
    /// <summary>
    /// True when the oval matches the cylinder on everything except wane and
    /// beats it on wane by less than the margin. A margin of 0 or less turns the
    /// rule off.
    /// </summary>
    internal static bool OvalWinIsOnlyMarginalWane(
        PeelSimulationResult cylinderPeel,
        PeelSimulationResult ovalPeel,
        double marginPercent)
    {
        if (!(marginPercent > 0.0))
            return false;

        if (ovalPeel.UsableFullSheets != cylinderPeel.UsableFullSheets ||
            ovalPeel.RejectedFullSheets != cylinderPeel.RejectedFullSheets ||
            Math.Abs(ovalPeel.UsableVeneerLengthMM - cylinderPeel.UsableVeneerLengthMM) > 0.001)
        {
            return false;
        }

        double severeGain = cylinderPeel.SevereWaneAreaPercent - ovalPeel.SevereWaneAreaPercent;
        double totalGain = cylinderPeel.TotalWaneAreaPercent - ovalPeel.TotalWaneAreaPercent;

        return severeGain < marginPercent && totalGain < marginPercent;
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

    /// <summary>
    /// Builds a reason string explaining why one peel simulation result was selected over another.
    /// </summary>
    /// <param name="winnerName">The name of the winning peel simulation result.</param>
    /// <param name="winner">The winning peel simulation result.</param>
    /// <param name="otherName">The name of the other peel simulation result.</param>
    /// <param name="other">The other peel simulation result.</param>
    /// <returns>A string explaining the reason for the selection.</returns>
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
