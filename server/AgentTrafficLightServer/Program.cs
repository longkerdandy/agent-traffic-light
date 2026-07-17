using System.Diagnostics.CodeAnalysis;
using AgentTrafficLight.Server.Configuration;
using AgentTrafficLight.Server.Drivers;
using AgentTrafficLight.Server.Endpoints;
using AgentTrafficLight.Server.Events;
using AgentTrafficLight.Server.Hardware;
using AgentTrafficLight.Server.Services;
using AgentTrafficLight.Server.Stores;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace AgentTrafficLight.Server;

/// <summary>
/// Application entry point for the Agent Traffic Light server.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class Program
{
    /// <summary>
    /// Configures services and starts the host.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.Configure<BleOptions>(
            builder.Configuration.GetSection(BleOptions.SectionName));
        builder.Services.Configure<ServerOptions>(
            builder.Configuration.GetSection(ServerOptions.SectionName));
        builder.Services.Configure<AgentOptions>(
            builder.Configuration.GetSection(AgentOptions.SectionName));

        var serverOptions = builder.Configuration
            .GetSection(ServerOptions.SectionName)
            .Get<ServerOptions>() ?? new ServerOptions();
        builder.WebHost.UseSetting(WebHostDefaults.ServerUrlsKey, $"http://{serverOptions.Host}:{serverOptions.Port}");

        builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
        builder.Services.AddSingleton<StateChangeNotifier>();
        builder.Services.AddSingleton<IAgentStore, InMemoryAgentStore>();
        builder.Services.AddSingleton<IAgentCoreLightDriver, BleAgentCoreLightDriver>();
        builder.Services.AddSingleton<IAgentCoreLightManager, AgentCoreLightManager>();
        builder.Services.AddSingleton<AgentCoreLightManager>(
            sp => (AgentCoreLightManager)sp.GetRequiredService<IAgentCoreLightManager>());
        builder.Services.AddSingleton<IAgentEventSubscriber>(
            sp => (AgentCoreLightManager)sp.GetRequiredService<IAgentCoreLightManager>());
        builder.Services.AddHostedService(
            sp => sp.GetRequiredService<AgentCoreLightManager>());
        builder.Services.AddSingleton<AgentEventDispatcher>();
        builder.Services.AddSingleton<AgentLifecycleService>();

        var app = builder.Build();
        app.MapAgentEndpoints();
        await app.RunAsync().ConfigureAwait(false);
    }
}
