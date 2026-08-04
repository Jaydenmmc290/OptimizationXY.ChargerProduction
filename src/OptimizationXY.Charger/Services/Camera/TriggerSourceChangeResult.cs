using System;
using System.Collections.Generic;
using System.Text;

namespace OptimizationXY.Charger.Services.Camera;

public sealed record TriggerSourceChangeResult(
    bool Success,
    string RequestedSource,
    string ActualSource,
    string Message);
