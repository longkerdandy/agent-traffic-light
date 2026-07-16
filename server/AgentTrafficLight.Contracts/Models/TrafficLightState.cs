namespace AgentTrafficLight.Contracts.Models;

/// <summary>
/// Canonical traffic-light states sent to the AgentCore-Light hardware.
/// </summary>
public enum TrafficLightState
{
    /// <summary>
    /// All LEDs off.
    /// </summary>
    Off,

    /// <summary>
    /// Green breathing effect.
    /// </summary>
    Idle,

    /// <summary>
    /// Fast red-yellow-green chase.
    /// </summary>
    Thinking,

    /// <summary>
    /// Soft red-yellow-green chase.
    /// </summary>
    Ai,

    /// <summary>
    /// Yellow slow blink.
    /// </summary>
    Busy,

    /// <summary>
    /// Yellow steady while waiting for user confirmation.
    /// </summary>
    WaitConfirm,

    /// <summary>
    /// Green steady for 5 seconds, then returns to Idle.
    /// </summary>
    Success,

    /// <summary>
    /// Red fast blink.
    /// </summary>
    Error
}

/// <summary>
/// Extensions for converting <see cref="TrafficLightState"/> values to firmware commands.
/// </summary>
public static class TrafficLightStateExtensions
{
    /// <summary>
    /// Maps a traffic-light state to the command string understood by the firmware.
    /// </summary>
    /// <param name="state">The state to convert.</param>
    /// <returns>The firmware command string.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="state"/> is not supported.</exception>
    public static string ToCommandString(this TrafficLightState state) => state switch
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
