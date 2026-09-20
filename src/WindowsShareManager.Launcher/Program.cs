using System.Runtime.InteropServices;

namespace WindowsShareManager.Launcher;

internal static class Program
{
    private const string AppFileName = "WindowsShareManager.App.exe";
    private const string DownloadUrl = "https://dotnet.microsoft.com/download/dotnet/10.0";

    private const uint MbYesNo = 0x00000004;
    private const uint MbIconWarning = 0x00000030;
    private const int IdYes = 6;
    private const int SwShowNormal = 1;

    [STAThread]
    private static int Main()
    {
        if (!HasDesktopRuntime10X64())
        {
            var result = MessageBoxW(
                IntPtr.Zero,
                "이 프로그램을 실행하려면 Microsoft .NET 10 Desktop Runtime (x64)이 필요합니다.\n\n예: Microsoft 공식 다운로드 페이지 열기\n아니요: 종료",
                "필요한 구성 요소가 없습니다.",
                MbYesNo | MbIconWarning);

            if (result == IdYes)
            {
                ShellExecuteW(IntPtr.Zero, "open", DownloadUrl, null, null, SwShowNormal);
            }

            return 10;
        }

        var appPath = Path.Combine(AppContext.BaseDirectory, AppFileName);
        if (!File.Exists(appPath))
        {
            MessageBoxW(
                IntPtr.Zero,
                $"{AppFileName} 파일을 찾을 수 없습니다. 배포 ZIP을 다시 풀어 주세요.",
                "Windows Share Manager",
                MbIconWarning);
            return 11;
        }

        var resultCode = ShellExecuteW(
            IntPtr.Zero,
            "open",
            appPath,
            null,
            AppContext.BaseDirectory,
            SwShowNormal);

        if (resultCode.ToInt64() <= 32)
        {
            MessageBoxW(
                IntPtr.Zero,
                "Windows Share Manager를 시작하지 못했습니다.",
                "Windows Share Manager",
                MbIconWarning);
            return 12;
        }

        return 0;
    }

    private static bool HasDesktopRuntime10X64()
    {
        var roots = new List<string>();

        var dotnetRootX64 = Environment.GetEnvironmentVariable("DOTNET_ROOT_X64");
        if (!string.IsNullOrWhiteSpace(dotnetRootX64))
        {
            roots.Add(dotnetRootX64);
        }

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (!string.IsNullOrWhiteSpace(programFiles))
        {
            roots.Add(Path.Combine(programFiles, "dotnet"));
        }

        foreach (var root in roots.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var desktopRuntimeRoot = Path.Combine(root, "shared", "Microsoft.WindowsDesktop.App");
            if (!Directory.Exists(desktopRuntimeRoot))
            {
                continue;
            }

            foreach (var directory in Directory.EnumerateDirectories(desktopRuntimeRoot))
            {
                var versionName = Path.GetFileName(directory);
                if (Version.TryParse(versionName, out var version) && version.Major == 10)
                {
                    return true;
                }
            }
        }

        return false;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(
        IntPtr hWnd,
        string lpText,
        string lpCaption,
        uint uType);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr ShellExecuteW(
        IntPtr hwnd,
        string lpOperation,
        string lpFile,
        string? lpParameters,
        string? lpDirectory,
        int nShowCmd);
}
