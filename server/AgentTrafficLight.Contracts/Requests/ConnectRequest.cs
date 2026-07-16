namespace AgentTrafficLight.Contracts.Requests;

/// <summary>
/// Request body for registering an agent instance as online.
/// </summary>
public sealed class ConnectRequest
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
