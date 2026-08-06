/// <summary>
/// Represents a point in a log profile with position, radius, and validity.
/// </summary>

namespace OptimizationXY.Charger.UI.Controls;


/// <summary>
/// Represents a point in a log profile with position, radius, and validity.
/// </summary>
/// <param name="PositionInches"></param>
/// <param name="RadiusInches"></param>
/// <param name="IsValid"></param>
public readonly record struct LogProfilePoint(
    double PositionInches,
    double RadiusInches,
    bool IsValid);
