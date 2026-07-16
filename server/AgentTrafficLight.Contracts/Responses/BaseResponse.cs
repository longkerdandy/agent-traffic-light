using System.Text.Json.Serialization;

namespace AgentTrafficLight.Contracts.Responses;

/// <summary>
/// Base class for API responses.
/// </summary>
public abstract class BaseResponse
{
    /// <summary>
    /// Gets or sets a value indicating whether the request succeeded.
    /// </summary>
    [JsonPropertyOrder(-2)]
    public bool Ok { get; set; }

    /// <summary>
    /// Gets or sets an error code when <see cref="Ok"/> is <see langword="false"/>.
    /// </summary>
    [JsonPropertyOrder(-1)]
    public string? Error { get; set; }
}
