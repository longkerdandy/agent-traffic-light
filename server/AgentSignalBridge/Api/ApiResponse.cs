using AgentSignalBridge.Server.Drivers;
using AgentSignalBridge.Server.Models;

namespace AgentSignalBridge.Server.Api;

/// <summary>
/// Base response returned by API endpoints.
/// </summary>
public abstract class ApiResponse
{
    /// <summary>
    /// Gets or sets a value indicating whether the request succeeded.
    /// </summary>
    public bool Ok { get; set; }
}

/// <summary>
/// Response returned by <c>POST /hook</c>.
/// </summary>
public sealed class HookResponse : ApiResponse
{
    /// <summary>
    /// Gets or sets the agent identifier.
    /// </summary>
    public string AgentId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the agent is the master.
    /// </summary>
    public bool IsMaster { get; set; }

    /// <summary>
    /// Gets or sets the command mapped from the event, if the agent is the master.
    /// </summary>
    public AgentCoreLightCommand? Command { get; set; }
}

/// <summary>
/// Response returned by <c>POST /heartbeat</c>.
/// </summary>
public sealed class HeartbeatResponse : ApiResponse
{
    /// <summary>
    /// Gets or sets the agent identifier.
    /// </summary>
    public string AgentId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the agent is the master.
    /// </summary>
    public bool IsMaster { get; set; }
}

/// <summary>
/// Response returned by <c>POST /api/master</c>.
/// </summary>
public sealed class MasterResponse : ApiResponse
{
    /// <summary>
    /// Gets or sets the agent identifier.
    /// </summary>
    public string AgentId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the agent is the master.
    /// </summary>
    public bool IsMaster { get; set; }

    /// <summary>
    /// Gets or sets the error code when the request fails.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Gets or sets the current master agent identifier when the request conflicts.
    /// </summary>
    public string? MasterAgentId { get; set; }
}

/// <summary>
/// Response returned by <c>GET /api/status</c>.
/// </summary>
public sealed class StatusResponse : ApiResponse
{
    /// <summary>
    /// Gets or sets the last command sent to the hardware.
    /// </summary>
    public AgentCoreLightCommand? Command { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the hardware driver is connected.
    /// </summary>
    public bool IsConnected { get; set; }

    /// <summary>
    /// Gets or sets the current master agent identifier, if any.
    /// </summary>
    public string? MasterAgentId { get; set; }

    /// <summary>
    /// Gets or sets the time of the last state update.
    /// </summary>
    public DateTimeOffset? LastUpdated { get; set; }
}

/// <summary>
/// Information about a single active session.
/// </summary>
public sealed class SessionInfo
{
    /// <summary>
    /// Gets or sets the agent identifier.
    /// </summary>
    public string AgentId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the agent name.
    /// </summary>
    public string AgentName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the latest agent lifecycle event.
    /// </summary>
    public AgentEvent Event { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the agent is the master.
    /// </summary>
    public bool IsMaster { get; set; }

    /// <summary>
    /// Gets or sets the last time the agent was seen.
    /// </summary>
    public DateTimeOffset LastSeen { get; set; }
}

/// <summary>
/// Response returned by <c>GET /api/sessions</c>.
/// </summary>
public sealed class SessionsResponse : ApiResponse
{
    /// <summary>
    /// Gets or sets the current master agent identifier, if any.
    /// </summary>
    public string? MasterAgentId { get; set; }

    /// <summary>
    /// Gets or sets the active sessions.
    /// </summary>
    public IReadOnlyList<SessionInfo> Sessions { get; set; } = [];
}

/// <summary>
/// Payload emitted by the <c>GET /stream</c> Server-Sent Events endpoint.
/// </summary>
public sealed class StateChangedEvent
{
    /// <summary>
    /// Gets or sets the command sent to the hardware.
    /// </summary>
    public AgentCoreLightCommand? Command { get; set; }

    /// <summary>
    /// Gets or sets the current master agent identifier, if any.
    /// </summary>
    public string? MasterAgentId { get; set; }

    /// <summary>
    /// Gets or sets the event timestamp.
    /// </summary>
    public DateTimeOffset Timestamp { get; set; }
}
