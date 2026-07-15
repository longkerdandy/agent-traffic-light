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

        Assert.Equal(TrafficLightState.Off, controller.CurrentState);
        Assert.False(controller.IsConnected);
    }

    [Fact]
    public async Task SetStateAsync_Throws_WhenNotConnected()
    {
        var controller = CreateController();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => controller.SetStateAsync(TrafficLightState.Busy));

        Assert.Contains("ConnectAsync", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SetStateAsync_DoesNotThrow_ForSameState_WhenNotConnected()
    {
        var controller = CreateController();

        // CurrentState is already Off; requesting Off again should short-circuit
        // before checking whether the controller is connected.
        await controller.SetStateAsync(TrafficLightState.Off);

        Assert.Equal(TrafficLightState.Off, controller.CurrentState);
    }

    private static BleTrafficLightController CreateController()
    {
        var options = Options.Create(new BleOptions());
        return new BleTrafficLightController(options, NullLogger<BleTrafficLightController>.Instance);
    }
}
