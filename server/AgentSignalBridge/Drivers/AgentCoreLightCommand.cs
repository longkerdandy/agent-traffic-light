namespace AgentSignalBridge.Server.Drivers;

/// <summary>
/// Canonical AgentCoreLight commands sent to AgentCore-Light compatible hardware.
/// </summary>
public enum AgentCoreLightCommand
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
/// Extensions for converting <see cref="AgentCoreLightCommand"/> values to firmware commands.
/// </summary>
public static class AgentCoreLightCommandExtensions
{
    /// <summary>
    /// Maps an AgentCoreLight command to the command string understood by the firmware.
    /// </summary>
    /// <param name="command">The command to convert.</param>
    /// <returns>The firmware command string.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="command"/> is not supported.</exception>
    public static string ToCommandString(this AgentCoreLightCommand command) => command switch
    {
        AgentCoreLightCommand.Off => "off",
        AgentCoreLightCommand.Idle => "idle",
        AgentCoreLightCommand.Thinking => "thinking",
        AgentCoreLightCommand.Ai => "ai",
        AgentCoreLightCommand.Busy => "busy",
        AgentCoreLightCommand.WaitConfirm => "wait_confirm",
        AgentCoreLightCommand.Success => "success",
        AgentCoreLightCommand.Error => "error",
        _ => throw new ArgumentOutOfRangeException(nameof(command), command, null)
    };
}
