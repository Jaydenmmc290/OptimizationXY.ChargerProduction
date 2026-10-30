using System.Globalization;
using OptimizationXY.Charger.Infrastructure;

namespace OptimizationXY.Charger.Services.Production;
/// <summary>
/// Provides functionality to manage the production sequence number, ensuring that it is persisted across application runs and
/// that it does not exceed the maximum representable value for PLC FLOATs.
/// The last number used is kept in Config\production-sequence.txt; each
/// production scan takes the next one (it is sent to the PLC as the block count).
/// </summary>
public static class ProductionSequenceStore
{
    private static readonly Lock Sync = new();

    private static string SequencePath =>
        Path.Combine(ApplicationPaths.ConfigDirectory, "production-sequence.txt");

    /// <summary>
    /// Gets the next production sequence number, incrementing the stored value and ensuring it does not exceed the maximum representable PLC FLOAT value.
    /// </summary>
    /// <returns>The next production sequence number.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the production sequence file is invalid or the sequence has reached its maximum value.</exception>
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
            // Write to a temporary file, then rename over the real one, so a power cut
            // can never leave a half-written sequence file.
            File.Move(temporaryPath, SequencePath, overwrite: true);
            return next;
        }
    }
}
