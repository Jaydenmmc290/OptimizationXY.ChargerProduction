using System;
using System.Collections.Generic;
using System.Text;

namespace OptimizationXY.Charger.UI.Controls;


public readonly record struct LogProfilePoint(
    double PositionInches,
    double RadiusInches,
    bool IsValid);
