
// OptimizedSpindleMovement.cs - a spindle axis position, as a move from the current axis.
// (This file uses a block-scoped namespace, unlike most files in the project.)
namespace OptimizationXY.Charger.Models.Optimization.Spindle
{
    /// <summary>
    /// A candidate or selected spindle axis, expressed as how far its tail and
    /// head ends sit from the current spindle axis, in millimetres, in the
    /// point-cloud frame (X/Y across the log, Z = 0 at the tail end).
    /// </summary>
    /// <remarks>
    /// The axis is a straight line from (TailMoveX, TailMoveY) at the tail end to
    /// (HeadMoveX, HeadMoveY) at the head end. Center* is the middle of that line
    /// and SweepCorrection* is the tail-minus-head difference (see
    /// SpindleMovementBuilder). The PLC command is the negative of these values.
    /// </remarks>
    public class OptimizedSpindleMovement
    {

        /// <summary>
        /// Tail-end axis offset (mm) in point-cloud X and Y.
        /// </summary>
        public double TailMoveXMM { get; set; }
        public double TailMoveYMM { get; set; }

        /// <summary>Head-end axis offset (mm) in point-cloud X and Y.</summary>
        public double HeadMoveXMM { get; set; }
        public double HeadMoveYMM { get; set; }

        /// <summary>Offset (mm) of the axis at the middle of the block.</summary>
        public double CenterMoveXMM { get; set; }
        public double CenterMoveYMM { get; set; }

        /// <summary>Tail offset minus head offset (mm): how much the axis is tilted end to end.</summary>
        public double SweepCorrectionXMM { get; set; }
        public double SweepCorrectionYMM { get; set; }
    }
}
