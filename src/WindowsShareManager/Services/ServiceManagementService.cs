using WindowsShareManager.Models;

namespace WindowsShareManager.Services;

public sealed class ServiceManagementService
{
    private static readonly HashSet<string> AllowedServices =
    [
        "LanmanServer",
        "Spooler",
        "fdPHost",
        "FDResPub",
        "SSDPSRV",
        "upnphost",
        "Dnscache"
    ];

    private readonly PowerShellRunner _runner = new();

    public async Task<ServiceControlInfo> GetServiceInfoAsync(
        string serviceName,
        CancellationToken cancellationToken = default)
    {
        ValidateServiceName(serviceName);
        const string script = """
            $name = $env:WSM_SERVICE_NAME
            $service = Get-CimInstance Win32_Service -Filter "Name='$name'" -ErrorAction Stop
            $registry = Get-ItemProperty `
              -LiteralPath "HKLM:\SYSTEM\CurrentControlSet\Services\$name" `
              -ErrorAction SilentlyContinue
            $startMode = switch ([string]$service.StartMode) {
              'Auto' {
                if ([int]$registry.DelayedAutoStart -eq 1) { 'AutomaticDelayed' }
                else { 'Automatic' }
              }
              'Manual' { 'Manual' }
              'Disabled' { 'Disabled' }
              default { [string]$service.StartMode }
            }
            [pscustomobject]@{
              Name = [string]$service.Name
              DisplayName = [string]$service.DisplayName
              Status = [string]$service.State
              StartMode = $startMode
            } | ConvertTo-Json -Depth 3 -Compress
            """;

        return await _runner.RunJsonAsync<ServiceControlInfo>(
            script,
            EnvironmentFor(serviceName),
            cancellationToken)
            ?? throw new InvalidOperationException("서비스 상태를 읽지 못했습니다.");
    }

    public async Task<ServiceControlInfo> ApplyStartOptionAsync(
        string serviceName,
        ServiceStartOption option,
        CancellationToken cancellationToken = default)
    {
        ValidateServiceName(serviceName);
        ValidateOption(serviceName, option);

        var script = option switch
        {
            ServiceStartOption.StartOnly => """
                $service = Get-Service -Name $env:WSM_SERVICE_NAME -ErrorAction Stop
                $configuration = Get-CimInstance Win32_Service `
                  -Filter "Name='$env:WSM_SERVICE_NAME'" -ErrorAction Stop
                if ($configuration.StartMode -eq 'Disabled') {
                  throw '사용 안 함으로 설정된 서비스는 시작 유형을 먼저 변경해야 합니다.'
                }
                if ($service.Status -ne 'Running') {
                  Start-Service -Name $env:WSM_SERVICE_NAME
                }
                """,
            ServiceStartOption.ManualAndStart => """
                Set-Service -Name $env:WSM_SERVICE_NAME -StartupType Manual
                Start-Service -Name $env:WSM_SERVICE_NAME
                """,
            ServiceStartOption.AutomaticAndStart => """
                Set-Service -Name $env:WSM_SERVICE_NAME -StartupType Automatic
                Set-ItemProperty `
                  -LiteralPath "HKLM:\SYSTEM\CurrentControlSet\Services\$env:WSM_SERVICE_NAME" `
                  -Name DelayedAutoStart -Type DWord -Value 0 -ErrorAction SilentlyContinue
                Start-Service -Name $env:WSM_SERVICE_NAME
                """,
            ServiceStartOption.DelayedAutomaticAndStart => """
                & sc.exe config $env:WSM_SERVICE_NAME start= delayed-auto | Out-Null
                if ($LASTEXITCODE -ne 0) {
                  throw "서비스 시작 유형 변경이 종료 코드 $LASTEXITCODE로 실패했습니다."
                }
                Start-Service -Name $env:WSM_SERVICE_NAME
                """,
            _ => throw new ArgumentOutOfRangeException(nameof(option))
        };

        Logger.Info($"서비스 시작 설정 적용: {serviceName}, {option}");
        await _runner.RunAsync(script, EnvironmentFor(serviceName), cancellationToken);
        var updated = await GetServiceInfoAsync(serviceName, cancellationToken);
        Logger.Info(
            $"서비스 적용 결과: {serviceName}, 상태={updated.Status}, 시작 유형={updated.StartMode}");
        return updated;
    }

    public static bool AllowsAutomatic(string serviceName) =>
        serviceName is "LanmanServer" or "Spooler";

    public static bool AllowsDelayedAutomatic(string serviceName) =>
        serviceName == "FDResPub";

    public static bool IsTriggerIdleExpected(string serviceName) =>
        serviceName == "fdPHost";

    private static void ValidateOption(string serviceName, ServiceStartOption option)
    {
        if (option == ServiceStartOption.AutomaticAndStart &&
            !AllowsAutomatic(serviceName))
        {
            throw new InvalidOperationException("이 서비스에는 자동 시작 변경을 제공하지 않습니다.");
        }

        if (option == ServiceStartOption.DelayedAutomaticAndStart &&
            !AllowsDelayedAutomatic(serviceName))
        {
            throw new InvalidOperationException("이 서비스에는 지연된 자동 시작 변경을 제공하지 않습니다.");
        }
    }

    private static void ValidateServiceName(string serviceName)
    {
        if (!AllowedServices.Contains(serviceName))
        {
            throw new ArgumentException("관리할 수 없는 서비스입니다.", nameof(serviceName));
        }
    }

    private static Dictionary<string, string?> EnvironmentFor(string serviceName) => new()
    {
        ["WSM_SERVICE_NAME"] = serviceName
    };
}
