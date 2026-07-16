using AgentTrafficLight.Server.Configuration;

namespace AgentTrafficLight.Server.Services;

/// <summary>
/// Periodically removes expired agent instances from the store and releases
/// hardware control when the controller expires.
/// </summary>
public sealed class InstanceCleanupHostedService : BackgroundService
{
    private readonly IInstanceStore _store;
    private readonly ITrafficLightController _controller;
    private readonly TimeSpan _sweepInterval;
    private readonly ILogger<InstanceCleanupHostedService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="InstanceCleanupHostedService"/> class.
    /// </summary>
    /// <param name="store">The instance store.</param>
    /// <param name="controller">The traffic-light controller.</param>
    /// <param name="options">Instance options.</param>
    /// <param name="logger">The logger.</param>
    public InstanceCleanupHostedService(
        IInstanceStore store,
        ITrafficLightController controller,
        IOptions<InstanceOptions> options,
        ILogger<InstanceCleanupHostedService> logger)
    {
        _store = store;
        _controller = controller;
        _sweepInterval = TimeSpan.FromSeconds(options.Value.SweepIntervalSeconds);
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Instance cleanup service started with sweep interval {SweepInterval}", _sweepInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_sweepInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            await SweepExpiredInstancesAsync(stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task SweepExpiredInstancesAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var controllerRemoved = _store.RemoveExpiredInstances(now);

        if (controllerRemoved)
        {
            _logger.LogInformation("Controller instance expired; releasing control and turning hardware Off");
            await _controller.SetStateAsync(TrafficLightState.Off, cancellationToken).ConfigureAwait(false);
        }
    }
}
