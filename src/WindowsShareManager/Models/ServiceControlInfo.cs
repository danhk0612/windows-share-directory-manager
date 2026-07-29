namespace WindowsShareManager.Models;

public sealed class ServiceControlInfo
{
    public string Name { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string Status { get; init; } = "";
    public string StartMode { get; init; } = "";

    public bool IsRunning => Status.Equals("Running", StringComparison.OrdinalIgnoreCase);
    public bool IsDisabled => StartMode.Equals("Disabled", StringComparison.OrdinalIgnoreCase);

    public string StatusDisplay => Status switch
    {
        "Running" => "실행 중",
        "Stopped" => "중지",
        "StartPending" => "시작 중",
        "StopPending" => "중지 중",
        _ => Status
    };

    public string StartModeDisplay => StartMode switch
    {
        "Automatic" => "자동",
        "AutomaticDelayed" => "자동(지연된 시작)",
        "Manual" => "수동/요청 시 시작",
        "Disabled" => "사용 안 함",
        _ => StartMode
    };
}

public enum ServiceStartOption
{
    StartOnly,
    ManualAndStart,
    AutomaticAndStart,
    DelayedAutomaticAndStart
}
