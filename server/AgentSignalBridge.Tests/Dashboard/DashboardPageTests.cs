using System.Net;
using AgentSignalBridge.Server.Tests.Endpoints;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AgentSignalBridge.Server.Tests.Dashboard;

public sealed class DashboardPageTests : IDisposable
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public DashboardPageTests()
    {
        _factory = new TestWebApplicationFactory();
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task GetRoot_ReturnsOkAndContainsDashboardContent()
    {
        var response = await _client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Agent Signal Bridge", content);
        Assert.Contains("Dashboard", content);
        Assert.Contains("Active Agents", content);
        Assert.Contains("Active Hardware", content);
        Assert.Contains("Manual Controls", content);
    }
}
