namespace WindowsShareManager.Models;

public sealed class DiagnosticItem
{
    public string Name { get; init; } = "";
    public string Status { get; init; } = "";
    public string Detail { get; init; } = "";
    public string Tip { get; init; } = "";
    public string ActionKey { get; init; } = "";
    public string ActionLabel { get; init; } = "";
    public string FixKey { get; init; } = "";
    public string FixLabel { get; init; } = "";
    public bool HasAction => !string.IsNullOrWhiteSpace(ActionKey);
    public bool CanFix => !string.IsNullOrWhiteSpace(FixKey);
}
