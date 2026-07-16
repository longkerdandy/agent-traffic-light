namespace AgentTrafficLight.Contracts.Requests;

/// <summary>
/// Request body for keeping an instance alive without changing its state.
/// </summary>
public sealed class HeartbeatRequest
{
    /// <summary>
    /// Gets or sets the agent kind (e.g., "kimi", "claude").
    /// </summary>
    public string Agent { get; set; } = "unknown";

    /// <summary>
    /// Gets or sets the working directory of the agent process.
    /// </summary>
    public string? Cwd { get; set; }
}
