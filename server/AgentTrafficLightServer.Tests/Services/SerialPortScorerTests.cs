using AgentTrafficLight.Server.Services;

namespace AgentTrafficLight.Server.Tests.Services;

/// <summary>
/// Tests for <see cref="SerialPortScorer"/>.
/// </summary>
public class SerialPortScorerTests
{
    [Fact]
    public void BestPort_PrefersEsp32OverUsb()
    {
        var ports = new[] { "COM1", "COM2", "COM3" };
        var descriptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["COM1"] = "Generic serial",
            ["COM2"] = "USB Serial Port",
            ["COM3"] = "USB Serial Port (ESP32)"
        };

        var best = SerialPortScorer.BestPort(ports, descriptions);

        Assert.Equal("COM3", best);
    }

    [Fact]
    public void BestPort_PrefersUsbOverGenericSerial()
    {
        var ports = new[] { "COM1", "COM2" };
        var descriptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["COM1"] = "Generic serial",
            ["COM2"] = "USB Serial Port"
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
    public void BestPort_ReturnsNullWhenNoPortMatches()
    {
        var ports = new[] { "COM1", "COM2" };
        var descriptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["COM1"] = "Unknown device",
            ["COM2"] = "Printer port"
        };

        var best = SerialPortScorer.BestPort(ports, descriptions);

        Assert.Null(best);
    }

    [Theory]
    [InlineData("CP2102 USB to UART Bridge Controller", "COM1")]
    [InlineData("CH340 USB-SERIAL", "COM1")]
    [InlineData("FTDI USB Serial Converter", "COM1")]
    public void BestPort_RecognizesCommonUsbBridgeChips(string description, string expectedPort)
    {
        var ports = new[] { "COM1", "COM2" };
        var descriptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["COM1"] = description,
            ["COM2"] = "Generic serial"
        };

        var best = SerialPortScorer.BestPort(ports, descriptions);

        Assert.Equal(expectedPort, best);
    }
}
