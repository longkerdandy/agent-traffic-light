using AgentTrafficLight.Contracts.Models;

namespace AgentTrafficLight.Contracts.Api;

/// <summary>
/// Request body for reporting an agent lifecycle event.
/// </summary>
public sealed class HookRequest
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
    /// Gets or sets the canonical agent lifecycle event.
    /// </summary>
    public AgentEvent Event { get; set; }
}
