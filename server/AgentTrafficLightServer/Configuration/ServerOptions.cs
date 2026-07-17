namespace AgentTrafficLight.Server.Configuration;

/// <summary>
/// Options for configuring the HTTP server.
/// </summary>
public sealed class ServerOptions
{
    /// <summary>
    /// The configuration section name used in appsettings.json.
    /// </summary>
    public const string SectionName = "Server";

    /// <summary>
    /// Gets or sets the host address to bind to.
    /// </summary>
    public string Host { get; set; } = "127.0.0.1";

    /// <summary>
    /// Gets or sets the port to listen on.
    /// </summary>
    public int Port { get; set; } = 8787;
}
