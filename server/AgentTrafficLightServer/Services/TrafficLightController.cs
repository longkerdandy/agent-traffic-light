using AgentTrafficLight.Server.Models;

namespace AgentTrafficLight.Server.Services;

public interface ITrafficLightController
{
    TrafficLightState CurrentState { get; }

    Task SetStateAsync(TrafficLightState state, CancellationToken cancellationToken = default);
}

public sealed class TrafficLightController : ITrafficLightController
{
    private readonly ISerialController _serialController;
    private readonly ILogger<TrafficLightController> _logger;

    public TrafficLightState CurrentState { get; private set; } = TrafficLightState.Off;

    public TrafficLightController(ISerialController serialController, ILogger<TrafficLightController> logger)
    {
        _serialController = serialController;
        _logger = logger;
    }

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
