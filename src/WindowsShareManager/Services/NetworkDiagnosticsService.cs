using WindowsShareManager.Models;

namespace WindowsShareManager.Services;

public sealed class NetworkDiagnosticsService
{
    private readonly PowerShellRunner _runner = new();

    public async Task<IReadOnlyList<DiagnosticItem>> GetItemsAsync(
        NetworkAdapterInfo adapter,
        CancellationToken cancellationToken = default)
    {
        const string script = """
            $items = [System.Collections.Generic.List[object]]::new()
            $interfaceIndex = [int]$env:WSM_INTERFACE_INDEX
            $selectedIp = $env:WSM_IPV4_ADDRESS

            function Add-DiagnosticItem(
              $name, $status, $detail, $tip,
              $actionKey = '', $actionLabel = '',
              $fixKey = '', $fixLabel = ''
            ) {
              $items.Add([pscustomobject]@{
                Name = $name
                Status = $status
                Detail = $detail
                Tip = $tip
                ActionKey = $actionKey
                ActionLabel = $actionLabel
                FixKey = $fixKey
                FixLabel = $fixLabel
              })
            }

            function Test-RuleAppliesToProfile($rule, $profileName) {
              $profiles = @([string]$rule.Profile -split ',\s*')
              return $profiles -contains 'Any' -or $profiles -contains $profileName
            }

            try {
              $profile = Get-NetConnectionProfile -InterfaceIndex $interfaceIndex -ErrorAction Stop
              $category = [string]$profile.NetworkCategory
              if ($category -eq 'Public') {
                Add-DiagnosticItem '선택 네트워크 프로필' '경고' "$($profile.Name): $category" `
                  '신뢰할 수 있는 내부망인 경우에만 개인 네트워크로 변경하세요.' `
                  'NetworkSettings' '네트워크 설정' 'SetPrivateProfile' '개인으로 변경'
              } elseif ($category -in @('Private', 'DomainAuthenticated')) {
                Add-DiagnosticItem '선택 네트워크 프로필' '정상' "$($profile.Name): $category" `
                  '개인 또는 도메인 네트워크 프로필입니다.' 'NetworkSettings' '네트워크 설정'
              } else {
                Add-DiagnosticItem '선택 네트워크 프로필' '확인 불가' "$($profile.Name): $category" `
                  'Windows 네트워크 설정에서 프로필을 확인하세요.' 'NetworkSettings' '네트워크 설정'
              }
              $firewallProfile = switch ($category) {
                'Private' { 'Private' }
                'Public' { 'Public' }
                'DomainAuthenticated' { 'Domain' }
                default { '' }
              }
            } catch {
              $profile = $null
              $firewallProfile = ''
              Add-DiagnosticItem '선택 네트워크 프로필' '확인 불가' $_.Exception.Message `
                '선택한 어댑터의 연결 상태를 확인하세요.' 'NetworkSettings' '네트워크 설정'
            }

            try {
              $config = Get-NetIPConfiguration -InterfaceIndex $interfaceIndex -ErrorAction Stop
              $addresses = @($config.IPv4Address.IPAddress)
              if ($addresses.Count -eq 0) {
                Add-DiagnosticItem '선택 어댑터 IPv4 주소' '실패' 'IPv4 주소가 없습니다.' `
                  '어댑터의 IPv4 또는 DHCP 설정을 확인하세요.' 'AdvancedNetworkSettings' '어댑터 설정'
              } else {
                $gatewayItem = $config.IPv4DefaultGateway | Select-Object -First 1
                $gateway = [string]$gatewayItem.NextHop
                $detail = "$($config.InterfaceAlias): $($addresses -join ', ')"
                if ($gateway) { $detail += ", 게이트웨이: $gateway" }
                Add-DiagnosticItem '선택 어댑터 IPv4 주소' '정상' $detail `
                  '다른 PC는 이 PC 이름 또는 표시된 주소로 접근할 수 있습니다.' `
                  'AdvancedNetworkSettings' '어댑터 설정'
              }
            } catch {
              Add-DiagnosticItem '선택 어댑터 IPv4 주소' '확인 불가' $_.Exception.Message `
                '어댑터 설정에서 직접 확인하세요.' 'AdvancedNetworkSettings' '어댑터 설정'
            }

            try {
              $server = Get-CimInstance Win32_Service -Filter "Name='LanmanServer'"
              if ($server.State -eq 'Running') {
                Add-DiagnosticItem 'Server 서비스' '정상' `
                  "상태: $($server.State), 시작 유형: $($server.StartMode)" `
                  'Windows 파일 공유 서비스가 실행 중입니다.' 'Services' '서비스 관리'
              } else {
                Add-DiagnosticItem 'Server 서비스' '실패' `
                  "상태: $($server.State), 시작 유형: $($server.StartMode)" `
                  '서비스를 시작할 수 있습니다. 시작 유형은 변경하지 않습니다.' `
                  'Services' '서비스 관리' 'StartServer' '서비스 시작'
              }
            } catch {
              Add-DiagnosticItem 'Server 서비스' '확인 불가' $_.Exception.Message `
                '서비스 관리에서 Server 서비스를 확인하세요.' 'Services' '서비스 관리'
            }

            try {
              $smb = Get-SmbServerConfiguration
              if ($smb.EnableSMB2Protocol) {
                Add-DiagnosticItem 'SMB 2/3 서버' '정상' 'SMB 2/3 프로토콜 활성화' `
                  '현재 Windows 공유에 권장되는 SMB 프로토콜입니다.'
              } else {
                Add-DiagnosticItem 'SMB 2/3 서버' '실패' 'SMB 2/3 프로토콜 비활성화' `
                  '시스템 전체와 정책에 영향을 주므로 프로그램에서 자동 변경하지 않습니다.' `
                  'WindowsFeatures' 'Windows 기능'
              }
            } catch {
              Add-DiagnosticItem 'SMB 2/3 서버' '확인 불가' $_.Exception.Message `
                '시스템 정책에 의해 조회가 제한될 수 있습니다.'
            }

            try {
              $listeners = @(Get-NetTCPConnection -LocalPort 445 -State Listen -ErrorAction SilentlyContinue |
                Where-Object { $_.LocalAddress -in @($selectedIp, '0.0.0.0', '::') })
              if ($listeners.Count -gt 0) {
                $addresses = ($listeners.LocalAddress | Select-Object -Unique) -join ', '
                Add-DiagnosticItem '선택 어댑터 TCP 445 수신' '정상' "수신 주소: $addresses" `
                  '선택한 어댑터에서 SMB 연결을 받을 수 있는 수신 상태입니다.'
              } else {
                Add-DiagnosticItem '선택 어댑터 TCP 445 수신' '실패' `
                  '선택한 IPv4 또는 전체 인터페이스의 SMB 수신 대기를 찾지 못했습니다.' `
                  'Server 서비스와 방화벽을 확인하세요.' 'Firewall' '고급 방화벽'
              }
            } catch {
              Add-DiagnosticItem '선택 어댑터 TCP 445 수신' '확인 불가' $_.Exception.Message `
                '고급 방화벽과 Server 서비스를 확인하세요.' 'Firewall' '고급 방화벽'
            }

            try {
              $sharingRules = @(Get-NetFirewallRule -ErrorAction SilentlyContinue | Where-Object {
                $_.Enabled -eq 'True' -and $_.Direction -eq 'Inbound' -and $_.Action -eq 'Allow' -and (
                  $_.Group -eq '@FirewallAPI.dll,-28502' -or
                  $_.DisplayGroup -in @('File and Printer Sharing', '파일 및 프린터 공유')
                ) -and (!$firewallProfile -or (Test-RuleAppliesToProfile $_ $firewallProfile))
              })
              if (!$firewallProfile) {
                Add-DiagnosticItem '파일 및 프린터 공유 방화벽' '확인 불가' `
                  '선택한 네트워크의 방화벽 프로필을 확인할 수 없습니다.' `
                  '네트워크 프로필을 확인한 뒤 다시 진단하세요.' 'Firewall' '고급 방화벽'
              } elseif ($sharingRules.Count -gt 0) {
                Add-DiagnosticItem '파일 및 프린터 공유 방화벽' '정상' `
                  "$firewallProfile 프로필 활성 인바운드 허용 규칙: $($sharingRules.Count)개" `
                  '선택한 어댑터의 현재 네트워크 프로필을 기준으로 확인했습니다.' `
                  'Firewall' '고급 방화벽'
              } else {
                Add-DiagnosticItem '파일 및 프린터 공유 방화벽' '실패' `
                  "$firewallProfile 프로필의 활성 인바운드 허용 규칙을 찾지 못했습니다." `
                  '선택한 프로필에 해당하는 기본 Windows 방화벽 규칙을 활성화할 수 있습니다.' `
                  'Firewall' '고급 방화벽' 'EnableFileSharingFirewall' '규칙 활성화'
              }
            } catch {
              Add-DiagnosticItem '파일 및 프린터 공유 방화벽' '확인 불가' $_.Exception.Message `
                '고급 방화벽에서 직접 확인하세요.' 'Firewall' '고급 방화벽'
            }

            try {
              $discoveryRules = @(Get-NetFirewallRule -ErrorAction SilentlyContinue | Where-Object {
                $_.Enabled -eq 'True' -and $_.Direction -eq 'Inbound' -and $_.Action -eq 'Allow' -and (
                  $_.Group -eq '@FirewallAPI.dll,-32752' -or
                  $_.DisplayGroup -in @('Network Discovery', '네트워크 검색')
                ) -and (!$firewallProfile -or (Test-RuleAppliesToProfile $_ $firewallProfile))
              })
              if (!$firewallProfile) {
                Add-DiagnosticItem '네트워크 검색 방화벽' '확인 불가' `
                  '선택한 네트워크의 방화벽 프로필을 확인할 수 없습니다.' `
                  '네트워크 프로필을 확인한 뒤 다시 진단하세요.' 'Firewall' '고급 방화벽'
              } elseif ($discoveryRules.Count -gt 0) {
                Add-DiagnosticItem '네트워크 검색 방화벽' '정상' `
                  "$firewallProfile 프로필 활성 인바운드 허용 규칙: $($discoveryRules.Count)개" `
                  '탐색기 네트워크 목록 검색에 사용됩니다.' 'Firewall' '고급 방화벽'
              } else {
                Add-DiagnosticItem '네트워크 검색 방화벽' '경고' `
                  "$firewallProfile 프로필의 활성 인바운드 허용 규칙을 찾지 못했습니다." `
                  'UNC 직접 접속은 가능하더라도 탐색기 네트워크 목록에 표시되지 않을 수 있습니다.' `
                  'Firewall' '고급 방화벽' 'EnableNetworkDiscoveryFirewall' '규칙 활성화'
              }
            } catch {
              Add-DiagnosticItem '네트워크 검색 방화벽' '확인 불가' $_.Exception.Message `
                '고급 방화벽에서 직접 확인하세요.' 'Firewall' '고급 방화벽'
            }

            try {
              $discoveryServices = @(Get-Service -Name fdPHost,FDResPub -ErrorAction SilentlyContinue)
              $running = @($discoveryServices | Where-Object Status -eq 'Running').Count
              if ($discoveryServices.Count -gt 0 -and $running -eq $discoveryServices.Count) {
                Add-DiagnosticItem 'Function Discovery 서비스' '정상' `
                  'fdPHost 및 FDResPub 실행 중' '탐색기 네트워크 검색과 PC 게시에 사용됩니다.' `
                  'Services' '서비스 관리'
              } else {
                $text = ($discoveryServices | ForEach-Object { "$($_.Name): $($_.Status)" }) -join ', '
                Add-DiagnosticItem 'Function Discovery 서비스' '경고' $text `
                  '중지된 서비스를 시작할 수 있습니다. 시작 유형은 변경하지 않습니다.' `
                  'Services' '서비스 관리' 'StartDiscoveryServices' '서비스 시작'
              }
            } catch {
              Add-DiagnosticItem 'Function Discovery 서비스' '확인 불가' $_.Exception.Message `
                '서비스 관리에서 직접 확인하세요.' 'Services' '서비스 관리'
            }

            try {
              $shares = @(Get-SmbShare | Where-Object { -not $_.Special })
              Add-DiagnosticItem '일반 SMB 공유' '정상' "현재 일반 공유: $($shares.Count)개" `
                '프로그램의 폴더 공유 탭에서 확인할 수 있습니다.'
            } catch {
              Add-DiagnosticItem '일반 SMB 공유' '확인 불가' $_.Exception.Message `
                'Server 서비스와 관리자 권한을 확인하세요.'
            }

            try {
              $spooler = Get-Service -Name Spooler
              if ($spooler.Status -eq 'Running') {
                Add-DiagnosticItem 'Print Spooler 서비스' '정상' '인쇄 스풀러 실행 중' `
                  '프린터 공유 관리 기능을 사용할 수 있습니다.' 'Printers' '프린터 설정'
              } else {
                Add-DiagnosticItem 'Print Spooler 서비스' '경고' "상태: $($spooler.Status)" `
                  '서비스를 시작할 수 있습니다. 시작 유형은 변경하지 않습니다.' `
                  'Services' '서비스 관리' 'StartSpooler' '서비스 시작'
              }
            } catch {
              Add-DiagnosticItem 'Print Spooler 서비스' '확인 불가' $_.Exception.Message `
                '서비스 관리에서 Print Spooler를 확인하세요.' 'Services' '서비스 관리'
            }

            Add-DiagnosticItem '암호로 보호된 공유' '확인' `
              '접속 계정과 Windows 공유 정책에 따라 동작합니다.' `
              '계정 정책에 영향을 주므로 자동 변경하지 않습니다. 올바른 Windows 자격 증명을 사용하세요.' `
              'AdvancedNetworkSettings' '고급 네트워크 설정'

            ConvertTo-Json -InputObject @($items) -Depth 4 -Compress
            """;

        var items = await _runner.RunJsonAsync<List<DiagnosticItem>>(
            script,
            ToEnvironment(adapter),
            cancellationToken) ?? [];
        Logger.Info(
            $"상세 네트워크 진단 완료: {items.Count}개 항목, 어댑터: {adapter.Name} ({adapter.IPv4Address})");
        return items;
    }

