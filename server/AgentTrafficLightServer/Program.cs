using System.Diagnostics.CodeAnalysis;
using AgentTrafficLight.Server.Configuration;
using AgentTrafficLight.Server.Services;

namespace AgentTrafficLight.Server;

[ExcludeFromCodeCoverage]
public static class Program
{
    public static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        builder.Services.Configure<SerialOptions>(
            builder.Configuration.GetSection(SerialOptions.SectionName));

        builder.Services.AddSingleton<ISerialController, SerialController>();
        builder.Services.AddSingleton<ITrafficLightController, TrafficLightController>();
        builder.Services.AddHostedService<TestHarnessHostedService>();

        var host = builder.Build();
        await host.RunAsync().ConfigureAwait(false);
    }
}
