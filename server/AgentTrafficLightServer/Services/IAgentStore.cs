using AgentTrafficLight.Server.Models;

namespace AgentTrafficLight.Server.Services;

/// <summary>
/// Stores active agent sessions and tracks which agent holds exclusive control of the traffic light.
/// </summary>
public interface IAgentStore
{
    /// <summary>
    /// Creates or updates an agent session and sets its requested state.
    /// </summary>
    /// <param name="agentId">The agent identifier.</param>
    /// <param name="agentName">The agent name, for example "kimi", "claude", or "codex".</param>
    /// <param name="cwd">The working directory, if any.</param>
    /// <param name="state">The requested state.</param>
    /// <param name="now">The current time.</param>
    /// <returns>The created or updated agent.</returns>
    Agent Upsert(string agentId, string agentName, string? cwd, TrafficLightState state, DateTimeOffset now);

    /// <summary>
    /// Attempts to retrieve an active agent.
    /// </summary>
    /// <param name="agentId">The agent identifier.</param>
    /// <param name="agent">The agent, if found and not expired.</param>
    /// <returns><see langword="true"/> if the agent is active.</returns>
    bool TryGet(string agentId, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out Agent? agent);

    /// <summary>
    /// Removes an agent immediately.
    /// </summary>
    /// <param name="agentId">The agent identifier.</param>
    /// <param name="agent">The removed agent, if any.</param>
    /// <returns><see langword="true"/> if the agent was removed.</returns>
    bool TryRemove(string agentId, out Agent? agent);

    /// <summary>
    /// Gets a snapshot of all active agents.
    /// </summary>
    /// <returns>A read-only snapshot of active agents.</returns>
    IReadOnlyList<Agent> GetSnapshot();

    /// <summary>
    /// Attempts to make an agent the exclusive controller of the traffic light.
    /// </summary>
    /// <param name="agentId">The agent identifier.</param>
    /// <param name="conflictAgentId">The identifier of the agent that already holds control, if any.</param>
    /// <returns><see langword="true"/> if this agent is now the controller.</returns>
    bool TrySetController(string agentId, out string? conflictAgentId);

    /// <summary>
    /// Releases exclusive control if the specified agent is the controller.
    /// </summary>
    /// <param name="agentId">The agent identifier.</param>
    /// <returns><see langword="true"/> if control was released.</returns>
    bool TryReleaseController(string agentId);

    /// <summary>
    /// Gets the identifier of the current controller, if any.
    /// </summary>
    /// <returns>The controller agent identifier, or <see langword="null"/>.</returns>
    string? GetControllerAgentId();
}
