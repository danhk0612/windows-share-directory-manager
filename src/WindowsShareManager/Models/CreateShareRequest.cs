namespace WindowsShareManager.Models;

public sealed record CreateShareRequest(
    string Name,
    string Path,
    string Description,
    string AccountName,
    PermissionLevel Permission);
