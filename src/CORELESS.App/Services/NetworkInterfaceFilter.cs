namespace Coreless.Services;

/// <summary>
/// Identifies virtual/inactive network adapters that LibreHardwareMonitor reports raw
/// (Npcap/QoS/WFP filter drivers, kernel debugger adapters) — noise that clutters the
/// UI and bloats exported reports without carrying any useful sensor data.
/// </summary>
public static class NetworkInterfaceFilter
{
    private static readonly string[] VirtualNameSuffixes =
    {
        "-Npcap Packet Driver",
        "-QoS Packet Scheduler",
        "-WFP Native MAC Layer",
        "-WFP 802.3 MAC Layer",
    };

    public static bool IsVirtualByName(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        if (name.Contains("débogueur du noyau", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Kernel Debugger", StringComparison.OrdinalIgnoreCase))
            return true;

        foreach (string suffix in VirtualNameSuffixes)
            if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                return true;

        return false;
    }
}
