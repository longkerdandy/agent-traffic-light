using System.Text.Json;
using AgentTrafficLight.Server.Api;
using AgentTrafficLight.Server.Drivers;
using AgentTrafficLight.Server.Events;
using AgentTrafficLight.Server.Hardware;
using AgentTrafficLight.Server.Models;
using AgentTrafficLight.Server.Services;
using AgentTrafficLight.Server.Stores;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace AgentTrafficLight.Server.Endpoints;

/// <summary>
/// Minimal-API endpoint handlers for agent lifecycle, master control, and status.
/// </summary>
public static class AgentEndpoints
{
    private static readonly JsonSerializerOptions StreamJsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Maps the v0.3 HTTP API endpoints.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <returns>The web application for chaining.</returns>
    public static IApplicationBuilder MapAgentEndpoints(this IApplicationBuilder app)
    {
        var webApp = (WebApplication)app;

        webApp.MapPost("/hook", HandleHookAsync);
        webApp.MapPost("/heartbeat", HandleHeartbeat);
        webApp.MapPost("/api/master", HandleMaster);
        webApp.MapGet("/api/status", HandleStatus);
        webApp.MapGet("/api/sessions", HandleSessions);
        webApp.MapGet("/stream", HandleStreamAsync);

        return app;
    }

    private static async Task<IResult> HandleHookAsync(
        HookRequest request,
        AgentLifecycleService lifecycleService,
        IAgentStore store,
        IAgentCoreLightManager manager,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(request.Event))
        {
            return Results.BadRequest(new { ok = false, error = "invalid_event" });
        }

        var now = timeProvider.GetUtcNow();
        await lifecycleService.ProcessEventAsync(
            request.AgentId,
            request.AgentName,
            request.Cwd,
            request.Event,
            now,
            cancellationToken).ConfigureAwait(false);

        var isMaster = store.GetMasterAgentId() == request.AgentId;

        return Results.Ok(new HookResponse
        {
            Ok = true,
            AgentId = request.AgentId,
            IsMaster = isMaster,
            Command = isMaster ? manager.LastCommand : null
        });
    }

    private static IResult HandleHeartbeat(
        HeartbeatRequest request,
        IAgentStore store,
        TimeProvider timeProvider)
    {
        var now = timeProvider.GetUtcNow();
        var agent = store.Touch(request.AgentId, request.AgentName, request.Cwd, now);

        return Results.Ok(new HeartbeatResponse
        {
            Ok = true,
            AgentId = request.AgentId,
            IsMaster = agent.IsMaster
        });
    }

    private static IResult HandleMaster(
        MasterRequest request,
        IAgentStore store)
    {
        if (request.Enabled)
        {
            if (store.TrySetMaster(request.AgentId))
            {
                return Results.Ok(new MasterResponse
                {
                    Ok = true,
                    AgentId = request.AgentId,
                    IsMaster = true
                });
            }

            return Results.Conflict(new MasterResponse
            {
                Ok = false,
                AgentId = request.AgentId,
                IsMaster = false,
                Error = "master_already_held",
                MasterAgentId = store.GetMasterAgentId()
            });
        }

        var released = store.TryReleaseMaster(request.AgentId);
        var isStillMaster = !released && store.GetMasterAgentId() == request.AgentId;

        return Results.Ok(new MasterResponse
        {
            Ok = true,
            AgentId = request.AgentId,
            IsMaster = isStillMaster
        });
    }

    private static IResult HandleStatus(
        IAgentCoreLightManager manager,
        IAgentStore store)
    {
        return Results.Ok(new StatusResponse
        {
            Ok = true,
            Command = manager.LastCommand,
            IsConnected = manager.IsConnected,
            MasterAgentId = store.GetMasterAgentId(),
            LastUpdated = manager.LastUpdated
        });
    }

    private static IResult HandleSessions(
        IAgentStore store)
    {
        var snapshot = store.GetSnapshot();
        var sessions = snapshot
            .Select(a => new SessionInfo
            {
                AgentId = a.AgentId,
                AgentName = a.AgentName,
                Event = a.Event,
                IsMaster = a.IsMaster,
                LastSeen = a.LastSeen
            })
            .ToList();

        return Results.Ok(new SessionsResponse
        {
            Ok = true,
            MasterAgentId = store.GetMasterAgentId(),
            Sessions = sessions
        });
    }

    private static async Task HandleStreamAsync(
        HttpContext context,
        StateChangeNotifier notifier,
        CancellationToken cancellationToken)
    {
        context.Response.Headers.ContentType = "text/event-stream";
        context.Response.Headers.CacheControl = "no-cache";

        await foreach (var state in notifier.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            await context.Response.WriteAsync("event: state\n", cancellationToken).ConfigureAwait(false);
            await context.Response.WriteAsync(
                $"data: {JsonSerializer.Serialize(state, StreamJsonOptions)}\n\n",
                cancellationToken).ConfigureAwait(false);
            await context.Response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
