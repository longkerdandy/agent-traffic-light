using AgentTrafficLight.Server.Models;
using AgentTrafficLight.Server.Services;
using AgentTrafficLight.Server.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentTrafficLight.Server.Tests.Services;

/// <summary>
/// Tests for <see cref="TrafficLightController"/>.
/// </summary>
public class TrafficLightControllerTests
{
    [Theory]
    [InlineData(TrafficLightState.Idle, "idle")]
    [InlineData(TrafficLightState.Thinking, "thinking")]
    [InlineData(TrafficLightState.Ai, "ai")]
    [InlineData(TrafficLightState.Busy, "busy")]
    [InlineData(TrafficLightState.WaitConfirm, "wait_confirm")]
    [InlineData(TrafficLightState.Success, "success")]
    [InlineData(TrafficLightState.Error, "error")]
    [InlineData(TrafficLightState.Off, "off")]
    public async Task SetStateAsync_WritesCorrectCommand(TrafficLightState state, string expectedCommand)
    {
        var fakeSerial = new FakeSerialController();
        var controller = new TrafficLightController(fakeSerial, NullLogger<TrafficLightController>.Instance);

        // Move to a state different from the target so the target state always triggers a write.
        var initialState = state == TrafficLightState.Off ? TrafficLightState.Thinking : TrafficLightState.Off;
        await controller.SetStateAsync(initialState);
        fakeSerial.Commands.Clear();

        await controller.SetStateAsync(state);

        Assert.Equal(expectedCommand, Assert.Single(fakeSerial.Commands));
        Assert.Equal(state, controller.CurrentState);
    }

    [Fact]
    public async Task SetStateAsync_DeduplicatesRepeatedState()
    {
        var fakeSerial = new FakeSerialController();
        var controller = new TrafficLightController(fakeSerial, NullLogger<TrafficLightController>.Instance);

        await controller.SetStateAsync(TrafficLightState.Busy);
        await controller.SetStateAsync(TrafficLightState.Busy);
        await controller.SetStateAsync(TrafficLightState.Busy);

        Assert.Single(fakeSerial.Commands);
        Assert.Equal("busy", fakeSerial.Commands[0]);
    }

    [Fact]
    public async Task SetStateAsync_WritesWhenStateChanges()
    {
        var fakeSerial = new FakeSerialController();
        var controller = new TrafficLightController(fakeSerial, NullLogger<TrafficLightController>.Instance);

        await controller.SetStateAsync(TrafficLightState.Idle);
        await controller.SetStateAsync(TrafficLightState.Thinking);
        await controller.SetStateAsync(TrafficLightState.Idle);

        Assert.Equal(3, fakeSerial.Commands.Count);
        Assert.Equal(new[] { "idle", "thinking", "idle" }, fakeSerial.Commands);
    }
}
