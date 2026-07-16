namespace AgentTrafficLight.Contracts.Responses;

/// <summary>
/// Response returned after claiming or releasing control.
/// </summary>
public sealed class ControlResponse : BaseResponse
{
    /// <summary>
    /// Gets or sets the instance identifier.
    /// </summary>
    public string? InstanceId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the instance is now the controller.
    /// </summary>
    public bool IsController { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the instance that currently holds control
    /// when a claim conflict occurs.
    /// </summary>
    public string? ControllerInstanceId { get; set; }
}
