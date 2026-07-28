using WindowsShareManager.Models;
using WindowsShareManager.Utilities;

var failures = new List<string>();

Check(InputValidator.ValidateShareName("공유 문서") is null, "한글 및 공백 공유 이름");
Check(InputValidator.ValidateShareName("bad/name") is not null, "금지 문자 검출");
Check(InputValidator.ValidateShareName("ADMIN$") is not null, "관리 공유형 이름 차단");
Check(InputValidator.ValidateFolderPath(@"\\server\share") is not null, "UNC 경로 차단");
Check(PermissionLevel.Read.ToSmbAccessRight() == "Read", "읽기 SMB 매핑");
Check(PermissionLevel.Change.ToSmbAccessRight() == "Change", "변경 SMB 매핑");
Check(PermissionLevel.Full.ToSmbAccessRight() == "Full", "전체 SMB 매핑");
Check(PermissionLevel.Read.ToDisplayName() == "읽기 전용", "읽기 표시 매핑");

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
