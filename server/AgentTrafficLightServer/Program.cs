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

        builder.Services.Configure<SerialOptions>(
            builder.Configuration.GetSection(SerialOptions.SectionName));
        builder.Services.Configure<BleOptions>(
            builder.Configuration.GetSection(BleOptions.SectionName));
        builder.Services.Configure<TrafficLightOptions>(
            builder.Configuration.GetSection(TrafficLightOptions.SectionName));

        builder.Services.AddSingleton<ISerialController, SerialController>();
        builder.Services.AddSingleton<ITrafficLightController>(static provider =>
        {
            var options = provider.GetRequiredService<IOptions<TrafficLightOptions>>().Value;
            if (options.Transport.Equals("Ble", StringComparison.OrdinalIgnoreCase))
            {
                return provider.GetRequiredService<BleTrafficLightController>();
            }

            return provider.GetRequiredService<TrafficLightController>();
        });
        builder.Services.AddSingleton<BleTrafficLightController>();
        builder.Services.AddSingleton<TrafficLightController>();
        builder.Services.AddHostedService<TestHarnessHostedService>();

        var host = builder.Build();
        await host.RunAsync().ConfigureAwait(false);
    }
}
