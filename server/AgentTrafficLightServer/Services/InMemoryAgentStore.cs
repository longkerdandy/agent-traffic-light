using System.Diagnostics.CodeAnalysis;
using AgentTrafficLight.Server.Models;

namespace AgentTrafficLight.Server.Services;

/// <summary>
/// Thread-safe in-memory store for active agent sessions.
/// Each agent is associated with a one-shot TTL timer that removes the agent when it fires.
/// </summary>
public sealed class InMemoryAgentStore : IAgentStore, IDisposable
{
    private readonly Dictionary<string, Agent> _agents = [];
    private readonly Dictionary<string, Timer> _timers = [];
    private readonly Lock _lock = new();
    private readonly TimeSpan _ttl;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryAgentStore"/> class
    /// with the default TTL of 120 seconds.
    /// </summary>
    public InMemoryAgentStore()
        : this(TimeSpan.FromSeconds(120))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryAgentStore"/> class.
    /// </summary>
    /// <param name="ttl">The time-to-live for each agent session.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="ttl"/> is less than or equal to zero.
    /// </exception>
    public InMemoryAgentStore(TimeSpan ttl)
    {
        if (ttl <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(ttl), ttl, "TTL must be greater than zero.");
        }

        _ttl = ttl;
    }

    /// <inheritdoc />
    public Agent Upsert(string agentId, string agentName, string? cwd, AgentState state, DateTimeOffset now)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_lock)
        {
            var agent = GetOrCreateAgent(agentId, agentName, cwd, now);
            agent.State = state;
            RescheduleTimer(agentId);
            return agent;
        }
    }

    /// <inheritdoc />
    public Agent Touch(string agentId, string agentName, string? cwd, DateTimeOffset now)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_lock)
        {
            var agent = GetOrCreateAgent(agentId, agentName, cwd, now);
            RescheduleTimer(agentId);
            return agent;
        }
    }

    /// <inheritdoc />
    public bool TryGet(string agentId, [NotNullWhen(true)] out Agent? agent)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_lock)
        {
            if (_agents.TryGetValue(agentId, out agent))
            {
                return true;
            }

            agent = null;
            return false;
        }
    }

    /// <inheritdoc />
    public bool TryRemove(string agentId, out Agent? agent)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_lock)
        {
            if (!_agents.Remove(agentId, out agent))
            {
                return false;
            }

            if (agent != null)
            {
                agent.IsController = false;
            }

            if (_timers.Remove(agentId, out var timer))
            {
                timer.Dispose();
            }

            return true;
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<Agent> GetSnapshot()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_lock)
        {
            return [.. _agents.Values];
        }
    }

    /// <inheritdoc />
    public bool TrySetController(string agentId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_lock)
        {
            if (!_agents.TryGetValue(agentId, out var agent))
            {
                return false;
            }

            if (agent.IsController)
            {
                return true;
            }

            if (_agents.Values.Any(a => a.IsController))
            {
                return false;
            }

            agent.IsController = true;
            return true;
        }
    }

    /// <inheritdoc />
    public bool TryReleaseController(string agentId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_lock)
        {
            if (!_agents.TryGetValue(agentId, out var agent) || !agent.IsController)
            {
                return false;
            }

            agent.IsController = false;
            return true;
        }
    }

    /// <inheritdoc />
    public string? GetControllerAgentId()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_lock)
        {
            return _agents.Values.FirstOrDefault(a => a.IsController)?.AgentId;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            foreach (var timer in _timers.Values)
            {
                timer.Dispose();
            }

            _timers.Clear();
            _agents.Clear();
        }
    }

    private Agent GetOrCreateAgent(string agentId, string agentName, string? cwd, DateTimeOffset now)
    {
        if (!_agents.TryGetValue(agentId, out var agent))
        {
            agent = new Agent
            {
                AgentId = agentId,
                AgentName = agentName,
                Cwd = cwd,
                State = AgentState.Off,
                LastSeen = now,
                IsController = false
            };
            _agents[agentId] = agent;
        }
        else
        {
            agent.AgentName = agentName;
            agent.Cwd = cwd;
            agent.LastSeen = now;
        }

        return agent;
    }

    private void RescheduleTimer(string agentId)
    {
        if (_timers.Remove(agentId, out var existingTimer))
        {
            existingTimer.Dispose();
        }

        var timer = new Timer(
            _ => OnTimerElapsed(agentId),
            null,
            _ttl,
            Timeout.InfiniteTimeSpan);

        _timers[agentId] = timer;
    }

    private void OnTimerElapsed(string agentId)
    {
        Timer? timer;

        lock (_lock)
        {
            if (_disposed || !_timers.Remove(agentId, out timer))
            {
                return;
            }

            if (_agents.Remove(agentId, out var agent) && agent != null)
            {
                agent.IsController = false;
            }
        }

        timer.Dispose();
    }
}
