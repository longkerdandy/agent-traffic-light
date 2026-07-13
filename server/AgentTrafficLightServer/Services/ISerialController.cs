namespace AgentTrafficLight.Server.Services;

public interface ISerialController : IAsyncDisposable
{
    bool IsConnected { get; }

    Task WriteAsync(string command, CancellationToken cancellationToken = default);
}
