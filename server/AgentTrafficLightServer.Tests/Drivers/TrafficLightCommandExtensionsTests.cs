namespace AgentTrafficLight.Server.Tests.Drivers;

/// <summary>
/// Tests for <see cref="TrafficLightCommandExtensions"/>.
/// </summary>
public class TrafficLightCommandExtensionsTests
{
    [Theory]
    [InlineData(TrafficLightCommand.Off, "off")]
    [InlineData(TrafficLightCommand.Idle, "idle")]
    [InlineData(TrafficLightCommand.Thinking, "thinking")]
    [InlineData(TrafficLightCommand.Ai, "ai")]
    [InlineData(TrafficLightCommand.Busy, "busy")]
    [InlineData(TrafficLightCommand.WaitConfirm, "wait_confirm")]
    [InlineData(TrafficLightCommand.Success, "success")]
    [InlineData(TrafficLightCommand.Error, "error")]
    public void ToCommandString_ReturnsExpectedCommand(TrafficLightCommand command, string expectedCommand)
    {
        Assert.Equal(expectedCommand, command.ToCommandString());
    }
}
