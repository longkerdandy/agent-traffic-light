namespace AgentTrafficLight.Contracts.Requests;

/// <summary>
/// Request body for claiming or releasing exclusive hardware control.
/// </summary>
public sealed class ControlRequest
{
    /// <summary>
    /// Gets or sets a value indicating whether the instance should claim control.
    /// </summary>
    public bool Enabled { get; set; }
}