    public async Task ApplyFixAsync(
        string fixKey,
        NetworkAdapterInfo adapter,
        CancellationToken cancellationToken = default)
    {
        var script = fixKey switch
        {
            "SetPrivateProfile" => """
                $index = [int]$env:WSM_INTERFACE_INDEX
                $profile = Get-NetConnectionProfile -InterfaceIndex $index
                if ($profile.NetworkCategory -ne 'Public') {
                  throw '현재 네트워크 프로필이 공용이 아닙니다.'
                }
                Set-NetConnectionProfile -InterfaceIndex $index -NetworkCategory Private
                $updated = Get-NetConnectionProfile -InterfaceIndex $index
                if ($updated.NetworkCategory -ne 'Private') {
                  throw '네트워크 프로필 변경 후 개인 상태를 확인하지 못했습니다.'
                }
                """,
            "StartServer" => ServiceStartScript("LanmanServer"),
            "StartSpooler" => ServiceStartScript("Spooler"),
            "StartDiscoveryServices" => """
                Get-Service -Name fdPHost,FDResPub -ErrorAction Stop |
                  Where-Object Status -ne 'Running' |
                  Start-Service
                $stopped = @(Get-Service -Name fdPHost,FDResPub |
                  Where-Object Status -ne 'Running')
                if ($stopped.Count -gt 0) {
                  throw "시작되지 않은 서비스: $($stopped.Name -join ', ')"
                }
                """,
            "EnableFileSharingFirewall" => FirewallEnableScript(
                "@FirewallAPI.dll,-28502",
                "File and Printer Sharing",
                "파일 및 프린터 공유"),
            "EnableNetworkDiscoveryFirewall" => FirewallEnableScript(
                "@FirewallAPI.dll,-32752",
                "Network Discovery",
                "네트워크 검색"),
            _ => throw new ArgumentException("지원하지 않는 자동 수정 작업입니다.", nameof(fixKey))
        };

        Logger.Info($"네트워크 진단 자동 수정 시작: {fixKey}, 어댑터: {adapter.Name}");
        await _runner.RunAsync(script, ToEnvironment(adapter), cancellationToken);
        Logger.Info($"네트워크 진단 자동 수정 완료: {fixKey}, 어댑터: {adapter.Name}");
    }

