using System.Text.Json.Serialization;
using AgentTrafficLight.Contracts.Models;
using AgentTrafficLight.Contracts.Requests;
using AgentTrafficLight.Contracts.Responses;
using AgentTrafficLight.Server.Configuration;
using AgentTrafficLight.Server.Models;
using AgentTrafficLight.Server.Services;

namespace AgentTrafficLight.Server.Api;

/// <summary>
/// Maps the v0.3 instance HTTP API endpoints.
/// </summary>
public static class InstanceApi
{
    /// <summary>
    /// Maps instance API endpoints onto the route builder.
    /// </summary>
    /// <param name="app">The endpoint route builder.</param>
    /// <param name="store">The instance store.</param>
    /// <param name="controller">The traffic-light controller.</param>
    /// <param name="options">Instance options.</param>
    public static void MapEndpoints(
        IEndpointRouteBuilder app,
        IInstanceStore store,
        ITrafficLightController controller,
        IOptions<InstanceOptions> options)
    {
        var ttl = TimeSpan.FromSeconds(options.Value.TtlSeconds);

        app.MapPost("/api/instances/{instanceId}/connect", async (
            string instanceId,
            ConnectRequest? request,
            CancellationToken cancellationToken) =>
        {
            request ??= new ConnectRequest();
            var now = DateTimeOffset.UtcNow;
            var instance = store.Upsert(instanceId, request.Agent, request.Cwd, now, ttl);
            await SyncHardwareAsync(store, controller, cancellationToken).ConfigureAwait(false);

            return Results.Ok(new ConnectResponse
            {
                Ok = true,
                InstanceId = instance.InstanceId,
                State = instance.State,
                ExpiresAt = instance.ExpiresAt
            });
        });

        app.MapPost("/api/instances/{instanceId}/disconnect", async (
            string instanceId,
            CancellationToken cancellationToken) =>
        {
            var wasController = store.GetControllerInstanceId() == instanceId;
            _ = store.TryRemove(instanceId, out _);

            if (wasController)
            {
                await controller.SetStateAsync(TrafficLightState.Off, cancellationToken).ConfigureAwait(false);
            }

            return Results.Ok(new DisconnectResponse
            {
                Ok = true,
                InstanceId = instanceId
            });
        });

        app.MapPost("/api/instances/{instanceId}/events", async (
            string instanceId,
            EventRequest request,
            CancellationToken cancellationToken) =>
        {
            var now = DateTimeOffset.UtcNow;
            var instance = store.SetState(instanceId, "unknown", null, request.State, now, ttl);
            await SyncHardwareAsync(store, controller, cancellationToken).ConfigureAwait(false);

            return Results.Ok(new EventResponse
            {
                Ok = true,
                InstanceId = instance.InstanceId,
                State = instance.State
            });
        });

        app.MapPost("/api/instances/{instanceId}/heartbeat", async (
            string instanceId,
            HeartbeatRequest? request,
            CancellationToken cancellationToken) =>
        {
            request ??= new HeartbeatRequest();
            var now = DateTimeOffset.UtcNow;
            var instance = store.Touch(instanceId, request.Agent, request.Cwd, now, ttl);
            await SyncHardwareAsync(store, controller, cancellationToken).ConfigureAwait(false);

            return Results.Ok(new HeartbeatResponse
            {
                Ok = true,
                InstanceId = instance.InstanceId,
                ExpiresAt = instance.ExpiresAt
            });
        });

        app.MapPost("/api/instances/{instanceId}/control", async (
            string instanceId,
            ControlRequest request,
            CancellationToken cancellationToken) =>
        {
            if (request.Enabled)
            {
                if (!store.TrySetController(instanceId, out var conflictInstanceId))
                {
                    return conflictInstanceId == null
                        ? Results.BadRequest(new ControlResponse
                        {
                            Ok = false,
                            Error = "instance_not_found",
                            InstanceId = instanceId,
                            IsController = false
                        })
                        : Results.Conflict(new ControlResponse
                        {
                            Ok = false,
                            Error = "control_already_held",
                            InstanceId = instanceId,
                            IsController = false,
                            ControllerInstanceId = conflictInstanceId
                        });
                }

                await SyncHardwareAsync(store, controller, cancellationToken).ConfigureAwait(false);

                return Results.Ok(new ControlResponse
                {
                    Ok = true,
                    InstanceId = instanceId,
                    IsController = true
                });
            }

            if (store.TryReleaseController(instanceId))
            {
                await controller.SetStateAsync(TrafficLightState.Off, cancellationToken).ConfigureAwait(false);
            }

            return Results.Ok(new ControlResponse
            {
                Ok = true,
                InstanceId = instanceId,
                IsController = false
            });
        });

        app.MapGet("/api/instances", () =>
        {
            var controllerInstanceId = store.GetControllerInstanceId();
            var hardwareState = controllerInstanceId == null
                ? TrafficLightState.Off
                : (store.TryGet(controllerInstanceId, out var controllerInstance)
                    ? controllerInstance.State
                    : TrafficLightState.Off);

            return Results.Ok(new InstancesResponse
            {
                Ok = true,
                State = hardwareState,
                ControllerInstanceId = controllerInstanceId,
                Instances = store.GetSnapshot().Select(ToResponse).ToList()
            });
        });

        app.MapGet("/api/instances/{instanceId}", (string instanceId) =>
        {
            if (!store.TryGet(instanceId, out var instance))
            {
                return Results.NotFound(new InstanceDetailResponse
                {
                    Ok = false,
                    Error = "instance_not_found"
                });
            }

            return Results.Ok(new InstanceDetailResponse
            {
                Ok = true,
                Instance = ToResponse(instance)
            });
        });

        app.MapGet("/api/light", () =>
        {
            return Results.Ok(new LightStatusResponse
            {
                Ok = true,
                State = controller.CurrentState,
                IsConnected = controller.IsConnected,
                ControllerInstanceId = store.GetControllerInstanceId(),
                LastUpdated = DateTimeOffset.UtcNow
            });
        });
    }

    private static async Task SyncHardwareAsync(
        IInstanceStore store,
        ITrafficLightController controller,
        CancellationToken cancellationToken)
    {
        var controllerInstanceId = store.GetControllerInstanceId();
        if (controllerInstanceId == null)
        {
            return;
        }

        if (!store.TryGet(controllerInstanceId, out var instance))
        {
            await controller.SetStateAsync(TrafficLightState.Off, cancellationToken).ConfigureAwait(false);
            return;
        }

        await controller.SetStateAsync(instance.State, cancellationToken).ConfigureAwait(false);
    }

    private static InstanceResponse ToResponse(AgentInstance instance) => new()
    {
        InstanceId = instance.InstanceId,
        Agent = instance.Agent,
        State = instance.State,
        IsController = instance.IsController,
        LastSeen = instance.LastSeen,
        ExpiresAt = instance.ExpiresAt
    };
}
