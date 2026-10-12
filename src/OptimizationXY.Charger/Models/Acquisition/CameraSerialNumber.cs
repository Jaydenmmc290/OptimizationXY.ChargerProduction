namespace OptimizationXY.Charger.Models.Acquisition;

/// <summary>
/// Compares camera serial numbers the way people type them: case, spaces,
/// dashes and other separators are ignored, so "02-2020C-07697",
/// "022020C07697" and "02 2020c 07697" are all the same camera.
/// </summary>
public static class CameraSerialNumber
{
    /// <summary>
    /// Returns the serial number with only its letters and digits, upper case.
    /// </summary>
    public static string Normalize(string? serial) =>
        string.IsNullOrWhiteSpace(serial)
            ? string.Empty
            : new string([.. serial
                .Where(char.IsLetterOrDigit)
                .Select(char.ToUpperInvariant)]);

    /// <summary>
    /// True when both values are non-empty and name the same serial number.
    /// </summary>
    public static bool Matches(string? expected, string? reported)
    {
        string a = Normalize(expected);
        string b = Normalize(reported);

        return a.Length > 0 && string.Equals(a, b, StringComparison.Ordinal);
    }

    /// <summary>
    /// True when any of the identity strings reported by a camera matches the
    /// expected serial number.
    /// </summary>
    public static bool MatchesAny(string? expected, IEnumerable<string> reported) =>
        reported.Any(value => Matches(expected, value));
}
