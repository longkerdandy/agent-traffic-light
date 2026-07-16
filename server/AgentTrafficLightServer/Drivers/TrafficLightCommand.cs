namespace AgentTrafficLight.Server.Drivers;

/// <summary>
/// Canonical traffic-light commands sent to AgentCore-Light compatible hardware.
/// </summary>
public enum TrafficLightCommand
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
/// Extensions for converting <see cref="TrafficLightCommand"/> values to firmware commands.
/// </summary>
public static class TrafficLightCommandExtensions
{
    /// <summary>
    /// Maps a traffic-light command to the command string understood by the firmware.
    /// </summary>
    /// <param name="command">The command to convert.</param>
    /// <returns>The firmware command string.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="command"/> is not supported.</exception>
    public static string ToCommandString(this TrafficLightCommand command) => command switch
    {
        TrafficLightCommand.Off => "off",
        TrafficLightCommand.Idle => "idle",
        TrafficLightCommand.Thinking => "thinking",
        TrafficLightCommand.Ai => "ai",
        TrafficLightCommand.Busy => "busy",
        TrafficLightCommand.WaitConfirm => "wait_confirm",
        TrafficLightCommand.Success => "success",
        TrafficLightCommand.Error => "error",
        _ => throw new ArgumentOutOfRangeException(nameof(command), command, null)
    };
}
