namespace OptimizationXY.Charger.Models.Acquisition;

/// <summary>
/// Selects how a Sapera acquisition device is initialized.
/// Programmatic mode connects directly to the Sapera server/resource and then
/// applies the required GenICam features. ConfigurationFile is retained only
/// as a controlled legacy fallback.
/// </summary>
public enum CameraInitializationMode
{
    /// <summary>Open the camera by its Sapera location (or serial number) and write only the trigger features. Production default.</summary>
    Programmatic,
    /// <summary>Legacy: load a Sapera .ccf configuration file. Kept only as a fallback.</summary>
    ConfigurationFile
}
