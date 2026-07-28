using System.Windows;
using WindowsShareManager.Services;

namespace WindowsShareManager;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        Logger.Initialize();
        Logger.Info($"프로그램 시작. 관리자 권한: {ElevationService.IsAdministrator()}");

        DispatcherUnhandledException += (_, args) =>
        {
            Logger.Error("처리되지 않은 UI 오류", args.Exception);
            MessageBox.Show(
                $"예기치 않은 오류가 발생했습니다.\n\n{args.Exception.Message}\n\n로그에서 세부 내용을 확인할 수 있습니다.",
                "Windows Share Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            args.Handled = true;
        };

        base.OnStartup(e);
    }
}
