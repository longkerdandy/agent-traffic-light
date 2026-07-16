namespace AgentTrafficLight.Contracts.Responses;

/// <summary>
/// Response returned after disconnecting an instance.
/// </summary>
public sealed class DisconnectResponse : BaseResponse
{
    /// <summary>
    /// Gets or sets the instance identifier.
    /// </summary>
    public string? InstanceId { get; set; }
}
