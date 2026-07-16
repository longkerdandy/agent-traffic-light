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

        // Register or refresh an agent instance.
        // Logic:
        //   1. Creates a new instance with initial state Off if the id does not exist.
        //   2. Updates agent/cwd and refreshes LastSeen and ExpiresAt if it already exists.
        //   3. Calls SyncHardwareAsync to write the controller's requested state to hardware
        //      when a controller exists; does nothing otherwise.
        // Note: connect does not claim control automatically; the client must call /control.
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

        // Immediately remove an instance.
        // Logic:
        //   1. Check whether this instance is the current controller before removal,
        //      because TryRemove clears controller state when the controller is removed.
        //   2. Remove the instance from the store.
        //   3. Send Off to hardware only if the removed instance was the controller.
        //      Disconnecting a non-controller leaves the hardware state unchanged.
        //   4. Returns 200 even for unknown or already-expired instances (idempotent, fail-open).
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

        // Associate a requested hardware state with an instance.
        // Logic:
        //   1. Creates the instance if missing, otherwise updates State and refreshes TTL.
        //   2. The state value must be a valid TrafficLightState enum name; invalid values
        //      are rejected by model binding/validation and return 400 Bad Request.
        //   3. Calls SyncHardwareAsync: the hardware is driven only when this instance is
        //      the current controller. Non-controller state changes are kept in memory only.
        // Note: requesting Off sets the instance state to Off but does not remove it.
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

        // Keep an instance alive without changing its state.
        // Logic:
        //   1. Creates the instance with default agent unknown if missing, otherwise refreshes
        //      LastSeen and ExpiresAt.
        //   2. Calls SyncHardwareAsync to re-sync the controller state to hardware.
        //      Underlying controllers usually short-circuit when the state has not changed.
        //   3. Returns the new expires_at; clients should send another heartbeat before
        //      the previous one expires.
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

        // Claim or release exclusive hardware control.
        // Logic:
        //   enabled = true (claim):
        //     1. store.TrySetController succeeds if the instance is already the controller (idempotent).
        //     2. If control is free, the instance becomes controller and SyncHardwareAsync writes
        //        its current requested state to hardware (supports /events before /control).
        //     3. If another instance already holds control, return 409 Conflict with the
        //        current controller_instance_id in the response body.
        //     4. If the instance is not registered, return 400 Bad Request.
        //   enabled = false (release):
        //     1. store.TryReleaseController only releases control when this instance is the controller.
        //     2. After a successful release, send Off to hardware. If release fails (not controller),
        //        still return 200 but leave hardware state unchanged.
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

        // List the current controller, hardware state, and all active instances.
        // Logic:
        //   1. Read controller_instance_id from the store.
        //   2. If a controller exists and is still valid, hardware state is that instance's State;
        //      otherwise it is Off.
        //   3. Return a snapshot of all non-expired instances (GetSnapshot filters expired ones).
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

        // Return a single active instance.
        // Logic:
        //   1. store.TryGet validates both existence and non-expiration.
        //   2. Returns 404 Not Found when unknown or expired; otherwise 200 with instance details.
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

        // Return the current hardware state and connection status.
        // Logic:
        //   1. state reads ITrafficLightController.CurrentState, i.e. the last state successfully
        //      written to hardware.
        //   2. is_connected reflects whether the underlying controller (e.g. BLE) reports a live
        //      connection.
        //   3. controller_instance_id comes from the store; null means no controller currently.
        //   4. last_updated is the UTC time this response was generated. The controller internally
        //      tracks when the hardware was last changed; this endpoint does not track it separately.
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

    /// <summary>
    /// Synchronizes the current controller instance's state to the hardware.
    /// Logic:
    ///   1. If there is no controller, return immediately and leave hardware as-is
    ///      (disconnect/control are responsible for turning it Off).
    ///   2. If the controller instance is expired/missing, send Off as a safe fallback.
    ///   3. If the controller exists, call controller.SetStateAsync(instance.State).
    ///      Underlying controllers such as BleTrafficLightController short-circuit when the
    ///      requested state equals CurrentState to avoid redundant writes. FakeTrafficLightController
    ///      mimics this behavior in tests.
    /// </summary>
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
