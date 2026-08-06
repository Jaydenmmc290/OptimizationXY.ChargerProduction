using OptimizationXY.Charger.Models.Settings;

namespace OptimizationXY.Charger.Services.Configuration;
/// <summary>
/// Resolves the database connection string from environment variables or settings.
/// </summary>
public static class DatabaseConnectionStringResolver
{
    public const string EnvironmentVariableName =
        "OPTIMIZATION_XY_SQL_CONNECTION_STRING";


    /// <summary>
    /// Resolves the database connection string from environment variables or settings.
    /// </summary>
    /// <param name="settings">The database settings containing the connection string to resolve.</param>
    /// <returns>The resolved database connection string.</returns>
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
