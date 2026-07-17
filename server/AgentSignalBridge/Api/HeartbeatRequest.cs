namespace AgentSignalBridge.Server.Api;

/// <summary>
/// Request body for keeping a session alive.
/// </summary>
public sealed class HeartbeatRequest
{
    /// <summary>
    /// Gets or sets the agent identifier.
    /// </summary>
    public string AgentId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the agent name, for example "kimi", "claude", or "codex".
    /// </summary>
    public string AgentName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the working directory associated with the agent, if any.
    /// </summary>
    public string? Cwd { get; set; }
}
