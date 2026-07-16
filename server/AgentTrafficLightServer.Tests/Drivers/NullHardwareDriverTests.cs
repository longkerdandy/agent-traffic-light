using AgentTrafficLight.Server.Drivers;

namespace AgentTrafficLight.Server.Tests.Drivers;

public sealed class NullHardwareDriverTests : IDisposable
{
    private readonly NullHardwareDriver _driver = new();

    public void Dispose()
    {
        _driver.DisposeAsync().AsTask().Wait();
    }

    [Fact]
    public async Task ConnectAsync_SetsIsConnected()
    {
        await _driver.ConnectAsync();

        Assert.True(_driver.IsConnected);
    }

    [Fact]
    public async Task DisconnectAsync_ClearsIsConnected()
    {
        await _driver.ConnectAsync();
        await _driver.DisconnectAsync();

        Assert.False(_driver.IsConnected);
    }

    [Fact]
    public async Task SendCommandAsync_RecordsCurrentCommand()
    {
        await _driver.SendCommandAsync(TrafficLightCommand.Busy, CancellationToken.None);

        Assert.Equal(TrafficLightCommand.Busy, _driver.CurrentCommand);
    }

    [Fact]
    public async Task DisposeAsync_ClearsIsConnected()
    {
        await _driver.ConnectAsync();
        await _driver.DisposeAsync();

        Assert.False(_driver.IsConnected);
    }
}
