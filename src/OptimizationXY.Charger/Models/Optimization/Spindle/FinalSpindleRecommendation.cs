using OptimizationXY.Charger.Models.Optimization;

namespace OptimizationXY.Charger.Models.Optimization.Spindle
{
    public sealed class FinalSpindleRecommendation
    {
        public SpindleOptimizationMode OptimizationMode { get; set; }
        public string SelectedStrategy { get; set; } = string.Empty;

        public bool IsRecommended { get; set; }
        public bool MachineMoveAllowed { get; set; }

        public string RecommendedAction { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;

        public List<string> Warnings { get; set; } = new();

        // Operator-facing score
        public double RiskScore { get; set; }
        public string RiskRating { get; set; } = string.Empty;

        public double BaselineRiskScore { get; set; }
        public string BaselineRiskRating { get; set; } = string.Empty;

        public double RiskScoreReductionPercent { get; set; }

        // Actual machine command.
        // If IsRecommended is false, these should be zero.
        public double CommandTailXMM { get; set; }
        public double CommandTailYMM { get; set; }
        public double CommandHeadXMM { get; set; }
        public double CommandHeadYMM { get; set; }

        // Best tested optimizer position.
        // This is useful for engineering review even if not recommended.
        public double BestTestedTailXMM { get; set; }
        public double BestTestedTailYMM { get; set; }
        public double BestTestedHeadXMM { get; set; }
        public double BestTestedHeadYMM { get; set; }

        public double MaxSpindleMovementMM { get; set; }

        public int TotalFullSheets { get; set; }
        public int UsableFullSheets { get; set; }
        public int RejectedFullSheets { get; set; }
        public double UsableVeneerLengthMM { get; set; }

        public double InscribedMajorDiameterMM { get; set; }
        public double InscribedMinorDiameterMM { get; set; }
        public double InscribedOrientationDeg { get; set; }

        // Engineering/debug values
        public double BaselineWaneScoreMM { get; set; }
        public double OptimizedWaneScoreMM { get; set; }

        public double BaselineInternalSearchScore { get; set; }
        public double OptimizedInternalSearchScore { get; set; }

        public double BaselineDefectPenalty { get; set; }
        public double OptimizedDefectPenalty { get; set; }

        public int TestedCandidates { get; set; }
        public int CoarseCandidates { get; set; }
        public int MediumCandidates { get; set; }
        public int FineCandidates { get; set; }
    }
}
