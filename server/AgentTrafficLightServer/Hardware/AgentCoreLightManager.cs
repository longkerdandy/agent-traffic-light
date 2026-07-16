using AgentTrafficLight.Server.Drivers;
using AgentTrafficLight.Server.Events;
using AgentTrafficLight.Server.Models;
using AgentTrafficLight.Server.Stores;

namespace AgentTrafficLight.Server.Hardware;

/// <summary>
/// Hardware manager for the AgentCoreLight device.
/// Connects to the device when the host starts, subscribes to agent lifecycle events,
/// and maps them to AgentCoreLight commands sent through the bound driver, but only
/// for the current master agent.
/// </summary>
public sealed class AgentCoreLightManager : IAgentEventSubscriber, IHostedService
{
    private readonly IAgentStore _store;
    private readonly IAgentCoreLightDriver _driver;
    private readonly ILogger<AgentCoreLightManager> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentCoreLightManager"/> class.
    /// </summary>
    /// <param name="store">The agent session store.</param>
    /// <param name="driver">The bound driver for AgentCore-Light hardware.</param>
    /// <param name="logger">The logger.</param>
    public AgentCoreLightManager(IAgentStore store, IAgentCoreLightDriver driver, ILogger<AgentCoreLightManager> logger)
    {
        _store = store;
        _driver = driver;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _driver.ConnectAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("AgentCore-Light connected at startup");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AgentCore-Light could not be connected at startup; will retry on next command");
        }
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _driver.DisconnectAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AgentCore-Light disconnect failed during shutdown");
        }
    }

    /// <inheritdoc />
    public async Task OnAgentEventAsync(string agentId, AgentEvent agentEvent, CancellationToken cancellationToken = default)
    {
        var masterAgentId = _store.GetMasterAgentId();
        if (masterAgentId != agentId)
        {
            _logger.LogDebug(
                "Agent {AgentId} is not the master ({MasterAgentId}); ignoring {AgentEvent}",
                agentId,
                masterAgentId,
                agentEvent);
            return;
        }

        var command = MapEventToCommand(agentEvent);

        _logger.LogDebug(
            "AgentCore-Light mapped {AgentEvent} to {Command}",
            agentEvent,
            command);

        await _driver.SendCommandAsync(command, cancellationToken).ConfigureAwait(false);
    }

    private static AgentCoreLightCommand MapEventToCommand(AgentEvent agentEvent) => agentEvent switch
    {
        AgentEvent.SessionStart => AgentCoreLightCommand.Idle,
        AgentEvent.UserPromptSubmit => AgentCoreLightCommand.Thinking,
        AgentEvent.PreToolUse => AgentCoreLightCommand.Busy,
        AgentEvent.PostToolUse => AgentCoreLightCommand.Ai,
        AgentEvent.PermissionRequest => AgentCoreLightCommand.WaitConfirm,
        AgentEvent.Stop => AgentCoreLightCommand.Success,
        AgentEvent.StopFailure => AgentCoreLightCommand.Error,
        AgentEvent.Disconnect => AgentCoreLightCommand.Off,
        AgentEvent.SessionEnd => AgentCoreLightCommand.Off,
        _ => AgentCoreLightCommand.Off
    };
}
