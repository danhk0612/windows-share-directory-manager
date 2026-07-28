namespace WindowsShareManager.Models;

public sealed class SharePermissionInfo
{
    public string AccountName { get; init; } = "";
    public string AccessControlType { get; init; } = "";
    public string AccessRight { get; init; } = "";
    public string AccessControlDisplay =>
        AccessControlType.Equals("Deny", StringComparison.OrdinalIgnoreCase) ? "거부" : "허용";
    public string PermissionDisplay => PermissionLevelExtensions
        .FromSmbAccessRight(AccessRight)
        .ToDisplayName();
}
