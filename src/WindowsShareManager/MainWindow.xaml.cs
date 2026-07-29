using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using WindowsShareManager.Models;
using WindowsShareManager.Services;
using WindowsShareManager.Utilities;
using WindowsShareManager.Views;

namespace WindowsShareManager;

public partial class MainWindow : Window
{
    private readonly SmbShareService _shareService = new();
    private readonly NtfsPermissionService _ntfsService = new();
    private readonly LocalAccountService _accountService = new();
    private readonly PrinterShareService _printerService = new();
    private readonly NetworkDiagnosticsService _diagnosticsService = new();
    private readonly NetworkAdapterService _adapterService = new();
    private readonly UserSettingsService _settingsService = new();
    private IReadOnlyList<NetworkAdapterInfo> _allAdapters = [];
    private bool _updatingAdapterSelection;

    public MainWindow()
    {
        InitializeComponent();
        UpdateComputerInfo();
    }

    private IReadOnlyList<ShareInfo> SelectedShares =>
        SharesGrid.SelectedItems.Cast<ShareInfo>().ToList();
    private ShareInfo? SingleSelectedShare =>
        SelectedShares.Count == 1 ? SelectedShares[0] : null;
    private PrinterInfo? SelectedPrinter => PrintersGrid.SelectedItem as PrinterInfo;
    private NetworkAdapterInfo? SelectedAdapter =>
        AdapterComboBox.SelectedItem as NetworkAdapterInfo;

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

