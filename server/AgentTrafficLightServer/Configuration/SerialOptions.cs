namespace AgentTrafficLight.Server.Configuration;

/// <summary>
/// Options for configuring the serial connection to the AgentCore-Light hardware.
/// </summary>
public sealed class SerialOptions
{
    /// <summary>
    /// The configuration section name used in appsettings.json.
    /// </summary>
    public const string SectionName = "Serial";

    /// <summary>
    /// Gets or sets the serial port name. Use "auto" to detect the ESP32 port automatically.
    /// </summary>
    public string Port { get; set; } = "auto";

    /// <summary>
    /// Gets or sets the serial baud rate. The AgentCore-Light firmware uses 115200.
    /// </summary>
    public int BaudRate { get; set; } = 115200;

    /// <summary>
    /// Gets or sets a value indicating whether the Data Terminal Ready signal is enabled.
    /// </summary>
    public bool DtrEnable { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the Request To Send signal is enabled.
    /// </summary>
    public bool RtsEnable { get; set; }

    /// <summary>
    /// Gets or sets the interval, in milliseconds, between automatic reconnect attempts.
    /// </summary>
    public int ReconnectIntervalMs { get; set; } = 2000;
}
