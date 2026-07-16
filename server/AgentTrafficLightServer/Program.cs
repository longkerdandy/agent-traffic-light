using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using AgentTrafficLight.Server.Api;
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
    /// Configures services and starts the web host.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
#if WINDOWS
        builder.Services.AddSingleton<ITrafficLightController, BleTrafficLightController>();
#else
        builder.Services.AddSingleton<ITrafficLightController, NoOpTrafficLightController>();
#endif
        var app = ConfigureWebApplication(builder);
        await app.RunAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Creates and configures the web application without starting it.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>The configured <see cref="WebApplication"/>.</returns>
    public static WebApplication CreateWebApplication(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        return ConfigureWebApplication(builder);
    }

    /// <summary>
    /// Configures services and endpoints on the provided builder.
    /// </summary>
    /// <param name="builder">The web application builder.</param>
    /// <returns>The configured <see cref="WebApplication"/>.</returns>
    public static WebApplication ConfigureWebApplication(WebApplicationBuilder builder)
    {
        builder.Services.Configure<ServerOptions>(
            builder.Configuration.GetSection(ServerOptions.SectionName));
        builder.Services.Configure<InstanceOptions>(
            builder.Configuration.GetSection(InstanceOptions.SectionName));
        builder.Services.Configure<BleOptions>(
            builder.Configuration.GetSection(BleOptions.SectionName));

        builder.Services.AddSingleton<IInstanceStore, InMemoryInstanceStore>();
        builder.Services.AddHostedService<InstanceCleanupHostedService>();

        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        });

        var app = builder.Build();

        var serverOptions = app.Services.GetRequiredService<IOptions<ServerOptions>>().Value;
        app.Urls.Clear();
        app.Urls.Add($"http://{serverOptions.Host}:{serverOptions.Port}");

        var store = app.Services.GetRequiredService<IInstanceStore>();
        var controller = app.Services.GetRequiredService<ITrafficLightController>();
        var instanceOptions = app.Services.GetRequiredService<IOptions<InstanceOptions>>();

        InstanceApi.MapEndpoints(app, store, controller, instanceOptions);

        return app;
    }
}
