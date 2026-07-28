namespace WindowsShareManager.Models;

public enum PermissionLevel
{
    Read,
    Change,
    Full
}

public static class PermissionLevelExtensions
{
    public static string ToDisplayName(this PermissionLevel level) => level switch
    {
        PermissionLevel.Read => "읽기 전용",
        PermissionLevel.Change => "읽기 및 쓰기",
        PermissionLevel.Full => "모든 권한",
        _ => "알 수 없음"
    };

    public static string ToSmbAccessRight(this PermissionLevel level) => level switch
    {
        PermissionLevel.Read => "Read",
        PermissionLevel.Change => "Change",
        PermissionLevel.Full => "Full",
        _ => throw new ArgumentOutOfRangeException(nameof(level))
    };

    public static PermissionLevel FromSmbAccessRight(string value) =>
        value.Equals("Full", StringComparison.OrdinalIgnoreCase)
            ? PermissionLevel.Full
            : value.Equals("Change", StringComparison.OrdinalIgnoreCase)
                ? PermissionLevel.Change
                : PermissionLevel.Read;
}
