namespace AgentTrafficLight.Server.Tests.Fakes;

public sealed class FakeSerialController : ISerialController
{
    public bool IsConnected { get; set; } = true;

    public List<string> Commands { get; } = new();

    public Task WriteAsync(string command, CancellationToken cancellationToken = default)
    {
        Commands.Add(command);
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
