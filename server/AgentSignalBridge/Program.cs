using System.Diagnostics.CodeAnalysis;
using AgentSignalBridge.Server.Configuration;
using AgentSignalBridge.Server.Dashboard;
using AgentSignalBridge.Server.Drivers;
using AgentSignalBridge.Server.Endpoints;
using AgentSignalBridge.Server.Events;
using AgentSignalBridge.Server.Hardware;
using AgentSignalBridge.Server.Services;
using AgentSignalBridge.Server.Stores;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace AgentSignalBridge.Server;

/// <summary>
/// Application entry point for the Agent Signal Bridge server.
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

        var bleOptions = builder.Configuration
            .GetSection(BleOptions.SectionName)
            .Get<BleOptions>() ?? new BleOptions();

        builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
        builder.Services.AddSingleton<StateChangeNotifier>();
        builder.Services.AddSingleton<IAgentStore, InMemoryAgentStore>();

        if (bleOptions.Enabled)
        {
            builder.Services.AddSingleton<IAgentCoreLightDriver, BleAgentCoreLightDriver>();
        }
        else
        {
            builder.Services.AddSingleton<IAgentCoreLightDriver, NoOpAgentCoreLightDriver>();
        }
        builder.Services.AddSingleton<IAgentCoreLightManager, AgentCoreLightManager>();
        builder.Services.AddSingleton<AgentCoreLightManager>(
            sp => (AgentCoreLightManager)sp.GetRequiredService<IAgentCoreLightManager>());
        builder.Services.AddSingleton<IAgentEventSubscriber>(
            sp => (AgentCoreLightManager)sp.GetRequiredService<IAgentCoreLightManager>());
        builder.Services.AddHostedService(
            sp => sp.GetRequiredService<AgentCoreLightManager>());
        builder.Services.AddSingleton<AgentEventDispatcher>();
        builder.Services.AddSingleton<AgentLifecycleService>();
        builder.Services.AddSingleton<DashboardStateService>();
        builder.Services.AddRazorPages();
        builder.Services.AddServerSideBlazor();
        builder.Services.AddHttpClient();

        var app = builder.Build();
        app.UseStaticFiles();
        app.MapAgentEndpoints();
        app.MapBlazorHub();
        app.MapFallbackToPage("/_Host");
        await app.RunAsync().ConfigureAwait(false);
    }
}
