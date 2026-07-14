namespace AgentTrafficLight.Server.Configuration;

/// <summary>
/// Top-level options for selecting and configuring the traffic-light control transport.
/// </summary>
public sealed class TrafficLightOptions
{
    /// <summary>
    /// The configuration section name used in appsettings.json.
    /// </summary>
    public const string SectionName = "TrafficLight";

    /// <summary>
    /// Gets or sets the transport type. Supported values are "Serial" and "Ble".
    /// </summary>
    public string Transport { get; set; } = "Serial";
}
