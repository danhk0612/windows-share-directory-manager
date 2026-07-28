namespace WindowsShareManager.Models;

public sealed class PrinterInfo
{
    public string Name { get; init; } = "";
    public string DriverName { get; init; } = "";
    public string PortName { get; init; } = "";
    public string PrinterStatus { get; init; } = "";
    public bool Shared { get; init; }
    public string ShareName { get; init; } = "";
    public string Comment { get; init; } = "";
    public string Location { get; init; } = "";
    public string Type { get; init; } = "";

    public string SharedDisplay => Shared ? "공유 중" : "공유 안 함";
    public string NetworkPath => Shared && !string.IsNullOrWhiteSpace(ShareName)
        ? $@"\\{Environment.MachineName}\{ShareName}"
        : "";
}
