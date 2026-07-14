using AgentTrafficLight.Server.Models;

namespace AgentTrafficLight.Server.Services;

/// <summary>
/// Controls the AgentCore-Light traffic-light hardware state.
/// </summary>
public interface ITrafficLightController
{
    /// <summary>
    /// Gets the current traffic-light state.
    /// </summary>
    TrafficLightState CurrentState { get; }

    /// <summary>
    /// Sets the traffic-light state and writes the corresponding serial command.
    /// </summary>
    /// <param name="state">The desired state.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SetStateAsync(TrafficLightState state, CancellationToken cancellationToken = default);
}

/// <summary>
/// Default implementation of <see cref="ITrafficLightController"/> that writes
/// deduplicated serial commands through an <see cref="ISerialController"/>.
/// </summary>
public sealed class TrafficLightController : ITrafficLightController
{
    private readonly ISerialController _serialController;
    private readonly ILogger<TrafficLightController> _logger;

    /// <inheritdoc />
    public TrafficLightState CurrentState { get; private set; } = TrafficLightState.Off;

    /// <summary>
    /// Initializes a new instance of the <see cref="TrafficLightController"/> class.
    /// </summary>
    /// <param name="serialController">The serial controller used to send commands.</param>
    /// <param name="logger">The logger.</param>
    public TrafficLightController(ISerialController serialController, ILogger<TrafficLightController> logger)
    {
        _serialController = serialController;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task SetStateAsync(TrafficLightState state, CancellationToken cancellationToken = default)
    {
        if (state == CurrentState)
        {
            return;
        }

        var command = state.ToSerialCommand();
        await _serialController.WriteAsync(command, cancellationToken).ConfigureAwait(false);
        CurrentState = state;
        _logger.LogInformation("Traffic light state changed to {State} ({Command})", state, command);
    }
}
