using System.Windows;
using System.Windows.Controls;
using WindowsShareManager.Services;
using WindowsShareManager.Utilities;

namespace WindowsShareManager.Views;

public partial class NetworkDiagnosticsWindow : Window
{
    private readonly NetworkDiagnosticsService _service;

    public NetworkDiagnosticsWindow(NetworkDiagnosticsService service)
    {
        InitializeComponent();
        _service = service;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e) => await RefreshAsync();
    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshAsync();

    private async Task RefreshAsync()
    {
        RefreshButton.IsEnabled = false;
        StatusText.Text = "네트워크 공유 상태를 점검하는 중입니다...";
        try
        {
            var items = await _service.GetItemsAsync();
            ItemsGrid.ItemsSource = items;
            var failures = items.Count(x => x.Status == "실패");
            var warnings = items.Count(x => x.Status == "경고");
            StatusText.Text = $"진단 완료: 실패 {failures}개, 경고 {warnings}개, 전체 {items.Count}개";
        }
        catch (Exception ex)
        {
            Logger.Error("상세 네트워크 진단 실패", ex);
            MessageBox.Show(
                this,
                $"네트워크 공유 상태를 점검하지 못했습니다.\n\n세부 정보: {ex.Message}",
                "네트워크 진단 실패",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            StatusText.Text = "진단에 실패했습니다.";
        }
        finally
        {
            RefreshButton.IsEnabled = true;
        }
    }

    private void OpenSetting_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string actionKey }) return;
        try
        {
            SystemSettingsLauncher.Open(actionKey);
        }
        catch (Exception ex)
        {
            Logger.Error($"설정 화면 실행 실패: {actionKey}", ex);
            MessageBox.Show(
                this,
                $"관련 설정 화면을 열지 못했습니다.\n\n세부 정보: {ex.Message}",
                "설정 실행 실패",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}
