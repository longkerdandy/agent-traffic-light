using System.Threading.Channels;
using AgentTrafficLight.Server.Api;

namespace AgentTrafficLight.Server.Events;

/// <summary>
/// Publishes and subscribes to hardware state changes for Server-Sent Events.
/// </summary>
public sealed class StateChangeNotifier
{
    private readonly Channel<StateChangedEvent> _channel = Channel.CreateUnbounded<StateChangedEvent>(
        new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = false
        });

    /// <summary>
    /// Publishes a state change.
    /// </summary>
    /// <param name="state">The state change event.</param>
    public void Publish(StateChangedEvent state)
    {
        _channel.Writer.TryWrite(state);
    }

    /// <summary>
    /// Reads state changes as an asynchronous stream.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An asynchronous stream of state changes.</returns>
    public IAsyncEnumerable<StateChangedEvent> ReadAllAsync(CancellationToken cancellationToken)
    {
        return _channel.Reader.ReadAllAsync(cancellationToken);
    }
}
