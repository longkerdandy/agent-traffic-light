namespace AgentTrafficLight.Server.Models;

/// <summary>
/// Canonical traffic-light states sent to the AgentCore-Light hardware.
/// </summary>
public enum TrafficLightState
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
/// Extensions for converting <see cref="TrafficLightState"/> values to serial commands.
/// </summary>
public static class TrafficLightStateExtensions
{
    /// <summary>
    /// Maps a traffic-light state to the newline-terminated serial command understood by the firmware.
    /// </summary>
    /// <param name="state">The state to convert.</param>
    /// <returns>The serial command string.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="state"/> is not supported.</exception>
    public static string ToSerialCommand(this TrafficLightState state) => state switch
    {
        TrafficLightState.Off => "off",
        TrafficLightState.Idle => "idle",
        TrafficLightState.Thinking => "thinking",
        TrafficLightState.Ai => "ai",
        TrafficLightState.Busy => "busy",
        TrafficLightState.WaitConfirm => "wait_confirm",
        TrafficLightState.Success => "success",
        TrafficLightState.Error => "error",
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, null)
    };
}
