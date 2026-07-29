namespace WindowsShareManager.Models;

public sealed class NetworkAdapterInfo
{
    public int InterfaceIndex { get; init; }
    public string InterfaceId { get; init; } = "";
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public string IPv4Address { get; init; } = "";
    public string DefaultGateway { get; init; } = "";
    public string NetworkCategory { get; init; } = "";
    public int InterfaceMetric { get; init; }
    public bool IsVirtual { get; init; }

    public bool HasDefaultGateway => !string.IsNullOrWhiteSpace(DefaultGateway);
    public string AdapterType => IsVirtual ? "가상/VPN" : "물리";
    public string DisplayName =>
        $"{Name} · {IPv4Address} · {ToKoreanCategory(NetworkCategory)} · {AdapterType}";

    public int GetSelectionPriority() =>
        (HasDefaultGateway ? 10000 : 0) +
        (!IsVirtual ? 1000 : 0) -
        Math.Min(Math.Max(InterfaceMetric, 0), 999);

    public static string ToKoreanCategory(string category) => category switch
    {
        "Private" => "개인",
        "Public" => "공용",
        "DomainAuthenticated" => "도메인",
        _ => string.IsNullOrWhiteSpace(category) ? "확인 불가" : category
    };
}
