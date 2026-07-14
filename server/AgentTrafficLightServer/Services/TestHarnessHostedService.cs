using AgentTrafficLight.Server.Models;

namespace AgentTrafficLight.Server.Services;

/// <summary>
/// Interactive command-line hosted service for manually testing traffic-light states.
/// </summary>
public sealed class TestHarnessHostedService : BackgroundService
{
    private static readonly TrafficLightState[] s_demoStates =
    {
        TrafficLightState.Idle,
        TrafficLightState.Thinking,
        TrafficLightState.Busy,
        TrafficLightState.Success,
        TrafficLightState.Error,
        TrafficLightState.Off
    };

    private readonly ITrafficLightController _controller;
    private readonly IHostApplicationLifetime _lifetime;

    /// <summary>
    /// Initializes a new instance of the <see cref="TestHarnessHostedService"/> class.
    /// </summary>
    /// <param name="controller">The traffic-light controller.</param>
    /// <param name="lifetime">The host application lifetime.</param>
    public TestHarnessHostedService(
        ITrafficLightController controller,
        IHostApplicationLifetime lifetime)
    {
        _controller = controller;
        _lifetime = lifetime;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Console.WriteLine();
        Console.WriteLine("Agent Traffic Light Test Harness");
        Console.WriteLine("Commands: idle thinking ai busy wait_confirm success error off demo quit");

        if (Console.IsInputRedirected)
        {
            await RunRedirectedLoopAsync(stoppingToken).ConfigureAwait(false);
        }
        else
        {
            await RunInteractiveLoopAsync(stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task RunInteractiveLoopAsync(CancellationToken stoppingToken)
    {
        Console.Write("\u003e ");
        while (!stoppingToken.IsCancellationRequested)
        {
            if (!Console.KeyAvailable)
            {
                await Task.Delay(100, stoppingToken).ConfigureAwait(false);
                continue;
            }

            var line = Console.ReadLine();
            if (line == null)
            {
                return;
            }

            if (!await ProcessCommandAsync(line, interactive: true, stoppingToken).ConfigureAwait(false))
            {
                return;
            }
        }
    }

    private async Task RunRedirectedLoopAsync(CancellationToken stoppingToken)
    {
        string? line;
        while ((line = await Console.In.ReadLineAsync(stoppingToken).ConfigureAwait(false)) != null)
        {
            if (!await ProcessCommandAsync(line, interactive: false, stoppingToken).ConfigureAwait(false))
            {
                return;
            }
        }
    }

    private async Task<bool> ProcessCommandAsync(string line, bool interactive, CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            if (interactive)
            {
                Console.Write("\u003e ");
            }

            return true;
        }

        if (line.Equals("quit", StringComparison.OrdinalIgnoreCase))
        {
            _lifetime.StopApplication();
            return false;
        }

        if (line.Equals("demo", StringComparison.OrdinalIgnoreCase))
        {
            await RunDemoAsync(interactive, stoppingToken).ConfigureAwait(false);
            if (interactive)
            {
                Console.Write("\u003e ");
            }

            return true;
        }

        if (TryParseState(line, out var state))
        {
            await _controller.SetStateAsync(state, stoppingToken).ConfigureAwait(false);
        }
        else
        {
            Console.WriteLine($"Unknown command: {line}");
        }

        if (interactive)
        {
            Console.Write("\u003e ");
        }

        return true;
    }

    private async Task RunDemoAsync(bool interactive, CancellationToken stoppingToken)
    {
        if (!interactive)
        {
            foreach (var state in s_demoStates)
            {
                await _controller.SetStateAsync(state, stoppingToken).ConfigureAwait(false);
                await Task.Delay(2000, stoppingToken).ConfigureAwait(false);
            }

            return;
        }

        Console.WriteLine("Demo running; press any key to stop...");
        while (!stoppingToken.IsCancellationRequested)
        {
            foreach (var state in s_demoStates)
            {
                if (stoppingToken.IsCancellationRequested)
                {
                    return;
                }

                if (Console.KeyAvailable)
                {
                    _ = Console.ReadKey(true);
                    return;
                }

                await _controller.SetStateAsync(state, stoppingToken).ConfigureAwait(false);
                await Task.Delay(2000, stoppingToken).ConfigureAwait(false);
            }
        }
    }

    private static bool TryParseState(string text, out TrafficLightState state)
    {
        var normalized = text.Trim().ToLowerInvariant();
        state = normalized switch
        {
            "idle" => TrafficLightState.Idle,
            "thinking" => TrafficLightState.Thinking,
            "ai" => TrafficLightState.Ai,
            "busy" => TrafficLightState.Busy,
            "wait_confirm" => TrafficLightState.WaitConfirm,
            "success" => TrafficLightState.Success,
            "error" => TrafficLightState.Error,
            "off" => TrafficLightState.Off,
            _ => TrafficLightState.Off
        };

        return normalized is "idle" or "thinking" or "ai" or "busy" or "wait_confirm" or "success" or "error" or "off";
    }
}
