namespace AgentTrafficLight.Contracts.Models;

/// <summary>
/// Canonical agent lifecycle events consumed by the server.
/// </summary>
public enum AgentEvent
{
    /// <summary>
    /// The agent disconnected from the server.
    /// </summary>
    Disconnect,

    /// <summary>
    /// A new agent session started.
    /// </summary>
    SessionStart,

    /// <summary>
    /// The user submitted a prompt.
    /// </summary>
    UserPromptSubmit,

    /// <summary>
    /// A tool is about to be used.
    /// </summary>
    PreToolUse,

    /// <summary>
    /// A tool has finished executing.
    /// </summary>
    PostToolUse,

    /// <summary>
    /// The agent is waiting for user permission.
    /// </summary>
    PermissionRequest,

    /// <summary>
    /// The agent stopped normally.
    /// </summary>
    Stop,

    /// <summary>
    /// The agent failed to stop.
    /// </summary>
    StopFailure,

    /// <summary>
    /// The agent session ended.
    /// </summary>
    SessionEnd
}
