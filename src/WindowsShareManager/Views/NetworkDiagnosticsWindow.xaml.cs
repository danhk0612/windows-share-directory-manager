using System.Windows;
using System.Windows.Controls;
using WindowsShareManager.Models;
using WindowsShareManager.Services;
using WindowsShareManager.Utilities;

namespace WindowsShareManager.Views;

public partial class NetworkDiagnosticsWindow : Window
{
    private readonly NetworkDiagnosticsService _service;
    private readonly NetworkAdapterInfo _adapter;

    public NetworkDiagnosticsWindow(
        NetworkDiagnosticsService service,
        NetworkAdapterInfo adapter)
    {
        InitializeComponent();
        _service = service;
        _adapter = adapter;
        SelectedAdapterText.Text =
            $"기준 네트워크: {adapter.Name} · {adapter.IPv4Address} · " +
            $"{NetworkAdapterInfo.ToKoreanCategory(adapter.NetworkCategory)} · {adapter.AdapterType}";
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e) => await RefreshAsync();
    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshAsync();

    private async Task RefreshAsync()
    {
        RefreshButton.IsEnabled = false;
        StatusText.Text = "네트워크 공유 상태를 점검하는 중입니다...";
        try
        {
            var items = await _service.GetItemsAsync(_adapter);
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

    private async void ApplyFix_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string fixKey }) return;
        var description = NetworkDiagnosticsService.GetFixDescription(fixKey);
        var result = MessageBox.Show(
            this,
            $"기준 네트워크: {_adapter.Name} ({_adapter.IPv4Address})\n\n" +
            $"{description}\n\n이 설정을 적용하시겠습니까?",
            "네트워크 설정 자동 수정 확인",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);
        if (result != MessageBoxResult.Yes) return;

        RefreshButton.IsEnabled = false;
        ItemsGrid.IsEnabled = false;
        StatusText.Text = "선택한 설정을 적용하는 중입니다...";
        try
        {
            await _service.ApplyFixAsync(fixKey, _adapter);
            await RefreshAsync();
            StatusText.Text = "설정을 적용하고 진단 결과를 다시 확인했습니다.";
        }
        catch (Exception ex)
        {
            Logger.Error($"네트워크 진단 자동 수정 실패: {fixKey}", ex);
            MessageBox.Show(
                this,
                $"설정을 적용하지 못했습니다.\n\n세부 정보: {ex.Message}",
                "자동 수정 실패",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            StatusText.Text = "자동 수정에 실패했습니다. 관련 설정에서 직접 확인하세요.";
        }
        finally
        {
            RefreshButton.IsEnabled = true;
            ItemsGrid.IsEnabled = true;
        }
    }
}
