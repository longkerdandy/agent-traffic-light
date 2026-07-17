using AgentSignalBridge.Server.Models;

namespace AgentSignalBridge.Server.Stores;

/// <summary>
/// Stores active agent sessions and tracks which agent is currently the master.
/// </summary>
public interface IAgentStore
{
    /// <summary>
    /// Creates or updates an agent session and records its latest event.
    /// </summary>
    /// <param name="agentId">The agent identifier.</param>
    /// <param name="agentName">The agent name, for example "kimi", "claude", or "codex".</param>
    /// <param name="cwd">The working directory, if any.</param>
    /// <param name="agentEvent">The latest agent lifecycle event.</param>
    /// <param name="now">The current time.</param>
    /// <returns>The created or updated agent.</returns>
    Agent Upsert(string agentId, string agentName, string? cwd, AgentEvent agentEvent, DateTimeOffset now);

    /// <summary>
    /// Refreshes an agent session without changing its latest event.
    /// Creates the agent with the default <see cref="AgentEvent.Disconnect"/> event if it does not exist.
    /// </summary>
    /// <param name="agentId">The agent identifier.</param>
    /// <param name="agentName">The agent name, for example "kimi", "claude", or "codex".</param>
    /// <param name="cwd">The working directory, if any.</param>
    /// <param name="now">The current time.</param>
    /// <returns>The refreshed or created agent.</returns>
    Agent Touch(string agentId, string agentName, string? cwd, DateTimeOffset now);

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
    /// Attempts to make an agent the master.
    /// Returns <see langword="false"/> if the agent does not exist or if another agent is already the master.
    /// </summary>
    /// <param name="agentId">The agent identifier.</param>
    /// <returns><see langword="true"/> if this agent is now the master.</returns>
    bool TrySetMaster(string agentId);

    /// <summary>
    /// Releases the master role if the specified agent is the master.
    /// </summary>
    /// <param name="agentId">The agent identifier.</param>
    /// <returns><see langword="true"/> if the master role was released.</returns>
    bool TryReleaseMaster(string agentId);

    /// <summary>
    /// Gets the identifier of the current master, if any.
    /// </summary>
    /// <returns>The master agent identifier, or <see langword="null"/>.</returns>
    string? GetMasterAgentId();
}
