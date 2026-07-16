namespace AgentTrafficLight.Contracts.Responses;

/// <summary>
/// Response returned after a heartbeat.
/// </summary>
public sealed class HeartbeatResponse : BaseResponse
{
    /// <summary>
    /// Gets or sets the instance identifier.
    /// </summary>
    public string? InstanceId { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the instance expires if not refreshed.
    /// </summary>
    public DateTimeOffset ExpiresAt { get; set; }
}
