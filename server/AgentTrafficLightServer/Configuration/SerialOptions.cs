namespace AgentTrafficLight.Server.Configuration;

public sealed class SerialOptions
{
    public const string SectionName = "Serial";

    public string Port { get; set; } = "auto";

    public int BaudRate { get; set; } = 115200;

    public bool DtrEnable { get; set; }

    public bool RtsEnable { get; set; }

    public int ReconnectIntervalMs { get; set; } = 2000;
}
