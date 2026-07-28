using System.Diagnostics;

namespace WindowsShareManager.Utilities;

public static class SystemSettingsLauncher
{
    public static void Open(string actionKey)
    {
        var target = actionKey switch
        {
            "NetworkSettings" => "ms-settings:network-status",
            "AdvancedNetworkSettings" => "ms-settings:network-advancedsettings",
            "Printers" => "ms-settings:printers",
            "WindowsFeatures" => "optionalfeatures.exe",
            "Services" => "services.msc",
            "Firewall" => "wf.msc",
            _ => throw new ArgumentException("지원하지 않는 설정 화면입니다.", nameof(actionKey))
        };

        Process.Start(new ProcessStartInfo(target)
        {
            UseShellExecute = true
        });
    }
}
