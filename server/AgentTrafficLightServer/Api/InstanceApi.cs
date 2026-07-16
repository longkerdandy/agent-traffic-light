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

        // connect：注册或刷新一个 agent 实例。
        // 逻辑原理：
        //   1. 如果 instanceId 不存在，则在内存存储中创建新实例，初始状态为 Off。
        //   2. 如果已存在，则更新 agent/cwd 并重置 LastSeen 与 ExpiresAt（续约 TTL）。
        //   3. 调用 SyncHardwareAsync：若当前已有控制器实例，则把控制器的请求状态同步到硬件；
        //      无控制器时不操作硬件。
        // 注意：connect 不会自动获取控制权，客户端需显式调用 /control。
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

        // disconnect：立即移除指定实例。
        // 逻辑原理：
        //   1. 先判断该实例是否是当前控制器（因为在 TryRemove 后控制器信息会被清空）。
        //   2. 从内存存储中移除实例；若是控制器，存储会同步释放控制权。
        //   3. 仅当移除的是控制器时，才向硬件发送 Off；非控制器断开不影响当前硬件状态。
        //   4. 对未知或已过期实例返回 200（幂等、fail-open）。
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

        // events：客户端请求把某个硬件状态关联到指定实例。
        // 逻辑原理：
        //   1. 若 instanceId 不存在则创建实例，否则更新其 State 并刷新 TTL。
        //   2. 请求体中的 state 必须是合法的 TrafficLightState 枚举名；非法值会被 ASP.NET Core
        //      模型绑定/验证自动拦截并返回 400 Bad Request。
        //   3. 调用 SyncHardwareAsync：仅当该实例是当前控制器时，才会把状态写入硬件；
        //      非控制器的状态变更只保存在内存中，不驱动硬件。
        // 注意：请求 Off 只会把实例状态设为 Off，不会移除实例。
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

        // heartbeat：保持实例存活，不改变其状态。
        // 逻辑原理：
        //   1. 如果 instanceId 不存在则创建（agent 默认 unknown），否则刷新 LastSeen 与 ExpiresAt。
        //   2. 调用 SyncHardwareAsync 重新同步控制器状态到硬件（若控制器存在且状态未变，
        //      底层控制器通常会短路跳过实际写入）。
        //   3. 返回新的 expires_at，客户端应在上次心跳到期前再次发送心跳。
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

        // control：声明或释放对硬件的独占控制权。
        // 逻辑原理：
        //   请求 enabled = true（声明控制）：
        //     1. 调用 store.TrySetController；若该实例已是控制器，返回 200（幂等）。
        //     2. 若控制空闲，则该实例成为控制器，随后 SyncHardwareAsync 会把它当前请求的
        //        状态写入硬件（支持先 /events 再 /control 的场景）。
        //     3. 若已被其他实例控制，返回 409 Conflict，并在响应中附带当前 controller_instance_id。
        //     4. 若实例尚未注册，返回 400 Bad Request（未找到实例）。
        //   请求 enabled = false（释放控制）：
        //     1. 仅当该实例确实是当前控制器时，store.TryReleaseController 才会释放控制权。
        //     2. 释放成功后向硬件发送 Off；释放失败（不是控制器）也返回 200，但硬件状态不变。
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

        // GET /api/instances：返回当前控制器、硬件状态以及所有活跃实例列表。
        // 逻辑原理：
        //   1. 从存储获取 controller_instance_id。
        //   2. 若存在控制器且实例仍有效，硬件状态取该实例的 State；否则为 Off。
        //   3. 返回所有未过期实例的快照（GetSnapshot 会自动过滤已过期实例）。
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

        // GET /api/instances/{instanceId}：返回单个活跃实例详情。
        // 逻辑原理：
        //   1. 调用 store.TryGet，该方法会同时校验实例是否存在且未过期。
        //   2. 找不到或已过期返回 404 Not Found；找到返回 200 及实例详情。
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

        // GET /api/light：返回硬件实时状态与连接情况。
        // 逻辑原理：
        //   1. state 直接读取自 ITrafficLightController.CurrentState，即最后一次成功写入硬件的状态。
        //   2. is_connected 反映底层控制器（如 BLE）是否报告已连接。
        //   3. controller_instance_id 来自存储；为 null 表示当前无控制器。
        //   4. last_updated 取当前 UTC 时间作为响应生成时间（注意：不是硬件实际变更时间，
        //      硬件变更时间由控制器内部维护，此处不额外追踪）。
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
    /// 将当前控制器实例的状态同步到硬件。
    /// 逻辑原理：
    ///   1. 若无控制器，直接返回，不修改硬件（保持其当前状态，通常由 disconnect/control 负责归零）。
    ///   2. 若控制器实例已过期/不存在，发送 Off 作为安全回退。
    ///   3. 若控制器存在，调用 controller.SetStateAsync(instance.State)。
    ///      底层控制器（如 BleTrafficLightController）会在 state 与 CurrentState 相同时短路，
    ///      避免重复写入；FakeTrafficLightController 在测试中也模拟了该行为。
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
