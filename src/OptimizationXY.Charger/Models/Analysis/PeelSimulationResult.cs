using Emgu.CV;

namespace OptimizationXY.Charger.Models.Analysis
{
    public sealed class PeelSimulationResult
    {
        public Mat Image { get; set; } = new();
        public int Revolutions { get; set; }
        public double OuterDiameterMM { get; set; }
        public double CoreDiameterMM { get; set; }
        public double LogLengthMM { get; set; }
        public double TotalWaneAreaPercent { get; set; }
        public double SevereWaneAreaPercent { get; set; }

        public int TotalFullSheets { get; set; }
        public int UsableFullSheets { get; set; }
        public int RejectedFullSheets { get; set; }

        public double TotalVeneerLengthMM { get; set; }
        public double UsableVeneerLengthMM { get; set; }
        public double PartialSheetLengthMM { get; set; }
        public double AverageUsableCoveragePercent { get; set; }
    }
}
