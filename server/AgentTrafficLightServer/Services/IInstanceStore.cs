using AgentTrafficLight.Contracts.Models;
using AgentTrafficLight.Server.Models;

namespace AgentTrafficLight.Server.Services;

/// <summary>
/// Stores active agent instances and tracks which instance holds exclusive control.
/// </summary>
public interface IInstanceStore
{
    /// <summary>
    /// Creates or refreshes an instance. The state is set to <see cref="TrafficLightState.Off"/>
    /// when the instance is first created; otherwise the existing state is preserved.
    /// </summary>
    /// <param name="instanceId">The instance identifier.</param>
    /// <param name="agent">The agent kind.</param>
    /// <param name="cwd">The working directory, if any.</param>
    /// <param name="now">The current time.</param>
    /// <param name="ttl">The time-to-live.</param>
    /// <returns>The upserted instance.</returns>
    AgentInstance Upsert(string instanceId, string agent, string? cwd, DateTimeOffset now, TimeSpan ttl);

    /// <summary>
    /// Refreshes an instance's expiration timestamp without changing its state.
    /// Creates the instance if it does not already exist.
    /// </summary>
    /// <param name="instanceId">The instance identifier.</param>
    /// <param name="agent">The agent kind.</param>
    /// <param name="cwd">The working directory, if any.</param>
    /// <param name="now">The current time.</param>
    /// <param name="ttl">The time-to-live.</param>
    /// <returns>The upserted instance.</returns>
    AgentInstance Touch(string instanceId, string agent, string? cwd, DateTimeOffset now, TimeSpan ttl);

    /// <summary>
    /// Updates an instance's requested state and refreshes its expiration.
    /// Creates the instance if it does not already exist.
    /// </summary>
    /// <param name="instanceId">The instance identifier.</param>
    /// <param name="agent">The agent kind.</param>
    /// <param name="cwd">The working directory, if any.</param>
    /// <param name="state">The requested state.</param>
    /// <param name="now">The current time.</param>
    /// <param name="ttl">The time-to-live.</param>
    /// <returns>The updated instance.</returns>
    AgentInstance SetState(string instanceId, string agent, string? cwd, TrafficLightState state, DateTimeOffset now, TimeSpan ttl);

    /// <summary>
    /// Attempts to retrieve an active instance.
    /// </summary>
    /// <param name="instanceId">The instance identifier.</param>
    /// <param name="instance">The instance, if found.</param>
    /// <returns><see langword="true"/> if the instance is active.</returns>
    bool TryGet(string instanceId, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out AgentInstance? instance);

    /// <summary>
    /// Removes an instance immediately.
    /// </summary>
    /// <param name="instanceId">The instance identifier.</param>
    /// <param name="instance">The removed instance, if any.</param>
    /// <returns><see langword="true"/> if the instance was removed.</returns>
    bool TryRemove(string instanceId, out AgentInstance? instance);

    /// <summary>
    /// Gets a snapshot of all active instances.
    /// </summary>
    /// <returns>A read-only snapshot of active instances.</returns>
    IReadOnlyList<AgentInstance> GetSnapshot();

    /// <summary>
    /// Attempts to make an instance the exclusive controller.
    /// </summary>
    /// <param name="instanceId">The instance identifier.</param>
    /// <param name="conflictInstanceId">The identifier of the instance that already holds control, if any.</param>
    /// <returns><see langword="true"/> if this instance is now the controller.</returns>
    bool TrySetController(string instanceId, out string? conflictInstanceId);

    /// <summary>
    /// Releases exclusive control if the specified instance is the controller.
    /// </summary>
    /// <param name="instanceId">The instance identifier.</param>
    /// <returns><see langword="true"/> if control was released.</returns>
    bool TryReleaseController(string instanceId);

    /// <summary>
    /// Gets the identifier of the current controller, if any.
    /// </summary>
    /// <returns>The controller instance identifier, or <see langword="null"/>.</returns>
    string? GetControllerInstanceId();

    /// <summary>
    /// Removes all instances whose expiration timestamp has passed.
    /// </summary>
    /// <param name="now">The current time.</param>
    /// <returns><see langword="true"/> if the controller instance was among those removed.</returns>
    bool RemoveExpiredInstances(DateTimeOffset now);
}
