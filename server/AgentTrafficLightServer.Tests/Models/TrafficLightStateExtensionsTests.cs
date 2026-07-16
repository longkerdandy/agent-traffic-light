using AgentTrafficLight.Contracts.Models;

namespace AgentTrafficLight.Server.Tests.Models;

/// <summary>
/// Tests for <see cref="TrafficLightStateExtensions"/>.
/// </summary>
public class TrafficLightStateExtensionsTests
{
    [Theory]
    [InlineData(TrafficLightState.Off, "off")]
    [InlineData(TrafficLightState.Idle, "idle")]
    [InlineData(TrafficLightState.Thinking, "thinking")]
    [InlineData(TrafficLightState.Ai, "ai")]
    [InlineData(TrafficLightState.Busy, "busy")]
    [InlineData(TrafficLightState.WaitConfirm, "wait_confirm")]
    [InlineData(TrafficLightState.Success, "success")]
    [InlineData(TrafficLightState.Error, "error")]
    public void ToCommandString_ReturnsExpectedCommand(TrafficLightState state, string expectedCommand)
    {
        Assert.Equal(expectedCommand, state.ToCommandString());
    }
}
