using WindowsShareManager.Models;

namespace WindowsShareManager.Services;

public sealed class NetworkDiagnosticsService
{
    private readonly PowerShellRunner _runner = new();

    public async Task<IReadOnlyList<DiagnosticItem>> GetItemsAsync(
        CancellationToken cancellationToken = default)
    {
        const string script = """
            $items = [System.Collections.Generic.List[object]]::new()
            function Add-DiagnosticItem($name, $status, $detail, $tip, $actionKey = '', $actionLabel = '') {
              $items.Add([pscustomobject]@{
                Name = $name
                Status = $status
                Detail = $detail
                Tip = $tip
                ActionKey = $actionKey
                ActionLabel = $actionLabel
              })
            }

            try {
              $profiles = @(Get-NetConnectionProfile)
              if ($profiles.Count -eq 0) {
                Add-DiagnosticItem '네트워크 프로필' '확인 불가' '활성 네트워크 프로필을 찾지 못했습니다.' '네트워크 연결 상태를 확인하세요.' 'NetworkSettings' '네트워크 설정'
              } elseif (@($profiles | Where-Object NetworkCategory -eq 'Public').Count -gt 0) {
                $text = ($profiles | ForEach-Object { "$($_.Name): $($_.NetworkCategory)" }) -join ', '
                Add-DiagnosticItem '네트워크 프로필' '경고' $text '신뢰할 수 있는 내부망이라면 개인 네트워크 사용 여부를 확인하세요.' 'NetworkSettings' '네트워크 설정'
              } else {
                $text = ($profiles | ForEach-Object { "$($_.Name): $($_.NetworkCategory)" }) -join ', '
                Add-DiagnosticItem '네트워크 프로필' '정상' $text '개인 또는 도메인 네트워크 프로필입니다.' 'NetworkSettings' '네트워크 설정'
              }
            } catch {
              Add-DiagnosticItem '네트워크 프로필' '확인 불가' $_.Exception.Message 'Windows 네트워크 설정에서 직접 확인하세요.' 'NetworkSettings' '네트워크 설정'
            }

            try {
              $configs = @(Get-NetIPConfiguration | Where-Object {
                $_.NetAdapter.Status -eq 'Up' -and $_.IPv4Address
              })
              if ($configs.Count -eq 0) {
                Add-DiagnosticItem '활성 IPv4 주소' '실패' '사용 가능한 IPv4 주소를 찾지 못했습니다.' '네트워크 어댑터와 연결 상태를 확인하세요.' 'AdvancedNetworkSettings' '어댑터 설정'
              } else {
                $text = ($configs | ForEach-Object {
                  "$($_.InterfaceAlias): $(($_.IPv4Address.IPAddress) -join ', ')"
                }) -join ' / '
                Add-DiagnosticItem '활성 IPv4 주소' '정상' $text '다른 PC는 이 PC 이름 또는 표시된 주소로 접근할 수 있습니다.' 'AdvancedNetworkSettings' '어댑터 설정'
              }
            } catch {
              Add-DiagnosticItem '활성 IPv4 주소' '확인 불가' $_.Exception.Message '어댑터 설정에서 직접 확인하세요.' 'AdvancedNetworkSettings' '어댑터 설정'
            }

            try {
              $server = Get-CimInstance Win32_Service -Filter "Name='LanmanServer'"
              if ($server.State -eq 'Running') {
                Add-DiagnosticItem 'Server 서비스' '정상' "상태: $($server.State), 시작 유형: $($server.StartMode)" 'Windows 파일 공유 서비스가 실행 중입니다.' 'Services' '서비스 관리'
              } else {
                Add-DiagnosticItem 'Server 서비스' '실패' "상태: $($server.State), 시작 유형: $($server.StartMode)" 'LanmanServer 서비스가 중지되면 SMB 공유에 접근할 수 없습니다.' 'Services' '서비스 관리'
              }
            } catch {
              Add-DiagnosticItem 'Server 서비스' '확인 불가' $_.Exception.Message '서비스 관리에서 Server 서비스를 확인하세요.' 'Services' '서비스 관리'
            }

            try {
              $smb = Get-SmbServerConfiguration
              if ($smb.EnableSMB2Protocol) {
                Add-DiagnosticItem 'SMB 2/3 서버' '정상' 'SMB 2/3 프로토콜 활성화' '현재 Windows 공유에 권장되는 SMB 프로토콜입니다.'
              } else {
                Add-DiagnosticItem 'SMB 2/3 서버' '실패' 'SMB 2/3 프로토콜 비활성화' 'Windows 기능 또는 시스템 정책을 확인하세요.' 'WindowsFeatures' 'Windows 기능'
              }
            } catch {
              Add-DiagnosticItem 'SMB 2/3 서버' '확인 불가' $_.Exception.Message '시스템 정책에 의해 조회가 제한될 수 있습니다.'
            }

            try {
              $listeners = @(Get-NetTCPConnection -LocalPort 445 -State Listen -ErrorAction SilentlyContinue)
              if ($listeners.Count -gt 0) {
                Add-DiagnosticItem 'TCP 445 수신' '정상' 'SMB 포트가 수신 대기 중입니다.' '로컬 SMB 서버가 네트워크 연결을 받을 준비가 됐습니다.'
              } else {
                Add-DiagnosticItem 'TCP 445 수신' '실패' 'SMB 포트의 수신 대기를 찾지 못했습니다.' 'Server 서비스와 방화벽 설정을 확인하세요.' 'Firewall' '고급 방화벽'
              }
            } catch {
              Add-DiagnosticItem 'TCP 445 수신' '확인 불가' $_.Exception.Message '고급 방화벽과 Server 서비스를 확인하세요.' 'Firewall' '고급 방화벽'
            }

            try {
              $sharingRules = @(Get-NetFirewallRule -ErrorAction SilentlyContinue | Where-Object {
                $_.Enabled -eq 'True' -and $_.Direction -eq 'Inbound' -and $_.Action -eq 'Allow' -and (
                  $_.Group -eq '@FirewallAPI.dll,-28502' -or
                  $_.DisplayGroup -in @('File and Printer Sharing', '파일 및 프린터 공유')
                )
              })
              if ($sharingRules.Count -gt 0) {
                Add-DiagnosticItem '파일 및 프린터 공유 방화벽' '정상' "활성 인바운드 허용 규칙: $($sharingRules.Count)개" '적용 프로필이 현재 네트워크와 일치하는지도 확인할 수 있습니다.' 'Firewall' '고급 방화벽'
              } else {
                Add-DiagnosticItem '파일 및 프린터 공유 방화벽' '실패' '활성 인바운드 허용 규칙을 찾지 못했습니다.' '고급 공유 설정 또는 방화벽에서 파일 및 프린터 공유를 확인하세요.' 'Firewall' '고급 방화벽'
              }
            } catch {
              Add-DiagnosticItem '파일 및 프린터 공유 방화벽' '확인 불가' $_.Exception.Message '고급 방화벽에서 직접 확인하세요.' 'Firewall' '고급 방화벽'
            }

            try {
              $discoveryRules = @(Get-NetFirewallRule -ErrorAction SilentlyContinue | Where-Object {
                $_.Enabled -eq 'True' -and $_.Direction -eq 'Inbound' -and $_.Action -eq 'Allow' -and (
                  $_.Group -eq '@FirewallAPI.dll,-32752' -or
                  $_.DisplayGroup -in @('Network Discovery', '네트워크 검색')
                )
              })
              if ($discoveryRules.Count -gt 0) {
                Add-DiagnosticItem '네트워크 검색 방화벽' '정상' "활성 인바운드 허용 규칙: $($discoveryRules.Count)개" '탐색기 네트워크 목록 검색에 사용됩니다.' 'Firewall' '고급 방화벽'
              } else {
                Add-DiagnosticItem '네트워크 검색 방화벽' '경고' '활성 인바운드 허용 규칙을 찾지 못했습니다.' 'UNC 경로 직접 접속은 가능하더라도 탐색기 네트워크 목록에 표시되지 않을 수 있습니다.' 'Firewall' '고급 방화벽'
              }
            } catch {
              Add-DiagnosticItem '네트워크 검색 방화벽' '확인 불가' $_.Exception.Message '고급 방화벽에서 직접 확인하세요.' 'Firewall' '고급 방화벽'
            }

            try {
              $discoveryServices = @(Get-Service -Name fdPHost,FDResPub -ErrorAction SilentlyContinue)
              $running = @($discoveryServices | Where-Object Status -eq 'Running').Count
              if ($discoveryServices.Count -gt 0 -and $running -eq $discoveryServices.Count) {
                Add-DiagnosticItem 'Function Discovery 서비스' '정상' 'fdPHost 및 FDResPub 실행 중' '탐색기 네트워크 검색과 PC 게시에 사용됩니다.' 'Services' '서비스 관리'
              } else {
                $text = ($discoveryServices | ForEach-Object { "$($_.Name): $($_.Status)" }) -join ', '
                Add-DiagnosticItem 'Function Discovery 서비스' '경고' $text '탐색기 네트워크 목록에 PC가 보이지 않을 수 있습니다.' 'Services' '서비스 관리'
              }
            } catch {
              Add-DiagnosticItem 'Function Discovery 서비스' '확인 불가' $_.Exception.Message '서비스 관리에서 직접 확인하세요.' 'Services' '서비스 관리'
            }

            try {
              $shares = @(Get-SmbShare | Where-Object { -not $_.Special })
              Add-DiagnosticItem '일반 SMB 공유' '정상' "현재 일반 공유: $($shares.Count)개" '프로그램의 폴더 공유 탭에서 확인할 수 있습니다.'
            } catch {
              Add-DiagnosticItem '일반 SMB 공유' '확인 불가' $_.Exception.Message 'Server 서비스와 관리자 권한을 확인하세요.'
            }

            try {
              $spooler = Get-Service -Name Spooler
              if ($spooler.Status -eq 'Running') {
                Add-DiagnosticItem 'Print Spooler 서비스' '정상' '인쇄 스풀러 실행 중' '프린터 공유 관리 기능을 사용할 수 있습니다.' 'Printers' '프린터 설정'
              } else {
                Add-DiagnosticItem 'Print Spooler 서비스' '경고' "상태: $($spooler.Status)" '프린터 조회와 공유 변경이 실패할 수 있습니다.' 'Services' '서비스 관리'
              }
            } catch {
              Add-DiagnosticItem 'Print Spooler 서비스' '확인 불가' $_.Exception.Message '서비스 관리에서 Print Spooler를 확인하세요.' 'Services' '서비스 관리'
            }

            Add-DiagnosticItem '암호로 보호된 공유' '확인' '접속 계정과 Windows 공유 정책에 따라 동작합니다.' 'Everyone 권한이 있어도 접속 PC에서 올바른 Windows 계정 자격 증명이 필요할 수 있습니다.' 'AdvancedNetworkSettings' '고급 네트워크 설정'

            ConvertTo-Json -InputObject @($items) -Depth 4 -Compress
            """;

        var items = await _runner.RunJsonAsync<List<DiagnosticItem>>(script, cancellationToken: cancellationToken)
            ?? [];
        Logger.Info($"상세 네트워크 진단 완료: {items.Count}개 항목");
        return items;
    }
}
