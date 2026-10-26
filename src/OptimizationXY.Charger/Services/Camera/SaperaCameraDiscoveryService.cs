#if SAPERA

using DALSA.SaperaLT.SapClassBasic;
using OptimizationXY.Charger.Models.Acquisition;

namespace OptimizationXY.Charger.Services.Camera;

/// <summary>
/// Discovers acquisition devices currently visible to Sapera.
/// No camera names are hard-coded here.
/// </summary>
public static class SaperaCameraDiscoveryService
{

    /// <summary>
    /// Discovers all cameras currently visible to Sapera and returns their serial numbers.
    /// </summary>
    /// <returns>A read-only list of discovered cameras.</returns>
    public static IReadOnlyList<DiscoveredCamera> Discover()
    {
        var cameras = new List<DiscoveredCamera>();

        int serverCount = SapManager.GetServerCount();

        for (int serverIndex = 0; serverIndex < serverCount; serverIndex++)
        {
            string serverName;

            try
            {
                serverName = SapManager.GetServerName(serverIndex);
            }
            catch
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(serverName))
                continue;

            int resourceCount;

            try
            {
                resourceCount = SapManager.GetResourceCount(
                    serverName,
                    SapManager.ResourceType.AcqDevice);
            }
            catch
            {
                continue;
            }

            for (int deviceIndex = 0;
                 deviceIndex < resourceCount;
                 deviceIndex++)
            {
                try
                {
                    var location =
                        new SapLocation(serverName, deviceIndex);

                    cameras.Add(new DiscoveredCamera
                    {
                        ServerName = serverName,
                        DeviceIndex = deviceIndex
                    });
                }
                catch
                {
                    // This Sapera resource is not a usable acquisition device.
                }
            }
        }

        return [.. cameras
            .OrderBy(camera => camera.ServerName)
            .ThenBy(camera => camera.DeviceIndex)];
    }

    /// <summary>
    /// Feature names a GigE Vision / Genie camera may use for its serial number
    /// or a fixed device ID. Every one that exists is read, because different
    /// camera families put the label serial number in different features.
    /// </summary>
    private static readonly string[] IdentityFeatureNames =
    [
        "DeviceSerialNumber",
        "DeviceID",
        "DeviceUserID",
        "DeviceFactorySerialNumber",
        "deviceSerialNumber",
        "deviceID"
    ];

    /// <summary>
    /// Discovers every camera like <see cref="Discover"/> and also opens each one
    /// briefly to read the serial-number / device-ID strings it reports. Call it
    /// only while this program has no camera open (before connecting).
    /// </summary>
    private const int ProbeAttempts = 5;
    private const int ProbeRetryDelayMilliseconds = 2000;

    public static IReadOnlyList<DiscoveredCamera> DiscoverWithIdentity()
    {
        var probed = new List<DiscoveredCamera>();

        foreach (DiscoveredCamera camera in Discover())
        {
            var location = new SapLocation(camera.ServerName, camera.DeviceIndex);
            IReadOnlyList<string> identities = [];
            bool opened = false;

            // A camera that a closing program has just released stays reserved
            // for a few seconds, so the probe is retried before giving up.
            for (int attempt = 1; attempt <= ProbeAttempts && !opened; attempt++)
            {
                if (attempt > 1)
                    Thread.Sleep(ProbeRetryDelayMilliseconds);

                SapAcqDevice? device = null;
                try
                {
                    device = new SapAcqDevice(location);
                    if (device.Create())
                    {
                        identities = ReadIdentityStrings(device, location);
                        opened = true;
                    }
                }
                catch
                {
                    // Try again, then fall back below.
                }
                finally
                {
                    try { device?.Destroy(); } catch { /* probe only */ }
                    device?.Dispose();
                }
            }

            if (!opened)
                identities = ReadLocationSerialOnly(location);

            probed.Add(camera with { IdentityStrings = identities, IdentityProbed = opened });
        }

        return probed;
    }

    /// <summary>
    /// Reads every identity string an opened camera reports: the serial-number
    /// and device-ID features plus Sapera's own serial number for the location.
    /// Duplicates and empty values are removed.
    /// </summary>
    public static IReadOnlyList<string> ReadIdentityStrings(
        SapAcqDevice device,
        SapLocation location)
    {
        var values = new List<string>();

        foreach (string feature in IdentityFeatureNames)
        {
            try
            {
                if (device.IsFeatureAvailable(feature) &&
                    device.GetFeatureValue(feature, out string value) &&
                    !string.IsNullOrWhiteSpace(value))
                {
                    values.Add(value.Trim());
                }
            }
            catch
            {
                // Optional identity feature.
            }
        }

        values.AddRange(ReadLocationSerialOnly(location));

        return [.. values.Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    private static IReadOnlyList<string> ReadLocationSerialOnly(SapLocation location)
    {
        try
        {
            string serial = (SapManager.GetSerialNumber(location) ?? string.Empty).Trim();
            return serial.Length == 0 ? [] : [serial];
        }
        catch
        {
            return [];
        }
    }
}

#endif
