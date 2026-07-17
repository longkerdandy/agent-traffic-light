namespace AgentSignalBridge.Server.Configuration;

/// <summary>
/// Options for configuring the BLE connection to the AgentCore-Light hardware.
/// </summary>
public sealed class BleOptions
{
    /// <summary>
    /// The configuration section name used in appsettings.json.
    /// </summary>
    public const string SectionName = "Ble";

    /// <summary>
    /// Gets or sets a value indicating whether the BLE hardware driver is enabled.
    /// When <see langword="false"/>, a no-op driver is used so the server can run
    /// without the AgentCore-Light device attached.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the BLE device MAC address, e.g. "9C:CC:01:65:6E:72".
    /// If empty, the server will scan for <see cref="DeviceName"/>.
    /// </summary>
    public string DeviceAddress { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the BLE device name used for scanning when no address is configured.
    /// </summary>
    public string DeviceName { get; set; } = "AgentCore-Light";

    /// <summary>
    /// Gets or sets the GATT service UUID.
    /// </summary>
    public string ServiceUuid { get; set; } = "12345678-1234-5678-1234-56789abcdef0";

    /// <summary>
    /// Gets or sets the GATT characteristic UUID used to write light states.
    /// </summary>
    public string CharacteristicUuid { get; set; } = "12345678-1234-5678-1234-56789abcdef1";

    /// <summary>
    /// Gets or sets the scan timeout in milliseconds when no address is configured.
    /// </summary>
    public int ScanTimeoutMs { get; set; } = 10000;

    /// <summary>
    /// Gets or sets the interval, in milliseconds, between automatic reconnect attempts.
    /// </summary>
    public int ReconnectIntervalMs { get; set; } = 5000;
}
