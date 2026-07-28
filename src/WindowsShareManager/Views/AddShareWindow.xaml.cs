using Microsoft.Win32;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using WindowsShareManager.Models;
using WindowsShareManager.Services;
using WindowsShareManager.Utilities;

namespace WindowsShareManager.Views;

public partial class AddShareWindow : Window
{
    private readonly SmbShareService _shareService;
    private readonly NtfsPermissionService _ntfsService;
    private readonly LocalAccountService _accountService;
    private bool _shareNameManuallyEdited;

    public ShareCreationOutcome Outcome { get; private set; }

    public AddShareWindow(
        SmbShareService shareService,
        NtfsPermissionService ntfsService,
        LocalAccountService accountService)
    {
        InitializeComponent();
        _shareService = shareService;
        _ntfsService = ntfsService;
        _accountService = accountService;
        NameTextBox.TextChanged += (_, _) =>
        {
            if (NameTextBox.IsKeyboardFocused) _shareNameManuallyEdited = true;
        };
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            AccountComboBox.ItemsSource = await _accountService.GetAccountsAsync();
        }
        catch (Exception ex)
        {
            Logger.Error("로컬 계정 목록 조회 실패", ex);
            ValidationText.Text = "로컬 계정 목록을 불러오지 못했습니다. 계정 이름은 직접 입력할 수 있습니다.";
        }
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "공유할 로컬 폴더 선택",
            Multiselect = false
        };
        if (dialog.ShowDialog(this) == true)
        {
            PathTextBox.Text = dialog.FolderName;
        }
    }

    private void PathTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_shareNameManuallyEdited || string.IsNullOrWhiteSpace(PathTextBox.Text)) return;
        NameTextBox.Text = Path.GetFileName(PathTextBox.Text.TrimEnd(Path.DirectorySeparatorChar));
    }

    private void AccountMode_Changed(object sender, RoutedEventArgs e)
    {
        if (AccountComboBox is not null)
            AccountComboBox.IsEnabled = SpecificRadio?.IsChecked == true;
    }

    private async void Create_Click(object sender, RoutedEventArgs e)
    {
        ValidationText.Text = "";
        var path = PathTextBox.Text.Trim();
        var name = NameTextBox.Text.Trim();
        var account = GetAccountName();

        var validation = InputValidator.ValidateFolderPath(path)
            ?? InputValidator.ValidateShareName(name);
        if (validation is not null)
        {
            ValidationText.Text = validation;
            return;
        }
        if (string.IsNullOrWhiteSpace(account) || !_accountService.IsValidAccount(account))
        {
            ValidationText.Text = "Windows에서 확인할 수 있는 유효한 사용자 또는 그룹을 선택하세요.";
            return;
        }
        if (await _shareService.GetShareAsync(name) is not null)
        {
            ValidationText.Text = "같은 이름의 Windows 공유가 이미 존재합니다.";
            return;
        }

        var permission = GetPermission();
        var request = new CreateShareRequest(name, path, DescriptionTextBox.Text.Trim(), account, permission);
        var networkPath = $@"\\{Environment.MachineName}\{name}";
        var confirm = MessageBox.Show(
            $"공유 이름: {name}\n폴더 경로: {path}\n접근 대상: {account}\n" +
            $"권한: {permission.ToDisplayName()}\n네트워크 경로: {networkPath}\n\n이 설정으로 공유를 생성하시겠습니까?",
            "공유 생성 확인",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        CreateButton.IsEnabled = false;
        try
        {
            await _shareService.CreateShareAsync(request);
        }
        catch (Exception ex)
        {
            ShowFailure("SMB 공유 생성 단계에서 실패했습니다. 공유가 생성되지 않았습니다.", ex);
            CreateButton.IsEnabled = true;
            return;
        }

        try
        {
            _ntfsService.SetPermission(path, account, permission);
        }
        catch (Exception ex)
        {
            ShowFailure(
                "SMB 공유는 생성되었지만 NTFS 권한 설정 단계에서 실패했습니다. " +
                "현재 공유 설정은 유지되어 있으므로 메인 화면에서 상태를 확인하세요.", ex);
            Outcome = ShareCreationOutcome.SmbCreatedNtfsFailed;
            DialogResult = true;
            return;
        }

        Outcome = ShareCreationOutcome.Success;
        DialogResult = true;
    }

    private string GetAccountName()
    {
        if (EveryoneRadio.IsChecked == true)
            return _accountService.EveryoneAccountName;
        if (AccountComboBox.SelectedItem is LocalAccountInfo selected)
            return selected.Name;
        return AccountComboBox.Text.Trim();
    }

    private PermissionLevel GetPermission()
    {
        var tag = (PermissionComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        return Enum.TryParse<PermissionLevel>(tag, out var value) ? value : PermissionLevel.Read;
    }

    private static void ShowFailure(string message, Exception ex)
    {
        Logger.Error(message, ex);
        MessageBox.Show($"{message}\n\n세부 정보: {ex.Message}",
            "공유 생성 실패", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
