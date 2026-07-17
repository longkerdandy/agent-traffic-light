namespace AgentSignalBridge.Server.Configuration;

/// <summary>
/// Options for configuring agent session behavior.
/// </summary>
public sealed class AgentOptions
{
    /// <summary>
    /// The configuration section name used in appsettings.json.
    /// </summary>
    public const string SectionName = "Agent";

    /// <summary>
    /// Gets or sets the expected interval, in seconds, between client heartbeats.
    /// </summary>
    public int HeartbeatIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// Gets or sets the session time-to-live, in seconds, without a heartbeat or event.
    /// </summary>
    public int TtlSeconds { get; set; } = 120;
}
