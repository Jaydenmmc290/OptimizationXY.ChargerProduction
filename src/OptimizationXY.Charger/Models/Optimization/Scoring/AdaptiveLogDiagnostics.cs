namespace OptimizationXY.Charger.Models.Optimization.Scoring
{
    public sealed class AdaptiveLogDiagnostics
    {
        public int TotalFrames { get; set; }
        public int ValidFrames { get; set; }

        public double LogLengthMM { get; set; }
        public double AverageDiameterMM { get; set; }

        public double CenterlineSweepMM { get; set; }
        public double MaxBowMM { get; set; }
        public double OutOfRoundMM { get; set; }

        public double LowWoodPercent { get; set; }
        public double SevereLowWoodPercent { get; set; }
        public double HighSpotPercent { get; set; }
        public double DeepestLowWoodMM { get; set; }

        public double WorstPatchSeverityScore { get; set; }
        public int LargestPatchCells { get; set; }

        public double CenterlineSweepIn => CenterlineSweepMM / 25.4;
        public double MaxBowIn => MaxBowMM / 25.4;
        public double OutOfRoundIn => OutOfRoundMM / 25.4;
        public double DeepestLowWoodIn => DeepestLowWoodMM < 0.0
            ? Math.Abs(DeepestLowWoodMM) / 25.4
            : 0.0;
    }
}
