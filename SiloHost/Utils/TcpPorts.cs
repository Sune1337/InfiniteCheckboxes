namespace SiloHost.Utils;

using System.Net.NetworkInformation;

public static class TcpPorts
{
    #region Public Methods and Operators

    public static int GetNextFreeTcpPort(int startingPort)
    {
        var properties = IPGlobalProperties.GetIPGlobalProperties();
        var busyPorts = properties.GetActiveTcpListeners()
            .Where(ipep => ipep.Port >= startingPort)
            .Select(ipep => ipep.Port)
            .ToHashSet();
        
        for (var port = startingPort; port <= ushort.MaxValue; port++)
        {
            if (busyPorts.Contains(port))
            {
                continue;
            }

            return port;
        }

        throw new Exception("No free ports found.");
    }

    #endregion
}
