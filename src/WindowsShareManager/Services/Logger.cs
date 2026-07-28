using System.IO;
using System.Text;

namespace WindowsShareManager.Services;

public static class Logger
{
    private static readonly object Gate = new();
    private static string? _logFile;

    public static void Initialize()
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WindowsShareManager",
            "Logs");
        Directory.CreateDirectory(directory);
        _logFile = Path.Combine(directory, $"WindowsShareManager-{DateTime.Now:yyyyMMdd}.log");

        var oldFiles = new DirectoryInfo(directory)
            .GetFiles("WindowsShareManager-*.log")
            .OrderByDescending(x => x.LastWriteTimeUtc)
            .Skip(10);
        foreach (var file in oldFiles)
        {
            try { file.Delete(); } catch { /* 로그 정리는 실행을 방해하지 않는다. */ }
        }
    }

    public static void Info(string message) => Write("INFO", message, null);
    public static void Error(string message, Exception exception) => Write("ERROR", message, exception);

    private static void Write(string level, string message, Exception? exception)
    {
        try
        {
            lock (Gate)
            {
                _logFile ??= Path.Combine(Path.GetTempPath(), "WindowsShareManager.log");
                var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}";
                if (exception is not null)
                {
                    line += $"{Environment.NewLine}{exception}";
                }
                File.AppendAllText(_logFile, line + Environment.NewLine, new UTF8Encoding(false));
            }
        }
        catch
        {
            // 로깅 실패가 공유 관리 작업을 중단시키지 않도록 한다.
        }
    }
}
