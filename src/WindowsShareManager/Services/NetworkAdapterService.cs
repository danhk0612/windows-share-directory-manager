using WindowsShareManager.Models;

namespace WindowsShareManager.Services;

public sealed class NetworkAdapterService
{
    private readonly PowerShellRunner _runner = new();

    public async Task<IReadOnlyList<NetworkAdapterInfo>> GetAdaptersAsync(
        CancellationToken cancellationToken = default)
    {
        const string script = """
            $items = Get-NetIPConfiguration | Where-Object {
              $_.NetAdapter.Status -eq 'Up' -and $_.IPv4Address
            } | ForEach-Object {
              $config = $_
              $adapter = Get-NetAdapter -InterfaceIndex $config.InterfaceIndex
              $profile = Get-NetConnectionProfile -InterfaceIndex $config.InterfaceIndex -ErrorAction SilentlyContinue
              $ipInterface = Get-NetIPInterface -InterfaceIndex $config.InterfaceIndex `
                -AddressFamily IPv4 -ErrorAction SilentlyContinue |
                Sort-Object InterfaceMetric |
                Select-Object -First 1
              $description = [string]$adapter.InterfaceDescription
              $isVirtual = (-not [bool]$adapter.HardwareInterface) -or
                $description -match '(?i)VMware|Hyper-V|Virtual|VPN|TAP|TUN|WSL|Loopback'
              $ipv4 = ($config.IPv4Address | Select-Object -First 1).IPAddress
              $gateway = ($config.IPv4DefaultGateway | Select-Object -First 1).NextHop
              $category = ($profile | Select-Object -First 1).NetworkCategory
              [pscustomobject]@{
                InterfaceIndex = [int]$config.InterfaceIndex
                InterfaceId = [string]$adapter.InterfaceGuid
                Name = [string]$config.InterfaceAlias
                Description = $description
                IPv4Address = [string]$ipv4
                DefaultGateway = [string]$gateway
                NetworkCategory = [string]$category
                InterfaceMetric = if ($ipInterface) { [int]$ipInterface.InterfaceMetric } else { 9999 }
                IsVirtual = [bool]$isVirtual
              }
            }
            ConvertTo-Json -InputObject @($items) -Depth 3 -Compress
            """;

        Logger.Info("네트워크 어댑터 목록 조회 시작");
        var adapters = await _runner.RunJsonAsync<List<NetworkAdapterInfo>>(
            script,
            cancellationToken: cancellationToken) ?? [];
        var result = adapters
            .OrderByDescending(x => x.GetSelectionPriority())
            .ThenBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        Logger.Info($"네트워크 어댑터 목록 조회 완료: {result.Count}개");
        return result;
    }
}
