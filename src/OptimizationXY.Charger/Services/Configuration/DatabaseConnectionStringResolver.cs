using OptimizationXY.Charger.Models.Settings;

namespace OptimizationXY.Charger.Services.Configuration;

public static class DatabaseConnectionStringResolver
{
    public const string EnvironmentVariableName =
        "OPTIMIZATION_XY_SQL_CONNECTION_STRING";

    public static string Resolve(DatabaseSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        string? environmentValue = Environment.GetEnvironmentVariable(
            EnvironmentVariableName);

        return string.IsNullOrWhiteSpace(environmentValue)
            ? settings.ConnectionString
            : environmentValue;
    }
}
