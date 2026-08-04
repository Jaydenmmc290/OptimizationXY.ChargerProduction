using OptimizationXY.Charger.Models.Optimization.Spindle;
using System;
using System.Collections.Generic;
using System.Text;

namespace OptimizationXY.Charger.Models.Optimization.Results
{
    public class ChargerOptimizationResult  // This class represents the result of an optimization process for a charger,
                                            // including various metrics and penalties associated with the optimization.
    {
        public double OffsetXMM { get; set; }  // The offset in millimeters along the X-axis.
        public double OffsetYMM { get; set; }  // The offset in millimeters along the Y-axis.

        public double WaneScore { get; set; }  // A score representing the wane (deviation from ideal shape) of the charger.
        public double DefectPenalty { get; set; } // A penalty score representing the presence of defects in the charger.

        public double MovementPenalty { get; set; }  // A penalty score representing the amount of movement applied to the charger, which may affect its performance.

        public double FinalScore { get; set; }  // The final score of the optimization, which may be a combination of the wane score and the various penalties.

        public bool IsRecommended { get; set; }  // A boolean indicating whether the optimization result is recommended based on the final score and other criteria.
        public double ImprovementPercent { get; set; }  // The percentage improvement of the optimization result compared to a baseline or previous result.
        public int CoarseCandidates { get; set; }

        public int MediumCandidates { get; set; }

        public int FineCandidates { get; set; }
        public OptimizedSpindleMovement? SpindleMovement { get; set; }
        public int TestedCandidates { get; set; }  // The number of candidate solutions that were tested during the optimization process.

    }
}
