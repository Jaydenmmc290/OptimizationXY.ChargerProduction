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
}

#endif
