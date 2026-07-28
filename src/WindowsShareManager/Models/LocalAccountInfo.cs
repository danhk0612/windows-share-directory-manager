namespace WindowsShareManager.Models;

public sealed class LocalAccountInfo
{
    public string Domain { get; init; } = "";
    public string AccountName { get; init; } = "";
    public string Kind { get; init; } = "";
    public bool Disabled { get; init; }
    public string Name => string.IsNullOrWhiteSpace(Domain)
        ? AccountName
        : $@"{Domain}\{AccountName}";
    public string DisplayName => $"{Name} ({Kind}{(Disabled ? ", 사용 안 함" : "")})";
}
