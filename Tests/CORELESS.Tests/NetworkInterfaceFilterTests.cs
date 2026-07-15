using Coreless.Services;
using Xunit;

namespace Coreless.Tests;

public class NetworkInterfaceFilterTests
{
    [Theory]
    [InlineData("Connexion au réseau local* 5-Npcap Packet Driver")]
    [InlineData("Connexion au réseau local* 6-QoS Packet Scheduler")]
    [InlineData("Ethernet-WFP Native MAC Layer")]
    [InlineData("Ethernet-WFP 802.3 MAC Layer")]
    [InlineData("Ethernet (débogueur du noyau)")]
    public void IsVirtualByName_MatchesKnownVirtualAdapters(string name)
    {
        Assert.True(NetworkInterfaceFilter.IsVirtualByName(name));
    }

    [Theory]
    [InlineData("Realtek PCIe GbE Family Controller")]
    [InlineData("Intel(R) Wi-Fi 6 AX200")]
    [InlineData("")]
    public void IsVirtualByName_LeavesRealAdaptersAlone(string name)
    {
        Assert.False(NetworkInterfaceFilter.IsVirtualByName(name));
    }
}