    public static string GetFixDescription(string fixKey) => fixKey switch
    {
        "SetPrivateProfile" =>
            "선택한 연결의 네트워크 프로필을 공용에서 개인으로 변경합니다. 신뢰할 수 있는 내부 네트워크에서만 적용하세요. Windows가 다른 어댑터를 같은 네트워크 프로필로 식별한 경우 함께 변경될 수 있습니다.",
        "StartServer" =>
            "Windows 파일 공유에 필요한 Server 서비스를 시작합니다. 서비스 시작 유형은 변경하지 않습니다.",
        "StartSpooler" =>
            "프린터 공유 관리에 필요한 Print Spooler 서비스를 시작합니다. 서비스 시작 유형은 변경하지 않습니다.",
        "StartDiscoveryServices" =>
            "네트워크 검색에 필요한 Function Discovery 서비스를 시작합니다. 서비스 시작 유형은 변경하지 않습니다.",
        "EnableFileSharingFirewall" =>
            "선택 어댑터의 현재 프로필에 적용되는 Windows 기본 파일 및 프린터 공유 인바운드 규칙을 활성화합니다. 같은 프로필을 사용하는 다른 어댑터에도 적용됩니다.",
        "EnableNetworkDiscoveryFirewall" =>
            "선택 어댑터의 현재 프로필에 적용되는 Windows 기본 네트워크 검색 인바운드 규칙을 활성화합니다. 같은 프로필을 사용하는 다른 어댑터에도 적용됩니다.",
        _ => "선택한 진단 항목의 설정을 변경합니다."
    };

