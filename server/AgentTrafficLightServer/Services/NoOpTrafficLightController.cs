namespace AgentTrafficLight.Server.Services;

/// <summary>
/// No-op traffic-light controller used on platforms where BLE hardware access is unavailable.
/// </summary>
public sealed class NoOpTrafficLightController : ITrafficLightController
{
    private readonly ILogger<NoOpTrafficLightController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="NoOpTrafficLightController"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public NoOpTrafficLightController(ILogger<NoOpTrafficLightController> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public TrafficLightState CurrentState { get; private set; } = TrafficLightState.Off;

    /// <inheritdoc />
    public bool IsConnected => false;

    /// <inheritdoc />
    public Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("BLE hardware is not available on this platform");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SetStateAsync(TrafficLightState state, CancellationToken cancellationToken = default)
    {
        CurrentState = state;
        _logger.LogInformation("Traffic light state would be {State} (no hardware on this platform)", state);
        return Task.CompletedTask;
    }
}
