namespace WindowsShareManager.Models;

public sealed class LocalAccountInfo
{
    public string Name { get; init; } = "";
    public string Kind { get; init; } = "";
    public bool Disabled { get; init; }
    public string DisplayName => $"{Name} ({Kind}{(Disabled ? ", 사용 안 함" : "")})";
}
