namespace AgentTrafficLight.Server.Models;

/// <summary>
/// Canonical traffic-light states sent to the AgentCore-Light hardware.
/// </summary>
public enum AgentState
{
    Off,         // All LEDs off.
    Idle,        // Green breathing effect.
    Thinking,    // Fast red-yellow-green chase.
    Ai,          // Soft red-yellow-green chase.
    Busy,        // Yellow slow blink.
    WaitConfirm, // Yellow steady while waiting for user confirmation.
    Success,     // Green steady for 5 seconds, then returns to Idle.
    Error        // Red fast blink.
}

/// <summary>
/// Extensions for converting <see cref="AgentState"/> values to firmware commands.
/// </summary>
public static class AgentStateExtensions
{
    /// <summary>
    /// Maps a traffic-light state to the command string understood by the firmware.
    /// </summary>
    /// <param name="state">The state to convert.</param>
    /// <returns>The firmware command string.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="state"/> is not supported.</exception>
    public static string ToCommandString(this AgentState state) => state switch
    {
        AgentState.Off => "off",
        AgentState.Idle => "idle",
        AgentState.Thinking => "thinking",
        AgentState.Ai => "ai",
        AgentState.Busy => "busy",
        AgentState.WaitConfirm => "wait_confirm",
        AgentState.Success => "success",
        AgentState.Error => "error",
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, null)
    };
}
