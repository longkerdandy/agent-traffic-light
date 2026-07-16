using AgentTrafficLight.Server.Drivers;
using AgentTrafficLight.Server.Events;
using AgentTrafficLight.Server.Models;
using AgentTrafficLight.Server.Stores;

namespace AgentTrafficLight.Server.Hardware;

/// <summary>
/// Hardware manager for the AgentCore-Light traffic light.
/// Subscribes to agent lifecycle events and maps them to traffic-light commands
/// sent through the bound BLE driver, but only for the current master agent.
/// </summary>
public sealed class AgentCoreLightManager : IAgentEventSubscriber
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

        if (!_driver.IsConnected)
        {
            await _driver.ConnectAsync(cancellationToken).ConfigureAwait(false);
        }

        await _driver.SendCommandAsync(command, cancellationToken).ConfigureAwait(false);
    }

    private static TrafficLightCommand MapEventToCommand(AgentEvent agentEvent) => agentEvent switch
    {
        AgentEvent.SessionStart => TrafficLightCommand.Idle,
        AgentEvent.UserPromptSubmit => TrafficLightCommand.Thinking,
        AgentEvent.PreToolUse => TrafficLightCommand.Thinking,
        AgentEvent.PostToolUse => TrafficLightCommand.Thinking,
        AgentEvent.PermissionRequest => TrafficLightCommand.WaitConfirm,
        AgentEvent.Stop => TrafficLightCommand.Success,
        AgentEvent.StopFailure => TrafficLightCommand.Error,
        AgentEvent.Disconnect => TrafficLightCommand.Off,
        AgentEvent.SessionEnd => TrafficLightCommand.Off,
        _ => TrafficLightCommand.Off
    };
}
