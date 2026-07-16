using System.Text.Json.Serialization;
using AgentTrafficLight.Contracts.Models;

namespace AgentTrafficLight.Contracts.Responses;

/// <summary>
/// Response returned by the single-instance endpoint.
/// </summary>
public sealed class InstanceDetailResponse : BaseResponse
{
    /// <summary>
    /// Gets or sets the requested instance.
    /// </summary>
    public InstanceResponse? Instance { get; set; }
}
