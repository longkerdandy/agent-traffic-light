using System.Diagnostics.CodeAnalysis;
using AgentTrafficLight.Server.Configuration;
using AgentTrafficLight.Server.Services;

namespace AgentTrafficLight.Server;

/// <summary>
/// Application entry point for the Agent Traffic Light server.
/// </summary>
[ExcludeFromCodeCoverage]
public static class Program
{
    /// <summary>
    /// Configures services and starts the host.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        builder.Services.Configure<BleOptions>(
            builder.Configuration.GetSection(BleOptions.SectionName));

        builder.Services.AddSingleton<ITrafficLightController, BleTrafficLightController>();
        builder.Services.AddHostedService<TestHarnessHostedService>();

        var host = builder.Build();
        await host.RunAsync().ConfigureAwait(false);
    }
}
