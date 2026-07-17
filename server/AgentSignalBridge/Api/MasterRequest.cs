namespace AgentSignalBridge.Server.Api;

/// <summary>
/// Request body for claiming or releasing master control.
/// </summary>
public sealed class MasterRequest
{
    /// <summary>
    /// Gets or sets the agent identifier.
    /// </summary>
    public string AgentId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether to claim or release master control.
    /// </summary>
    public bool Enabled { get; set; }
}
