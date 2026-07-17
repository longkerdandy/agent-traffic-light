using AgentSignalBridge.Server.Models;

namespace AgentSignalBridge.Server.Models;

/// <summary>
/// Represents an active agent session tracked by the server.
/// </summary>
public sealed class Agent
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

    /// <summary>
    /// Gets or sets the latest agent lifecycle event.
    /// </summary>
    public AgentEvent Event { get; set; }

    /// <summary>
    /// Gets or sets the last time the agent was seen.
    /// </summary>
    public DateTimeOffset LastSeen { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this agent is currently the master agent.
    /// </summary>
    public bool IsMaster { get; set; }
}
