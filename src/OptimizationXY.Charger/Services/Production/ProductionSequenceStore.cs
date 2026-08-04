using System.Globalization;
using OptimizationXY.Charger.Infrastructure;

namespace OptimizationXY.Charger.Services.Production;

public static class ProductionSequenceStore
{
    private static readonly object Sync = new();

    private static string SequencePath =>
        Path.Combine(ApplicationPaths.ConfigDirectory, "production-sequence.txt");

    public static int GetNext()
    {
        lock (Sync)
        {
            ApplicationPaths.EnsureCreated();

            int current = 0;
            if (File.Exists(SequencePath))
            {
                string text = File.ReadAllText(SequencePath).Trim();
                if (!int.TryParse(
                        text,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out current) ||
                    current < 0 ||
                    current > 16_000_000)
                {
                    throw new InvalidOperationException(
                        $"Production sequence file is invalid: {SequencePath}. " +
                        "Correct it under commissioning control before resuming PLC writes.");
                }
            }

            if (current >= 16_000_000)
            {
                throw new InvalidOperationException(
                    "The production block sequence has reached its maximum exactly representable PLC FLOAT value. " +
                    "Reset the sequence only through an approved PLC/controls change procedure; automatic wraparound is blocked.");
            }

            int next = current + 1;
            string temporaryPath = SequencePath + ".tmp";
            File.WriteAllText(
                temporaryPath,
                next.ToString(CultureInfo.InvariantCulture));
            File.Move(temporaryPath, SequencePath, overwrite: true);
            return next;
        }
    }
}
