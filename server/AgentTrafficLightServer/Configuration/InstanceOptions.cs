namespace AgentTrafficLight.Server.Configuration;

/// <summary>
/// Options for configuring instance heartbeat and TTL behavior.
/// </summary>
public sealed class InstanceOptions
{
    /// <summary>
    /// The configuration section name used in appsettings.json.
    /// </summary>
    public const string SectionName = "Instance";

    /// <summary>
    /// Gets or sets the interval, in seconds, at which a client should send heartbeats.
    /// </summary>
    public int HeartbeatIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// Gets or sets the time, in seconds, after which an instance is considered expired
    /// if no heartbeat or other contact is received.
    /// </summary>
    public int TtlSeconds { get; set; } = 120;

    /// <summary>
    /// Gets or sets the interval, in seconds, between TTL sweep passes.
    /// </summary>
    public int SweepIntervalSeconds { get; set; } = 5;
}
