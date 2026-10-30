using OptimizationXY.Charger.Models.Optimization.Spindle;

namespace OptimizationXY.Charger.Services.Optimization.Movement;

/// <summary>
/// Builder class for creating optimized spindle movement configurations based on sweep parameters.
/// <para>
/// The searches describe a spindle axis by two numbers per direction:
/// an OFFSET (how far the middle of the axis moves) and a SWEEP (how much the
/// tail end moves relative to the head end, which tilts the axis). This turns
/// them into the four end moves:
/// tail = offset + sweep/2, head = offset − sweep/2 (separately for X and Y).
/// Example: offset X = 4 mm, sweep X = 2 mm → tail X = 5 mm, head X = 3 mm.
/// Values are mm in the point-cloud frame (current axis = 0, 0); the PLC
/// command is the negative of these (see FinalSpindleRecommendation).
/// </para>
/// </summary>
public static class SpindleMovementBuilder
{
    /// <summary>
    /// Builds an optimized spindle movement configuration based on the provided sweep parameters.
    /// </summary>
    /// <param name="offsetXMM">The X-axis offset in millimeters.</param>
    /// <param name="offsetYMM">The Y-axis offset in millimeters.</param>
    /// <param name="sweepXMM">The sweep distance along the X-axis in millimeters.</param>
    /// <param name="sweepYMM">The sweep distance along the Y-axis in millimeters.</param>
    /// <returns>An optimized spindle movement configuration.</returns>
    public static OptimizedSpindleMovement BuildFromSweep(
        double offsetXMM,
        double offsetYMM,
        double sweepXMM,
        double sweepYMM)
    {
        double halfSweepX = sweepXMM / 2.0;
        double halfSweepY = sweepYMM / 2.0;

        return new OptimizedSpindleMovement
        {
            CenterMoveXMM = offsetXMM,
            CenterMoveYMM = offsetYMM,

            SweepCorrectionXMM = sweepXMM,
            SweepCorrectionYMM = sweepYMM,

            TailMoveXMM = offsetXMM + halfSweepX,
            TailMoveYMM = offsetYMM + halfSweepY,

            HeadMoveXMM = offsetXMM - halfSweepX,
            HeadMoveYMM = offsetYMM - halfSweepY
        };
    }
}
