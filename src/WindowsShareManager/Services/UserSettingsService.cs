using System.IO;
using System.Text.Json;

namespace WindowsShareManager.Services;

public sealed class UserSettingsService
{
    private readonly string _settingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WindowsShareManager",
        "settings.json");

    public string LoadSelectedAdapterId()
    {
        try
        {
            if (!File.Exists(_settingsPath)) return "";
            var settings = JsonSerializer.Deserialize<UserSettings>(
                File.ReadAllText(_settingsPath));
            return settings?.SelectedAdapterId ?? "";
        }
        catch (Exception ex)
        {
            Logger.Error("네트워크 어댑터 선택 설정을 읽지 못했습니다.", ex);
            return "";
        }
    }

    public void SaveSelectedAdapterId(string adapterId)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
            File.WriteAllText(
                _settingsPath,
                JsonSerializer.Serialize(new UserSettings
                {
                    SelectedAdapterId = adapterId
                }));
        }
        catch (Exception ex)
        {
            Logger.Error("네트워크 어댑터 선택 설정을 저장하지 못했습니다.", ex);
        }
    }

    private sealed class UserSettings
    {
        public string SelectedAdapterId { get; init; } = "";
    }
}