        await RefreshAdaptersAsync();
        await RefreshSharesAsync();
        await RefreshPrintersAsync();
    }

    private async Task RefreshAdaptersAsync()
    {
        var currentId = SelectedAdapter?.InterfaceId;
        try
        {
            StatusText.Text = "네트워크 어댑터를 검색하는 중입니다...";
            _allAdapters = await _adapterService.GetAdaptersAsync();
            var preferredId = currentId ?? _settingsService.LoadSelectedAdapterId();
            ApplyAdapterFilter(preferredId);
            StatusText.Text = SelectedAdapter is null
                ? "사용 가능한 IPv4 네트워크 어댑터를 찾지 못했습니다."
                : $"기준 네트워크를 선택했습니다: {SelectedAdapter.Name}";
        }
        catch (Exception ex)
        {
            _allAdapters = [];
            ApplyAdapterFilter(null);
            ShowError("네트워크 어댑터 목록을 불러오지 못했습니다.", ex);
        }
    }

    private void ApplyAdapterFilter(string? preferredId)
    {
        _updatingAdapterSelection = true;
        try
        {
            var showVirtual = ShowVirtualAdaptersCheckBox.IsChecked == true;
            var visible = _allAdapters.Where(x => showVirtual || !x.IsVirtual).ToList();
            if (visible.Count == 0 && _allAdapters.Count > 0 && !showVirtual)
            {
                ShowVirtualAdaptersCheckBox.IsChecked = true;
                visible = _allAdapters.ToList();
            }

            AdapterComboBox.ItemsSource = visible;
            AdapterComboBox.SelectedItem =
                visible.FirstOrDefault(x =>
                    !string.IsNullOrWhiteSpace(preferredId) &&
                    x.InterfaceId.Equals(preferredId, StringComparison.OrdinalIgnoreCase))
                ?? visible.FirstOrDefault();
        }
        finally
        {
            _updatingAdapterSelection = false;
        }

        ApplySelectedAdapter();
    }

    private void ApplySelectedAdapter()
    {
        var adapter = SelectedAdapter;
        DiagnosticsButton.IsEnabled = adapter is not null;
        if (adapter is not null)
        {
            _settingsService.SaveSelectedAdapterId(adapter.InterfaceId);
        }
        UpdateComputerInfo();
    }

    private void UpdateComputerInfo()
    {
        var adapter = SelectedAdapter;
        ComputerInfoText.Text = adapter is null
            ? $@"PC 이름: {Environment.MachineName}    기준 네트워크: 선택되지 않음    접속 기준: \\{Environment.MachineName}\공유이름"
            : $@"PC 이름: {Environment.MachineName}    기준: {adapter.Name} / {adapter.IPv4Address} / " +
              $@"{NetworkAdapterInfo.ToKoreanCategory(adapter.NetworkCategory)}    접속 기준: \\{Environment.MachineName}\공유이름";
    }

    private async Task RefreshSharesAsync(string? completedStatus = null)
    {
        try
        {
            IsEnabled = false;
            StatusText.Text = "폴더 공유 목록을 불러오는 중입니다...";
            var shares = await _shareService.GetSharesAsync();
            SharesGrid.ItemsSource = shares;
            StatusText.Text = completedStatus ?? $"일반 폴더 공유 {shares.Count}개를 불러왔습니다.";
        }
        catch (Exception ex)
        {
            ShowError("폴더 공유 목록을 불러오지 못했습니다.", ex);
        }
        finally
        {
            IsEnabled = true;
        }
    }

    private async Task RefreshPrintersAsync(string? completedStatus = null)
    {
        try
        {
            IsEnabled = false;
            StatusText.Text = "로컬 프린터 목록을 불러오는 중입니다...";
            var printers = await _printerService.GetPrintersAsync();
            PrintersGrid.ItemsSource = printers;
            StatusText.Text = completedStatus ?? $"로컬 프린터 {printers.Count}개를 불러왔습니다.";
        }
        catch (Exception ex)
        {
            ShowError("로컬 프린터 목록을 불러오지 못했습니다. Print Spooler 상태를 확인하세요.", ex);
        }
        finally
        {
            IsEnabled = true;
        }
    }

    private async void RefreshShares_Click(object sender, RoutedEventArgs e) => await RefreshSharesAsync();
    private async void RefreshPrinters_Click(object sender, RoutedEventArgs e) => await RefreshPrintersAsync();
    private async void RefreshAdapters_Click(object sender, RoutedEventArgs e) => await RefreshAdaptersAsync();

    private void AutoSelectAdapter_Click(object sender, RoutedEventArgs e)
    {
        if (AdapterComboBox.Items.Count == 0) return;
        AdapterComboBox.SelectedIndex = 0;
        StatusText.Text = SelectedAdapter is null
            ? "자동으로 선택할 수 있는 네트워크 어댑터가 없습니다."
            : $"기본 경로와 어댑터 유형을 기준으로 자동 선택했습니다: {SelectedAdapter.Name}";
    }

    private async void Add_Click(object sender, RoutedEventArgs e)
    {
        var window = new AddShareWindow(_shareService, _ntfsService, _accountService) { Owner = this };
        if (window.ShowDialog() == true)
        {
            var status = window.Outcome == ShareCreationOutcome.SmbCreatedNtfsFailed
                ? "SMB 공유는 생성됐지만 NTFS 권한은 적용되지 않았습니다. 공유를 수정해 권한을 다시 설정하세요."
                : "폴더 공유가 생성되었습니다.";
            await RefreshSharesAsync(status);
        }
    }

    private async void Edit_Click(object sender, RoutedEventArgs e)
    {
        var share = SingleSelectedShare;
        if (share is null) return;
        var window = new EditShareWindow(
            share.Name,
            _shareService,
            _ntfsService,
            _accountService) { Owner = this };
        window.ShowDialog();
        await RefreshSharesAsync("폴더 공유 설정을 다시 불러왔습니다.");
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        var shares = SelectedShares;
        if (shares.Count == 0) return;

        var names = string.Join("\n", shares.Take(10).Select(x => $"- {x.Name} ({x.Path})"));
        if (shares.Count > 10)
        {
            names += $"\n- 그 외 {shares.Count - 10}개";
        }

        var result = MessageBox.Show(
            this,
            $"선택한 폴더 공유 {shares.Count}개를 제거합니다.\n\n{names}\n\n" +
            "Windows 네트워크 공유 설정만 제거됩니다.\n실제 폴더와 내부 파일은 삭제되지 않습니다.",
            "폴더 공유 일괄 삭제 확인",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);
        if (result != MessageBoxResult.Yes) return;

        IsEnabled = false;
        var failures = new List<string>();
        var removed = 0;
        try
        {
            foreach (var share in shares)
            {
                try
                {
                    if (SmbShareService.IsSystemShare(share.Name, share.Special))
                    {
                        throw new InvalidOperationException("시스템 또는 관리 공유는 삭제할 수 없습니다.");
                    }
                    await _shareService.DeleteShareAsync(share.Name);
                    removed++;
                }
                catch (Exception ex)
                {
                    Logger.Error($"폴더 공유 일괄 삭제 실패: {share.Name}", ex);
                    failures.Add($"{share.Name}: {ex.Message}");
                }
            }
        }
        finally
        {
            IsEnabled = true;
        }

        await RefreshSharesAsync($"폴더 공유 {removed}개를 제거했습니다. 실패: {failures.Count}개");
        if (failures.Count > 0)
        {
            MessageBox.Show(
                this,
                $"일부 폴더 공유를 제거하지 못했습니다.\n\n{string.Join("\n", failures)}",
                "일괄 삭제 일부 실패",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        var share = SingleSelectedShare;
        if (share is null) return;
        Clipboard.SetText(share.NetworkPath);
        StatusText.Text = $"네트워크 경로를 복사했습니다: {share.NetworkPath}";
    }

    private void Open_Click(object sender, RoutedEventArgs e)
    {
        var share = SingleSelectedShare;
        if (share is null) return;
        if (!Directory.Exists(share.Path))
        {
            MessageBox.Show("실제 폴더가 존재하지 않습니다. 새로 고침 후 상태를 확인하세요.",
                "폴더 없음", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{share.Path}\"") { UseShellExecute = true });
    }

    private async void PrinterShare_Click(object sender, RoutedEventArgs e)
    {
        var printer = SelectedPrinter;
        if (printer is null) return;
        var window = new PrinterShareWindow(printer, _printerService) { Owner = this };
        if (window.ShowDialog() == true)
        {
            await RefreshPrintersAsync($"'{printer.Name}' 프린터 공유 설정을 저장했습니다.");
        }
    }

    private async void PrinterDisable_Click(object sender, RoutedEventArgs e)
    {
        var printer = SelectedPrinter;
        if (printer is null || !printer.Shared) return;
        var result = MessageBox.Show(
            this,
            $"프린터: {printer.Name}\n공유 이름: {printer.ShareName}\n\n" +
            "네트워크 프린터 공유만 해제합니다.\n프린터 장치, 드라이버와 포트는 삭제되지 않습니다.",
            "프린터 공유 해제 확인",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);
        if (result != MessageBoxResult.Yes) return;

        IsEnabled = false;
        try
        {
            await _printerService.DisableShareAsync(printer.Name);
        }
        catch (Exception ex)
        {
            ShowError($"'{printer.Name}' 프린터 공유를 해제하지 못했습니다.", ex);
            return;
        }
        finally
        {
            IsEnabled = true;
        }
        await RefreshPrintersAsync($"'{printer.Name}' 프린터 공유를 해제했습니다. 프린터 장치는 유지됩니다.");
    }

    private void PrinterCopy_Click(object sender, RoutedEventArgs e)
    {
        var printer = SelectedPrinter;
        if (printer is null || !printer.Shared) return;
        Clipboard.SetText(printer.NetworkPath);
        StatusText.Text = $"프린터 네트워크 경로를 복사했습니다: {printer.NetworkPath}";
    }

    private void OpenPrintersSettings_Click(object sender, RoutedEventArgs e) =>
        OpenSystemSetting("Printers");

    private async void Diagnostics_Click(object sender, RoutedEventArgs e)
    {
        var adapter = SelectedAdapter;
        if (adapter is null)
        {
            MessageBox.Show(
                this,
                "먼저 기준 네트워크 어댑터를 선택하세요.",
                "네트워크 어댑터 필요",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }
        var window = new NetworkDiagnosticsWindow(_diagnosticsService, adapter) { Owner = this };
        window.ShowDialog();
        await RefreshAdaptersAsync();
    }

    private void AdapterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingAdapterSelection) return;
        ApplySelectedAdapter();
        if (SelectedAdapter is not null)
        {
            StatusText.Text =
                $"기준 네트워크를 변경했습니다: {SelectedAdapter.Name} ({SelectedAdapter.IPv4Address})";
        }
    }

    private void ShowVirtualAdapters_Changed(object sender, RoutedEventArgs e)
    {
        if (_updatingAdapterSelection || !IsLoaded) return;
        ApplyAdapterFilter(SelectedAdapter?.InterfaceId);
    }

    private void SharesGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var count = SelectedShares.Count;
        EditButton.IsEnabled = count == 1;
        DeleteButton.IsEnabled = count > 0;
        CopyButton.IsEnabled = count == 1;
        OpenButton.IsEnabled = count == 1;
    }

    private void PrintersGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var printer = SelectedPrinter;
        PrinterShareButton.IsEnabled = printer is not null;
        PrinterDisableButton.IsEnabled = printer?.Shared == true;
        PrinterCopyButton.IsEnabled = printer?.Shared == true;
    }

    private void OpenSystemSetting(string actionKey)
    {
        try
        {
            SystemSettingsLauncher.Open(actionKey);
        }
        catch (Exception ex)
        {
            ShowError("Windows 설정 화면을 열지 못했습니다.", ex);
        }
    }

    private static void ShowError(string message, Exception ex)
    {
        Logger.Error(message, ex);
        MessageBox.Show($"{message}\n\n세부 정보: {ex.Message}",
            "Windows Share Manager", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
