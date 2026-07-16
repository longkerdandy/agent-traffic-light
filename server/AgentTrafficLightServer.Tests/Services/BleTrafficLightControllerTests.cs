using AgentTrafficLight.Server.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AgentTrafficLight.Server.Tests.Services;

public sealed class BleTrafficLightControllerTests
{
    [Fact]
    public void Constructor_SetsDefaults()
    {
        var controller = CreateController();

        Assert.Equal(AgentState.Off, controller.CurrentState);
        Assert.False(controller.IsConnected);
    }

    [Fact]
    public async Task SetStateAsync_Throws_WhenConnectionFails()
    {
        var options = Options.Create(new BleOptions { ScanTimeoutMs = 100 });
        var controller = new BleTrafficLightController(options, NullLogger<BleTrafficLightController>.Instance);

        if (OperatingSystem.IsWindows())
        {
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => controller.SetStateAsync(AgentState.Busy));

            Assert.Contains("was not found", exception.Message, StringComparison.Ordinal);
        }
        else
        {
            // The Windows Runtime BLE APIs are not available on Linux/WSL.
            await Assert.ThrowsAsync<DllNotFoundException>(
                () => controller.SetStateAsync(AgentState.Busy));
        }
    }

    [Fact]
    public async Task SetStateAsync_DoesNotThrow_ForSameState_WhenNotConnected()
    {
        var controller = CreateController();

        // CurrentState is already Off; requesting Off again should short-circuit
        // before checking whether the controller is connected.
        await controller.SetStateAsync(AgentState.Off);

        Assert.Equal(AgentState.Off, controller.CurrentState);
    }

    private static BleTrafficLightController CreateController()
    {
        var options = Options.Create(new BleOptions());
        return new BleTrafficLightController(options, NullLogger<BleTrafficLightController>.Instance);
    }
}
