namespace AgentTrafficLight.Server.Models;

public enum TrafficLightState
{
    Off,
    Idle,
    Thinking,
    Ai,
    Busy,
    WaitConfirm,
    Success,
    Error
}

public static class TrafficLightStateExtensions
{
    public static string ToSerialCommand(this TrafficLightState state) => state switch
    {
        TrafficLightState.Off => "off",
        TrafficLightState.Idle => "idle",
        TrafficLightState.Thinking => "thinking",
        TrafficLightState.Ai => "ai",
        TrafficLightState.Busy => "busy",
        TrafficLightState.WaitConfirm => "wait_confirm",
        TrafficLightState.Success => "success",
        TrafficLightState.Error => "error",
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, null)
    };
}
