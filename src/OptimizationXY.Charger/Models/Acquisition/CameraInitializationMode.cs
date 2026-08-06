namespace OptimizationXY.Charger.Models.Acquisition;

/// <summary>
/// Selects how a Sapera acquisition device is initialized.
/// Programmatic mode connects directly to the Sapera server/resource and then
/// applies the required GenICam features. ConfigurationFile is retained only
/// as a controlled legacy fallback.
/// </summary>
public enum CameraInitializationMode
{ 
    Programmatic,
    ConfigurationFile
}
