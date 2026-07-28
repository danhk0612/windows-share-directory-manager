namespace WindowsShareManager.Services;

public sealed class NetworkDiagnosticsService
{
    private readonly PowerShellRunner _runner = new();

    public async Task<string> GetReportAsync(CancellationToken cancellationToken = default)
    {
        const string script = """
            $profiles = @(Get-NetConnectionProfile | Select-Object Name, NetworkCategory)
            $server = Get-Service -Name LanmanServer
            $firewall = @(Get-NetFirewallRule -ErrorAction SilentlyContinue | Where-Object {
              $_.Enabled -eq 'True' -and (
                $_.Group -eq '@FirewallAPI.dll,-28502' -or
                $_.DisplayGroup -in @('File and Printer Sharing', '파일 및 프린터 공유')
              )
            })

            $profileText = if ($profiles.Count -eq 0) {
              '확인할 수 없음'
            } else {
              ($profiles | ForEach-Object { "$($_.Name): $($_.NetworkCategory)" }) -join ', '
            }
            $firewallText = if ($firewall.Count -gt 0) { '허용 규칙 활성화됨' } else { '활성화된 허용 규칙을 찾지 못함' }

            "네트워크 프로필: $profileText`nServer 서비스: $($server.Status)`n파일 및 프린터 공유 방화벽: $firewallText"
            """;
        return await _runner.RunAsync(script, cancellationToken: cancellationToken);
    }
}
