namespace OptimizationXY.Charger.UI.Controls;


/// <summary>
/// One axial slice of the measured block, as drawn by the log profile view.
/// </summary>
/// <param name="PositionInches">Distance from the tail end of the scanned length.</param>
/// <param name="RadiusInches">Typical (median) radius of the slice.</param>
/// <param name="IsValid">False when the slice did not have enough angular coverage.</param>
/// <param name="MinRadiusInches">Narrowest radius measured around the slice (0 when unknown).</param>
/// <param name="MaxRadiusInches">Widest radius measured around the slice (0 when unknown).</param>
/// <param name="CenterXInches">Block centreline X at this slice, relative to the spindle axis.</param>
/// <param name="CenterYInches">Block centreline Y at this slice, relative to the spindle axis (down is positive).</param>
/// <param name="HasCenter">True when the centreline values are available.</param>
public readonly record struct LogProfilePoint(
    double PositionInches,
    double RadiusInches,
    bool IsValid,
    double MinRadiusInches = 0.0,
    double MaxRadiusInches = 0.0,
    double CenterXInches = 0.0,
    double CenterYInches = 0.0,
    bool HasCenter = false);
