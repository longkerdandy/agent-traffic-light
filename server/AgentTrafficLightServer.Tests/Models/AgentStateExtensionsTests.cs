using AgentTrafficLight.Server.Models;

namespace AgentTrafficLight.Server.Tests.Models;

/// <summary>
/// Tests for <see cref="AgentStateExtensions"/>.
/// </summary>
public class AgentStateExtensionsTests
{
    [Theory]
    [InlineData(AgentState.Off, "off")]
    [InlineData(AgentState.Idle, "idle")]
    [InlineData(AgentState.Thinking, "thinking")]
    [InlineData(AgentState.Ai, "ai")]
    [InlineData(AgentState.Busy, "busy")]
    [InlineData(AgentState.WaitConfirm, "wait_confirm")]
    [InlineData(AgentState.Success, "success")]
    [InlineData(AgentState.Error, "error")]
    public void ToCommandString_ReturnsExpectedCommand(AgentState state, string expectedCommand)
    {
        Assert.Equal(expectedCommand, state.ToCommandString());
    }
}
