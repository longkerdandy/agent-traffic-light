using System.Diagnostics.CodeAnalysis;

namespace AgentTrafficLight.Server;

public static class Program
{
    [ExcludeFromCodeCoverage]
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var app = builder.Build();

        app.MapGet("/", () => "Agent Traffic Light Server");

        app.Run();
    }
}
