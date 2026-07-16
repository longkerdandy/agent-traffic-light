using AgentTrafficLight.Contracts.Models;
using AgentTrafficLight.Server.Models;

namespace AgentTrafficLight.Server.Services;

/// <summary>
/// Thread-safe in-memory store for active agent instances.
/// </summary>
public sealed class InMemoryInstanceStore : IInstanceStore
{
    private readonly Dictionary<string, AgentInstance> _instances = new();
    private string? _controllerInstanceId;
    private readonly object _lock = new();

    /// <inheritdoc />
    public AgentInstance Upsert(string instanceId, string agent, string? cwd, DateTimeOffset now, TimeSpan ttl)
    {
        lock (_lock)
        {
            if (!_instances.TryGetValue(instanceId, out var instance))
            {
                instance = new AgentInstance
                {
                    InstanceId = instanceId,
                    Agent = agent,
                    Cwd = cwd,
                    State = TrafficLightState.Off,
                    LastSeen = now,
                    ExpiresAt = now + ttl
                };
                _instances[instanceId] = instance;
                return instance;
            }

            instance.Agent = agent;
            instance.Cwd = cwd;
            instance.LastSeen = now;
            instance.ExpiresAt = now + ttl;
            return instance;
        }
    }

    /// <inheritdoc />
    public AgentInstance Touch(string instanceId, string agent, string? cwd, DateTimeOffset now, TimeSpan ttl)
    {
        lock (_lock)
        {
            if (!_instances.TryGetValue(instanceId, out var instance) || instance.ExpiresAt <= now)
            {
                instance = new AgentInstance
                {
                    InstanceId = instanceId,
                    Agent = agent,
                    Cwd = cwd,
                    State = TrafficLightState.Off,
                    LastSeen = now,
                    ExpiresAt = now + ttl
                };
                _instances[instanceId] = instance;
                return instance;
            }

            instance.Agent = agent;
            instance.Cwd = cwd;
            instance.LastSeen = now;
            instance.ExpiresAt = now + ttl;
            return instance;
        }
    }

    /// <inheritdoc />
    public AgentInstance SetState(string instanceId, string agent, string? cwd, TrafficLightState state, DateTimeOffset now, TimeSpan ttl)
    {
        lock (_lock)
        {
            if (!_instances.TryGetValue(instanceId, out var instance) || instance.ExpiresAt <= now)
            {
                instance = new AgentInstance
                {
                    InstanceId = instanceId,
                    Agent = agent,
                    Cwd = cwd,
                    State = state,
                    LastSeen = now,
                    ExpiresAt = now + ttl
                };
                _instances[instanceId] = instance;
                return instance;
            }

            instance.Agent = agent;
            instance.Cwd = cwd;
            instance.State = state;
            instance.LastSeen = now;
            instance.ExpiresAt = now + ttl;
            return instance;
        }
    }

    /// <inheritdoc />
    public bool TryGet(string instanceId, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out AgentInstance? instance)
    {
        lock (_lock)
        {
            if (_instances.TryGetValue(instanceId, out instance) && instance.ExpiresAt > DateTimeOffset.UtcNow)
            {
                return true;
            }

            instance = null;
            return false;
        }
    }

    /// <inheritdoc />
    public bool TryRemove(string instanceId, out AgentInstance? instance)
    {
        lock (_lock)
        {
            if (!_instances.Remove(instanceId, out instance))
            {
                instance = null;
                return false;
            }

            if (_controllerInstanceId == instanceId)
            {
                _controllerInstanceId = null;
                instance.IsController = false;
            }

            return true;
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<AgentInstance> GetSnapshot()
    {
        lock (_lock)
        {
            var now = DateTimeOffset.UtcNow;
            return _instances.Values.Where(i => i.ExpiresAt > now).ToList();
        }
    }

    /// <inheritdoc />
    public bool TrySetController(string instanceId, out string? conflictInstanceId)
    {
        conflictInstanceId = null;

        lock (_lock)
        {
            if (!_instances.TryGetValue(instanceId, out _))
            {
                return false;
            }

            if (_controllerInstanceId == instanceId)
            {
                return true;
            }

            if (_controllerInstanceId != null)
            {
                conflictInstanceId = _controllerInstanceId;
                return false;
            }

            _controllerInstanceId = instanceId;
            _instances[instanceId].IsController = true;
            return true;
        }
    }

    /// <inheritdoc />
    public bool TryReleaseController(string instanceId)
    {
        lock (_lock)
        {
            if (_controllerInstanceId != instanceId)
            {
                return false;
            }

            _controllerInstanceId = null;
            if (_instances.TryGetValue(instanceId, out var instance))
            {
                instance.IsController = false;
            }

            return true;
        }
    }

    /// <inheritdoc />
    public string? GetControllerInstanceId()
    {
        lock (_lock)
        {
            return _controllerInstanceId;
        }
    }

    /// <inheritdoc />
    public bool RemoveExpiredInstances(DateTimeOffset now)
    {
        lock (_lock)
        {
            var expiredKeys = _instances
                .Where(pair => pair.Value.ExpiresAt <= now)
                .Select(pair => pair.Key)
                .ToList();

            var controllerRemoved = false;
            foreach (var key in expiredKeys)
            {
                if (_instances.Remove(key, out var instance) && instance.IsController)
                {
                    controllerRemoved = true;
                }
            }

            if (_controllerInstanceId != null && !_instances.ContainsKey(_controllerInstanceId))
            {
                _controllerInstanceId = null;
                controllerRemoved = true;
            }

            return controllerRemoved;
        }
    }
}
