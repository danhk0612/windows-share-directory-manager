using System.Windows;
using WindowsShareManager.Models;
using WindowsShareManager.Services;

namespace WindowsShareManager.Views;

public partial class EditShareWindow : Window
{
    private readonly string _shareName;
    private readonly SmbShareService _shareService;
    private readonly NtfsPermissionService _ntfsService;
    private readonly LocalAccountService _accountService;
    private ShareInfo? _share;

    public EditShareWindow(
        string shareName,
        SmbShareService shareService,
        NtfsPermissionService ntfsService,
        LocalAccountService accountService)
    {
        InitializeComponent();
        _shareName = shareName;
        _shareService = shareService;
        _ntfsService = ntfsService;
        _accountService = accountService;
    }

    private SharePermissionInfo? SelectedPermission => PermissionsGrid.SelectedItem as SharePermissionInfo;

    private async void Window_Loaded(object sender, RoutedEventArgs e) => await LoadShareAsync();

    private async Task LoadShareAsync()
    {
        try
        {
            _share = await _shareService.GetShareAsync(_shareName);
            if (_share is null)
            {
                MessageBox.Show("선택한 공유를 Windows에서 찾을 수 없습니다.",
                    "공유 없음", MessageBoxButton.OK, MessageBoxImage.Warning);
                Close();
                return;
            }
            NameTextBox.Text = _share.Name;
            PathTextBox.Text = _share.Path;
            DescriptionTextBox.Text = _share.Description;
            PermissionsGrid.ItemsSource = _share.Permissions;
        }
        catch (Exception ex)
        {
            ShowError("공유 정보를 불러오지 못했습니다.", ex);
        }
    }

    private async void SaveDescription_Click(object sender, RoutedEventArgs e)
    {
        IsEnabled = false;
        try
        {
            await _shareService.UpdateDescriptionAsync(_shareName, DescriptionTextBox.Text.Trim());
            StatusText.Text = "설명을 저장했습니다.";
            await LoadShareAsync();
        }
        catch (Exception ex)
        {
            ShowError("공유 설명을 변경하지 못했습니다.", ex);
        }
        finally
        {
            IsEnabled = true;
        }
    }

    private async void AddPermission_Click(object sender, RoutedEventArgs e)
    {
        if (_share is null) return;
        var dialog = new AccountPermissionWindow(_accountService) { Owner = this };
        if (dialog.ShowDialog() != true) return;
        await ApplyPermissionAsync(dialog.AccountName, dialog.Permission);
    }

    private async void ChangePermission_Click(object sender, RoutedEventArgs e)
    {
        if (_share is null || SelectedPermission is null) return;
        if (SelectedPermission.AccessControlType.Equals("Deny", StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show("초기 버전에서는 기존 거부 규칙을 표시만 하며 변경하지 않습니다.",
                "거부 규칙", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var current = PermissionLevelExtensions.FromSmbAccessRight(SelectedPermission.AccessRight);
        var dialog = new AccountPermissionWindow(
            _accountService,
            SelectedPermission.AccountName,
            current) { Owner = this };
        if (dialog.ShowDialog() != true) return;
        await ApplyPermissionAsync(dialog.AccountName, dialog.Permission);
    }

    private async Task ApplyPermissionAsync(string accountName, PermissionLevel permission)
    {
        if (_share is null) return;
        IsEnabled = false;
        try
        {
            try
            {
                await _shareService.SetPermissionAsync(_share.Name, accountName, permission);
            }
            catch (Exception ex)
            {
                ShowError("SMB 공유 권한 변경 단계에서 실패했습니다.", ex);
                return;
            }
            try
            {
                _ntfsService.SetPermission(_share.Path, accountName, permission);
                StatusText.Text = "SMB 및 NTFS 권한을 변경했습니다.";
            }
            catch (Exception ex)
            {
                ShowError(
                    "SMB 권한은 변경되었지만 NTFS 권한 변경 단계에서 실패했습니다. 현재 상태를 확인하세요.", ex);
            }
            await LoadShareAsync();
        }
        finally
        {
            IsEnabled = true;
        }
    }

    private async void RemovePermission_Click(object sender, RoutedEventArgs e)
    {
        if (_share is null || SelectedPermission is null) return;
        if (SelectedPermission.AccessControlType.Equals("Deny", StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show("초기 버전에서는 기존 거부 규칙을 표시만 하며 제거하지 않습니다.",
                "거부 규칙", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var result = MessageBox.Show(
            this,
            $"'{SelectedPermission.AccountName}' 계정의 SMB 권한을 제거합니다.\n\n" +
            "예: 해당 계정의 모든 명시적 NTFS 허용 규칙도 함께 제거\n" +
            "아니요: SMB 공유 권한만 제거\n" +
            "취소: 작업하지 않음\n\n" +
            "프로그램은 기존 규칙과 자신이 추가한 규칙을 구분할 수 없습니다. " +
            "예를 선택하면 다른 프로그램이나 사용자가 설정한 해당 계정의 명시적 허용 규칙도 제거될 수 있습니다. " +
            "상속된 권한과 다른 계정의 권한은 변경하지 않습니다.",
            "접근 계정 제거",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Warning,
            MessageBoxResult.No);
        if (result == MessageBoxResult.Cancel) return;

        var account = SelectedPermission.AccountName;
        IsEnabled = false;
        try
        {
            await _shareService.RemovePermissionAsync(_share.Name, account);
            if (result == MessageBoxResult.Yes)
            {
                _ntfsService.RemovePermission(_share.Path, account);
            }
            StatusText.Text = result == MessageBoxResult.Yes
                ? "SMB 및 명시적 NTFS 허용 권한을 제거했습니다."
                : "SMB 공유 권한을 제거했습니다.";
            await LoadShareAsync();
        }
        catch (Exception ex)
        {
            ShowError("선택한 계정의 권한을 제거하지 못했습니다.", ex);
        }
        finally
        {
            IsEnabled = true;
        }
    }

    private static void ShowError(string message, Exception ex)
    {
        Logger.Error(message, ex);
        MessageBox.Show($"{message}\n\n세부 정보: {ex.Message}",
            "공유 수정 실패", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
