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
            $advice = [System.Collections.Generic.List[string]]::new()
            if (@($profiles | Where-Object NetworkCategory -eq 'Public').Count -gt 0) {
              $advice.Add('공용 네트워크가 감지됐습니다. 신뢰할 수 있는 내부망이라면 Windows 설정에서 개인 네트워크 사용 여부를 확인하세요.')
            }
            if ($server.Status -ne 'Running') {
              $advice.Add('Server(LanmanServer) 서비스가 실행 중인지 Windows 서비스에서 확인하세요.')
            }
            if ($firewall.Count -eq 0) {
              $advice.Add('Windows 설정 > 네트워크 및 인터넷 > 고급 네트워크 설정 > 고급 공유 설정에서 파일 및 프린터 공유를 확인하세요.')
            }
            if ($advice.Count -eq 0) {
              $advice.Add('기본 공유 관련 상태에서 뚜렷한 문제를 찾지 못했습니다. 접속 계정과 암호로 보호된 공유 설정을 확인하세요.')
            }

            $adviceText = ($advice | ForEach-Object { "- $_" }) -join "`n"
            "네트워크 프로필: $profileText`nServer 서비스: $($server.Status)`n파일 및 프린터 공유 방화벽: $firewallText`n`n확인 안내:`n$adviceText"
            """;
        return await _runner.RunAsync(script, cancellationToken: cancellationToken);
    }
}
