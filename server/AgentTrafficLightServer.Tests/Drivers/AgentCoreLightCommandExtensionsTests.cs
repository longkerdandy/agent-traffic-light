namespace AgentTrafficLight.Server.Tests.Drivers;

/// <summary>
/// Tests for <see cref="AgentCoreLightCommandExtensions"/>.
/// </summary>
public class AgentCoreLightCommandExtensionsTests
{
    [Theory]
    [InlineData(AgentCoreLightCommand.Off, "off")]
    [InlineData(AgentCoreLightCommand.Idle, "idle")]
    [InlineData(AgentCoreLightCommand.Thinking, "thinking")]
    [InlineData(AgentCoreLightCommand.Ai, "ai")]
    [InlineData(AgentCoreLightCommand.Busy, "busy")]
    [InlineData(AgentCoreLightCommand.WaitConfirm, "wait_confirm")]
    [InlineData(AgentCoreLightCommand.Success, "success")]
    [InlineData(AgentCoreLightCommand.Error, "error")]
    public void ToCommandString_ReturnsExpectedCommand(AgentCoreLightCommand command, string expectedCommand)
    {
        Assert.Equal(expectedCommand, command.ToCommandString());
    }
}
