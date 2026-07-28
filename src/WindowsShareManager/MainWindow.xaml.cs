using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using WindowsShareManager.Models;
using WindowsShareManager.Services;
using WindowsShareManager.Views;

namespace WindowsShareManager;

public partial class MainWindow : Window
{
    private readonly SmbShareService _shareService = new();
    private readonly NtfsPermissionService _ntfsService = new();
    private readonly LocalAccountService _accountService = new();
    private readonly NetworkDiagnosticsService _diagnosticsService = new();

    public MainWindow()
    {
        InitializeComponent();
        ComputerInfoText.Text =
            $@"PC 이름: {Environment.MachineName}    접속 기준: \\{Environment.MachineName}\공유이름";
    }

    private ShareInfo? SelectedShare => SharesGrid.SelectedItem as ShareInfo;

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        if (!ElevationService.IsAdministrator())
        {
            MessageBox.Show(
                "공유와 권한을 관리하려면 관리자 권한이 필요합니다. UAC 요청을 허용한 뒤 다시 실행하세요.",
                "관리자 권한 필요",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            Close();
            return;
        }
        await RefreshSharesAsync();
    }

    private async Task RefreshSharesAsync(string? completedStatus = null)
    {
        try
        {
            IsEnabled = false;
            StatusText.Text = "공유 목록을 불러오는 중입니다...";
            var shares = await _shareService.GetSharesAsync();
            SharesGrid.ItemsSource = shares;
            StatusText.Text = completedStatus ?? $"일반 공유 {shares.Count}개를 불러왔습니다.";
        }
        catch (Exception ex)
        {
            ShowError("공유 목록을 불러오지 못했습니다.", ex);
        }
        finally
        {
            IsEnabled = true;
        }
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshSharesAsync();

    private async void Add_Click(object sender, RoutedEventArgs e)
    {
        var window = new AddShareWindow(_shareService, _ntfsService, _accountService) { Owner = this };
        if (window.ShowDialog() == true)
        {
            var status = window.Outcome == ShareCreationOutcome.SmbCreatedNtfsFailed
                ? "SMB 공유는 생성됐지만 NTFS 권한은 적용되지 않았습니다. 공유를 수정해 권한을 다시 설정하세요."
                : "공유가 생성되었습니다.";
            await RefreshSharesAsync(status);
        }
    }

    private async void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedShare is null) return;
        var window = new EditShareWindow(
            SelectedShare.Name,
            _shareService,
            _ntfsService,
            _accountService) { Owner = this };
        window.ShowDialog();
        await RefreshSharesAsync("공유 설정을 다시 불러왔습니다.");
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        var share = SelectedShare;
        if (share is null) return;

        var result = MessageBox.Show(
            this,
            $"공유 이름: {share.Name}\n실제 폴더: {share.Path}\n네트워크 경로: {share.NetworkPath}\n\n" +
            "Windows 네트워크 공유 설정만 제거됩니다.\n실제 폴더와 내부 파일은 삭제되지 않습니다.",
            "공유 삭제 확인",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);
        if (result != MessageBoxResult.Yes) return;

        IsEnabled = false;
        try
        {
            await _shareService.DeleteShareAsync(share.Name);
            await RefreshSharesAsync("공유 설정이 제거되었습니다. 실제 폴더와 파일은 유지됩니다.");
        }
        catch (Exception ex)
        {
            ShowError($"'{share.Name}' 공유를 제거하지 못했습니다.", ex);
        }
        finally
        {
            IsEnabled = true;
        }
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedShare is null) return;
        Clipboard.SetText(SelectedShare.NetworkPath);
        StatusText.Text = $"네트워크 경로를 복사했습니다: {SelectedShare.NetworkPath}";
    }

    private void Open_Click(object sender, RoutedEventArgs e)
    {
        var share = SelectedShare;
        if (share is null) return;
        if (!Directory.Exists(share.Path))
        {
            MessageBox.Show("실제 폴더가 존재하지 않습니다. 새로 고침 후 상태를 확인하세요.",
                "폴더 없음", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{share.Path}\"") { UseShellExecute = true });
    }

    private async void Diagnostics_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var report = await _diagnosticsService.GetReportAsync();
            MessageBox.Show(
                report + "\n\n이 프로그램은 네트워크 프로필이나 방화벽 설정을 자동으로 변경하지 않습니다.",
                "네트워크 공유 진단",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            ShowError("네트워크 공유 상태를 확인하지 못했습니다.", ex);
        }
    }

    private void SharesGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var enabled = SelectedShare is not null;
        EditButton.IsEnabled = enabled;
        DeleteButton.IsEnabled = enabled;
        CopyButton.IsEnabled = enabled;
        OpenButton.IsEnabled = enabled;
    }

    private static void ShowError(string message, Exception ex)
    {
        Logger.Error(message, ex);
        MessageBox.Show($"{message}\n\n세부 정보: {ex.Message}",
            "Windows Share Manager", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
