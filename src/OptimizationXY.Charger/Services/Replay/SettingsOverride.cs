using System.Globalization;
using System.Reflection;
using OptimizationXY.Charger.Models.Settings;

namespace OptimizationXY.Charger.Services.Replay;

/// <summary>
/// Changes one setting in memory for a replay, from text such as
/// "InscribedGeometry.CenterlinePullWeight=1.0" or "OptimizationMode=RecoveryOptimized".
/// Nothing is written to appsettings.json. Used to compare a setting against the
/// current behaviour on saved scans before turning it on in production.
/// </summary>
public static class SettingsOverride
{
    public const string CommandLineSwitch = "--set";

    /// <summary>Every "--set A.B=value" on the command line, in order.</summary>
    public static List<string> FromCommandLine(string[] args)
    {
        var overrides = new List<string>();

        for (int index = 0; index < args.Length - 1; index++)
        {
            if (string.Equals(args[index], CommandLineSwitch, StringComparison.OrdinalIgnoreCase))
                overrides.Add(args[index + 1].Trim('"'));
        }

        return overrides;
    }

    /// <summary>
    /// Applies one "Path.To.Setting=value". Throws with a clear message when the
    /// path or the value is not valid.
    /// </summary>
    public static string Apply(AppSettings settings, string assignment)
    {
        ArgumentNullException.ThrowIfNull(settings);

        int equals = assignment.IndexOf('=', StringComparison.Ordinal);
        if (equals <= 0)
            throw new ArgumentException($"Setting override '{assignment}' must look like Section.Name=value.");

        string path = assignment[..equals].Trim();
        string text = assignment[(equals + 1)..].Trim();
        string[] parts = path.Split('.', StringSplitOptions.RemoveEmptyEntries);

        object target = settings;

        for (int index = 0; index < parts.Length; index++)
        {
            PropertyInfo property =
                target.GetType().GetProperty(
                    parts[index],
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
                ?? throw new ArgumentException($"Setting '{path}' does not exist ('{parts[index]}' was not found).");

            if (index < parts.Length - 1)
            {
                target = property.GetValue(target)
                    ?? throw new ArgumentException($"Setting section '{parts[index]}' is empty.");
                continue;
            }

            if (!property.CanWrite)
                throw new ArgumentException($"Setting '{path}' cannot be changed.");

            object? value = Convert(text, property.PropertyType, path);
            property.SetValue(target, value);
            return $"{path} = {text}";
        }

        throw new ArgumentException($"Setting override '{assignment}' has no setting name.");
    }

    /// <summary>
    /// Converts the text after '=' to the setting's type: enums by name (any
    /// case), true/false, strings as-is, numbers with invariant culture ('.' as
    /// decimal point). "null" or empty clears a nullable setting.
    /// </summary>
    /// <exception cref="ArgumentException">The text is not a valid value for the setting.</exception>
    private static object? Convert(string text, Type type, string path)
    {
        Type underlying = Nullable.GetUnderlyingType(type) ?? type;

        if (Nullable.GetUnderlyingType(type) is not null &&
            (text.Length == 0 || string.Equals(text, "null", StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        try
        {
            if (underlying.IsEnum)
                return Enum.Parse(underlying, text, ignoreCase: true);

            if (underlying == typeof(bool))
                return bool.Parse(text);

            if (underlying == typeof(string))
                return text;

            return System.Convert.ChangeType(text, underlying, CultureInfo.InvariantCulture);
        }
        catch (Exception ex) when (ex is FormatException or InvalidCastException or ArgumentException or OverflowException)
        {
            throw new ArgumentException($"'{text}' is not a valid value for {path} ({underlying.Name}).", ex);
        }
    }
}
