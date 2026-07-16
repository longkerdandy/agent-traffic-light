using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AgentTrafficLight.Contracts.Requests;
using AgentTrafficLight.Contracts.Responses;
using AgentTrafficLight.Server.Services;
using Microsoft.AspNetCore.TestHost;

namespace AgentTrafficLight.Server.Tests.Api;

public sealed class InstanceApiTests : IDisposable
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private readonly WebApplication _app;
    private readonly HttpClient _client;
    private readonly FakeTrafficLightController _controller;
    private readonly IInstanceStore _store;

    public InstanceApiTests()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration["Server:Host"] = "127.0.0.1";
        builder.Configuration["Server:Port"] = "0";
        builder.Configuration["Instance:TtlSeconds"] = "120";
        builder.Configuration["Instance:SweepIntervalSeconds"] = "5";

        _controller = new FakeTrafficLightController();
        builder.Services.AddSingleton<ITrafficLightController>(_controller);
        _app = Program.ConfigureWebApplication(builder);
        _store = _app.Services.GetRequiredService<IInstanceStore>();
        _app.StartAsync().GetAwaiter().GetResult();
        _client = _app.GetTestClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _app.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    [Fact]
    public async Task Connect_CreatesInstance_WithOffState()
    {
        var response = await _client.PostAsJsonAsync("/api/instances/id-1/connect", new ConnectRequest { Agent = "kimi" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await DeserializeAsync<ConnectResponse>(response);
        Assert.True(body.Ok);
        Assert.Equal("id-1", body.InstanceId);
        Assert.Equal(TrafficLightState.Off, body.State);
        Assert.True(body.ExpiresAt > DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Events_UpdatesState_AndHardwareFollowsController()
    {
        await _client.PostAsJsonAsync("/api/instances/id-1/connect", new ConnectRequest { Agent = "kimi" });
        await _client.PostAsJsonAsync("/api/instances/id-1/control", new ControlRequest { Enabled = true });

        var response = await _client.PostAsJsonAsync("/api/instances/id-1/events", new EventRequest { State = TrafficLightState.Thinking });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await DeserializeAsync<EventResponse>(response);
        Assert.True(body.Ok);
        Assert.Equal(TrafficLightState.Thinking, body.State);
        Assert.Equal(TrafficLightState.Thinking, _controller.CurrentState);
    }

    [Fact]
    public async Task Events_FromNonController_DoesNotChangeHardware()
    {
        await _client.PostAsJsonAsync("/api/instances/id-1/connect", new ConnectRequest { Agent = "kimi" });
        await _client.PostAsJsonAsync("/api/instances/id-1/control", new ControlRequest { Enabled = true });
        await _client.PostAsJsonAsync("/api/instances/id-1/events", new EventRequest { State = TrafficLightState.Thinking });
        _controller.SetStateCallCount = 0;

        await _client.PostAsJsonAsync("/api/instances/id-2/events", new EventRequest { State = TrafficLightState.Error });

        Assert.Equal(TrafficLightState.Thinking, _controller.CurrentState);
        Assert.Equal(0, _controller.SetStateCallCount);
    }

    [Fact]
    public async Task Events_InvalidState_ReturnsBadRequest()
    {
        var response = await _client.PostAsync(
            "/api/instances/id-1/events",
            JsonContent.Create(new { state = "invalid" }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Heartbeat_RefreshesTtl_WithoutChangingState()
    {
        await _client.PostAsJsonAsync("/api/instances/id-1/connect", new ConnectRequest { Agent = "kimi" });
        await _client.PostAsJsonAsync("/api/instances/id-1/events", new EventRequest { State = TrafficLightState.Busy });
        var before = _store.GetSnapshot()[0].ExpiresAt;

        var response = await _client.PostAsJsonAsync("/api/instances/id-1/heartbeat", new HeartbeatRequest());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await DeserializeAsync<HeartbeatResponse>(response);
        Assert.True(body.Ok);
        Assert.True(body.ExpiresAt > before);
        Assert.Equal(TrafficLightState.Busy, _store.GetSnapshot()[0].State);
    }

    [Fact]
    public async Task Disconnect_NonController_DoesNotChangeHardware()
    {
        await _client.PostAsJsonAsync("/api/instances/id-1/connect", new ConnectRequest { Agent = "kimi" });
        await _client.PostAsJsonAsync("/api/instances/id-1/control", new ControlRequest { Enabled = true });
        await _client.PostAsJsonAsync("/api/instances/id-1/events", new EventRequest { State = TrafficLightState.Thinking });
        await _client.PostAsJsonAsync("/api/instances/id-2/connect", new ConnectRequest { Agent = "claude" });

        var response = await _client.PostAsync("/api/instances/id-2/disconnect", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(TrafficLightState.Thinking, _controller.CurrentState);
    }

    [Fact]
    public async Task Control_Claim_SyncsExistingStateToHardware()
    {
        await _client.PostAsJsonAsync("/api/instances/id-1/connect", new ConnectRequest { Agent = "kimi" });
        await _client.PostAsJsonAsync("/api/instances/id-1/events", new EventRequest { State = TrafficLightState.Ai });

        var response = await _client.PostAsJsonAsync("/api/instances/id-1/control", new ControlRequest { Enabled = true });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await DeserializeAsync<ControlResponse>(response);
        Assert.True(body.Ok);
        Assert.True(body.IsController);
        Assert.Equal(TrafficLightState.Ai, _controller.CurrentState);
    }

    [Fact]
    public async Task Control_SecondClaim_ReturnsConflict()
    {
        await _client.PostAsJsonAsync("/api/instances/id-1/connect", new ConnectRequest { Agent = "kimi" });
        await _client.PostAsJsonAsync("/api/instances/id-1/control", new ControlRequest { Enabled = true });
        await _client.PostAsJsonAsync("/api/instances/id-2/connect", new ConnectRequest { Agent = "claude" });

        var response = await _client.PostAsJsonAsync("/api/instances/id-2/control", new ControlRequest { Enabled = true });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await DeserializeAsync<ControlResponse>(response);
        Assert.False(body.Ok);
        Assert.Equal("control_already_held", body.Error);
        Assert.Equal("id-1", body.ControllerInstanceId);
    }

    [Fact]
    public async Task Control_Release_Succeeds()
    {
        await _client.PostAsJsonAsync("/api/instances/id-1/connect", new ConnectRequest { Agent = "kimi" });
        await _client.PostAsJsonAsync("/api/instances/id-1/control", new ControlRequest { Enabled = true });

        var response = await _client.PostAsJsonAsync("/api/instances/id-1/control", new ControlRequest { Enabled = false });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await DeserializeAsync<ControlResponse>(response);
        Assert.True(body.Ok);
        Assert.False(body.IsController);
        Assert.Null(_store.GetControllerInstanceId());
    }

    [Fact]
    public async Task Control_Claim_Fails_WhenInstanceUnknown()
    {
        var response = await _client.PostAsJsonAsync("/api/instances/unknown/control", new ControlRequest { Enabled = true });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Disconnect_RemovesInstance_AndReleasesControl()
    {
        await _client.PostAsJsonAsync("/api/instances/id-1/connect", new ConnectRequest { Agent = "kimi" });
        await _client.PostAsJsonAsync("/api/instances/id-1/control", new ControlRequest { Enabled = true });
        await _client.PostAsJsonAsync("/api/instances/id-1/events", new EventRequest { State = TrafficLightState.Thinking });

        var response = await _client.PostAsync("/api/instances/id-1/disconnect", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await DeserializeAsync<DisconnectResponse>(response);
        Assert.True(body.Ok);
        Assert.False(_store.TryGet("id-1", out _));
        Assert.Null(_store.GetControllerInstanceId());
        Assert.Equal(TrafficLightState.Off, _controller.CurrentState);
    }

    [Fact]
    public async Task GetInstances_ReturnsControllerAndInstances()
    {
        await _client.PostAsJsonAsync("/api/instances/id-1/connect", new ConnectRequest { Agent = "kimi" });
        await _client.PostAsJsonAsync("/api/instances/id-1/control", new ControlRequest { Enabled = true });
        await _client.PostAsJsonAsync("/api/instances/id-1/events", new EventRequest { State = TrafficLightState.Thinking });
        await _client.PostAsJsonAsync("/api/instances/id-2/connect", new ConnectRequest { Agent = "claude" });

        var response = await _client.GetAsync("/api/instances");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await DeserializeAsync<InstancesResponse>(response);
        Assert.True(body.Ok);
        Assert.Equal(TrafficLightState.Thinking, body.State);
        Assert.Equal("id-1", body.ControllerInstanceId);
        Assert.Equal(2, body.Instances.Count);
    }

    [Fact]
    public async Task GetInstance_ReturnsInstanceOrNotFound()
    {
        await _client.PostAsJsonAsync("/api/instances/id-1/connect", new ConnectRequest { Agent = "kimi" });

        var found = await _client.GetAsync("/api/instances/id-1");
        Assert.Equal(HttpStatusCode.OK, found.StatusCode);
        var foundBody = await DeserializeAsync<InstanceDetailResponse>(found);
        Assert.True(foundBody.Ok);
        Assert.Equal("id-1", foundBody.Instance!.InstanceId);

        var missing = await _client.GetAsync("/api/instances/unknown");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        var missingBody = await DeserializeAsync<InstanceDetailResponse>(missing);
        Assert.False(missingBody.Ok);
    }

    [Fact]
    public async Task GetLight_ReturnsControllerState()
    {
        await _client.PostAsJsonAsync("/api/instances/id-1/connect", new ConnectRequest { Agent = "kimi" });
        await _client.PostAsJsonAsync("/api/instances/id-1/control", new ControlRequest { Enabled = true });
        await _client.PostAsJsonAsync("/api/instances/id-1/events", new EventRequest { State = TrafficLightState.Ai });

        var response = await _client.GetAsync("/api/light");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await DeserializeAsync<LightStatusResponse>(response);
        Assert.True(body.Ok);
        Assert.Equal(TrafficLightState.Ai, body.State);
        Assert.Equal("id-1", body.ControllerInstanceId);
        Assert.True(body.LastUpdated > DateTimeOffset.MinValue);
    }

    private static async Task<T> DeserializeAsync<T>(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        return JsonSerializer.Deserialize<T>(content, s_jsonOptions)!;
    }
}
