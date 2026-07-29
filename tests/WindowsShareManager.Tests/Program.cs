using WindowsShareManager.Models;
using WindowsShareManager.Services;
using WindowsShareManager.Utilities;

var failures = new List<string>();

Check(InputValidator.ValidateShareName("공유 문서") is null, "한글 및 공백 공유 이름");
Check(InputValidator.ValidateShareName(new string('가', 80)) is null, "80자 공유 이름 허용");
Check(InputValidator.ValidateShareName(new string('가', 81)) is not null, "81자 공유 이름 차단");
Check(InputValidator.ValidateShareName("bad/name") is not null, "금지 문자 검출");
Check(InputValidator.ValidateShareName("ADMIN$") is not null, "관리 공유형 이름 차단");
Check(InputValidator.ValidateShareName("공유.") is not null, "마침표로 끝나는 공유 이름 차단");
Check(InputValidator.ValidateFolderPath(@"\\server\share") is not null, "UNC 경로 차단");
Check(PermissionLevel.Read.ToSmbAccessRight() == "Read", "읽기 SMB 매핑");
Check(PermissionLevel.Change.ToSmbAccessRight() == "Change", "변경 SMB 매핑");
Check(PermissionLevel.Full.ToSmbAccessRight() == "Full", "전체 SMB 매핑");
Check(PermissionLevel.Read.ToDisplayName() == "읽기 전용", "읽기 표시 매핑");
Check(new LocalAccountInfo
{
    Domain = "BUILTIN",
    AccountName = "Users",
    Kind = "그룹"
}.Name == @"BUILTIN\Users", "실제 계정 도메인 접두사 사용");
Check(SmbShareService.IsSystemShare("ADMIN$", false), "ADMIN$ 시스템 공유 판별");
Check(SmbShareService.IsSystemShare("C$", false), "드라이브 관리 공유 판별");
Check(SmbShareService.IsSystemShare("사용자공유", true), "Special 공유 판별");
Check(!SmbShareService.IsSystemShare("Documents", false), "일반 공유 판별");
Check(ShareCreationOutcome.Success != ShareCreationOutcome.SmbCreatedNtfsFailed, "공유 생성 결과 구분");
Check(new PrinterInfo
{
    Shared = true,
    ShareName = "사무실 프린터"
}.NetworkPath == $@"\\{Environment.MachineName}\사무실 프린터", "공유 프린터 네트워크 경로");
Check(new PrinterInfo { Shared = false, ShareName = "Printer" }.NetworkPath == "",
    "공유하지 않는 프린터 네트워크 경로 비표시");
Check(new PrinterInfo { Shared = true }.SharedDisplay == "공유 중", "프린터 공유 상태 표시");
Check(new DiagnosticItem { ActionKey = "Services" }.HasAction, "진단 설정 작업 표시");
Check(!new DiagnosticItem().HasAction, "진단 설정 작업 없음 표시");
Check(new DiagnosticItem { FixKey = "StartServer" }.CanFix, "진단 자동 수정 표시");
Check(!new DiagnosticItem().CanFix, "진단 자동 수정 없음 표시");
Check(NetworkAdapterInfo.ToKoreanCategory("Private") == "개인", "개인 네트워크 표시");
Check(NetworkAdapterInfo.ToKoreanCategory("Public") == "공용", "공용 네트워크 표시");
Check(new NetworkAdapterInfo
{
    DefaultGateway = "192.168.0.1",
    InterfaceMetric = 20,
    IsVirtual = false
}.GetSelectionPriority() > new NetworkAdapterInfo
{
    DefaultGateway = "",
    InterfaceMetric = 1,
    IsVirtual = false
}.GetSelectionPriority(), "기본 게이트웨이 어댑터 우선 선택");
Check(new NetworkAdapterInfo
{
    DefaultGateway = "192.168.0.1",
    InterfaceMetric = 20,
    IsVirtual = false
}.GetSelectionPriority() > new NetworkAdapterInfo
{
    DefaultGateway = "192.168.0.1",
    InterfaceMetric = 1,
    IsVirtual = true
}.GetSelectionPriority(), "물리 어댑터 우선 선택");

if (failures.Count > 0)
{
    Console.Error.WriteLine("실패한 테스트:");
    foreach (var failure in failures) Console.Error.WriteLine($"- {failure}");
    return 1;
}

Console.WriteLine("모든 단위 테스트를 통과했습니다.");
return 0;

void Check(bool condition, string name)
{
    if (!condition) failures.Add(name);
}
