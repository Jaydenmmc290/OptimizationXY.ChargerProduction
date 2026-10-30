// =============================================================================
// GenieNanoCameraService.Unavailable.cs - stand-in when the Sapera SDK is missing
// -----------------------------------------------------------------------------
// Compiled only when SAPERA is NOT defined (e.g. the GitHub build check, or a
// development PC without the SDK). It has the same public members as the real
// GenieNanoCameraService so MainForm and the forms compile, but every attempt
// to connect or start acquisition throws PlatformNotSupportedException. Stop/
// Disconnect do nothing. Camera discovery returns no cameras.
// =============================================================================

#if !SAPERA
using OptimizationXY.Charger.Models.Acquisition;

namespace OptimizationXY.Charger.Services.Camera;

/// <summary>Non-hardware build of the camera service: reports "not available" for every camera action.</summary>
public sealed class GenieNanoCameraService : IDisposable
{
    private const string MissingSdkMessage =
        "Sapera camera support is not available in this build. Install the 64-bit Teledyne DALSA Sapera LT SDK, set SAPERA_SDK_DIR, and rebuild the win-x64 production target.";

    public bool IsConnected => false;
    public string ConnectedSerialNumber => string.Empty;
    public string ConnectedModelName => string.Empty;
    public Control? PanelCameraView => null;
    public ReadOnlyMemory<byte> LoadedConfigurationSnapshot => ReadOnlyMemory<byte>.Empty;
    public event EventHandler<CameraFrameEventArgs>? FrameAcquired;
    public event EventHandler<string>? DiagnosticsMessage;

    public void Connect(CameraDeviceSettings settings) => ThrowUnavailable();

    public void Connect(
        CameraDeviceSettings settings,
        Control panelCameraView) =>
        ThrowUnavailable();

    // Same signature as the Sapera build, so MainForm compiles without the SDK.
    public void Connect(
        CameraDeviceSettings settings,
        Control panelCameraView,
        Exception argumentOutOfRangeException1,
        ArgumentOutOfRangeException argumentOutOfRangeException,
        DiscoveredCamera? openLocation = null) =>
        ThrowUnavailable();

    public void StartLive() => ThrowUnavailable();

    public void StartManagedFrameCapture() =>
        ThrowUnavailable();

    public void StopLive()
    {
    }

    public void StopManagedFrameCapture()
    {
    }

    public void Disconnect()
    {
    }

    public void Dispose() => Disconnect();

    /// <summary>Logs and throws the "Sapera support is not available in this build" error.</summary>
    private void ThrowUnavailable()
    {
        DiagnosticsMessage?.Invoke(
            this,
            MissingSdkMessage);

        throw new PlatformNotSupportedException(
            MissingSdkMessage);
    }
}

/// <summary>
/// Non-hardware build: no Sapera SDK, so no cameras can be discovered.
/// The production build (SAPERA defined) uses the real discovery service.
/// </summary>
public static class SaperaCameraDiscoveryService
{
    public static IReadOnlyList<DiscoveredCamera> Discover() => [];

    public static IReadOnlyList<DiscoveredCamera> DiscoverWithIdentity() => [];
}
#endif