    private static Dictionary<string, string?> ToEnvironment(NetworkAdapterInfo adapter) => new()
    {
        ["WSM_INTERFACE_INDEX"] = adapter.InterfaceIndex.ToString(),
        ["WSM_INTERFACE_NAME"] = adapter.Name,
        ["WSM_IPV4_ADDRESS"] = adapter.IPv4Address,
        ["WSM_NETWORK_CATEGORY"] = adapter.NetworkCategory
    };

    private static string ServiceStartScript(string serviceName) => $$"""
        $service = Get-Service -Name '{{serviceName}}' -ErrorAction Stop
        if ($service.Status -ne 'Running') {
          Start-Service -Name '{{serviceName}}'
        }
        if ((Get-Service -Name '{{serviceName}}').Status -ne 'Running') {
          throw '{{serviceName}} 서비스를 시작하지 못했습니다.'
        }
        """;

    private static string FirewallEnableScript(
        string resourceGroup,
        string englishGroup,
        string koreanGroup) => $$"""
        $currentProfile = Get-NetConnectionProfile -InterfaceIndex ([int]$env:WSM_INTERFACE_INDEX)
        $profileName = switch ([string]$currentProfile.NetworkCategory) {
          'Private' { 'Private' }
          'Public' { 'Public' }
          'DomainAuthenticated' { 'Domain' }
          default { throw '선택한 네트워크의 방화벽 프로필을 확인할 수 없습니다.' }
        }
        $rules = @(Get-NetFirewallRule -ErrorAction Stop | Where-Object {
          $_.Direction -eq 'Inbound' -and $_.Action -eq 'Allow' -and (
            $_.Group -eq '{{resourceGroup}}' -or
            $_.DisplayGroup -in @('{{englishGroup}}', '{{koreanGroup}}')
          ) -and (
            @([string]$_.Profile -split ',\s*') -contains 'Any' -or
            @([string]$_.Profile -split ',\s*') -contains $profileName
          )
        })
        if ($rules.Count -eq 0) {
          throw "$profileName 프로필에 적용되는 Windows 기본 방화벽 규칙을 찾지 못했습니다."
        }
        $rules | Enable-NetFirewallRule
        $disabled = @($rules | Get-NetFirewallRule | Where-Object Enabled -ne 'True')
        if ($disabled.Count -gt 0) {
          throw '방화벽 규칙 활성화 후 적용 상태를 확인하지 못했습니다.'
        }
        """;
}
