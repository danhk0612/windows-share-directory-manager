namespace WindowsShareManager.Models;

public sealed class ShareInfo
{
    public string Name { get; init; } = "";
    public string Path { get; init; } = "";
    public string Description { get; init; } = "";
    public bool Special { get; init; }
    public List<SharePermissionInfo> Permissions { get; init; } = [];

    public string NetworkPath => $@"\\{Environment.MachineName}\{Name}";
    public string PrincipalSummary =>
        Permissions.Count == 0 ? "설정 없음" : string.Join(", ", Permissions.Select(x => x.AccountName).Distinct());
    public string PermissionSummary =>
        Permissions.Count == 0
            ? "설정 없음"
            : string.Join(", ", Permissions.Select(x =>
                $"{x.AccessControlDisplay} {x.PermissionDisplay}").Distinct());
    public string Status => Directory.Exists(Path) ? "정상" : "폴더 없음";
}
