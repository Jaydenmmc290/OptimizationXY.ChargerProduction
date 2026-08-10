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

                    string serial =
                        (SapManager.GetSerialNumber(location) ??
                         string.Empty).Trim();

                    cameras.Add(new DiscoveredCamera
                    {
                        ServerName = serverName,
                        DeviceIndex = deviceIndex,
                        SerialNumber = serial
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
    /// Gets the serial number of the specified SapAcqDevice, throwing an exception if the serial number is unavailable or cannot be read.
    /// </summary>
    /// <param name="cameras">The collection of discovered cameras to search.</param>
    /// <param name="assignedSerialNumber">The serial number of the camera to find.</param>
    /// <returns>The discovered camera with the specified serial number, or null if not found.</returns>
    public static DiscoveredCamera? FindAssignedCamera(
        IEnumerable<DiscoveredCamera> cameras,
        string assignedSerialNumber)
    {
        if (string.IsNullOrWhiteSpace(assignedSerialNumber))
            return null;

        return cameras.FirstOrDefault(
            camera =>
                string.Equals(
                    camera.SerialNumber,
                    assignedSerialNumber.Trim(),
                    StringComparison.OrdinalIgnoreCase));
    }
}

#endif
