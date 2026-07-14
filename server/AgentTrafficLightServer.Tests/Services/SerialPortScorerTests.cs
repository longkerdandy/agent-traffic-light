using AgentTrafficLight.Server.Services;

namespace AgentTrafficLight.Server.Tests.Services;

/// <summary>
/// Tests for <see cref="SerialPortScorer"/>.
/// </summary>
public class SerialPortScorerTests
{
    [Fact]
    public void BestPort_PrefersHighestScoringPort()
    {
        var ports = new[] { "COM1", "COM2", "COM3" };
        var descriptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["COM1"] = "Generic serial",
            ["COM2"] = "USB Serial Port (ESP32)",
            ["COM3"] = "Bluetooth COM Port"
        };

        var best = SerialPortScorer.BestPort(ports, descriptions);

        Assert.Equal("COM2", best);
    }

    [Fact]
    public void BestPort_ExcludesBluetooth()
    {
        var ports = new[] { "COM1", "rfcomm0" };
        var descriptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["COM1"] = "USB Serial",
            ["rfcomm0"] = "Bluetooth serial"
        };

        var best = SerialPortScorer.BestPort(ports, descriptions);

        Assert.Equal("COM1", best);
    }

    [Fact]
    public void BestPort_ReturnsNullWhenNoPorts()
    {
        var best = SerialPortScorer.BestPort(Array.Empty<string>());

        Assert.Null(best);
    }

    [Fact]
    public void BestPort_ReturnsFirstWhenNoDescriptionsAndNoKeywords()
    {
        var ports = new[] { "COM1", "COM2" };

        var best = SerialPortScorer.BestPort(ports);

        Assert.Equal("COM1", best);
    }
}
