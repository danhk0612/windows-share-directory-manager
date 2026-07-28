using System.Windows;
using System.Windows.Controls;
using WindowsShareManager.Models;
using WindowsShareManager.Services;

namespace WindowsShareManager.Views;

public partial class AccountPermissionWindow : Window
{
    private readonly LocalAccountService _accountService;
    private readonly string? _fixedAccount;
    private readonly PermissionLevel _initialPermission;

    public string AccountName { get; private set; } = "";
    public PermissionLevel Permission { get; private set; }

    public AccountPermissionWindow(
        LocalAccountService accountService,
        string? fixedAccount = null,
        PermissionLevel initialPermission = PermissionLevel.Read)
    {
        InitializeComponent();
        _accountService = accountService;
        _fixedAccount = fixedAccount;
        _initialPermission = initialPermission;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        PermissionComboBox.SelectedIndex = (int)_initialPermission;
        if (_fixedAccount is not null)
        {
            AccountComboBox.Text = _fixedAccount;
            AccountComboBox.IsEnabled = false;
            return;
        }
        try
        {
            AccountComboBox.ItemsSource = await _accountService.GetAccountsAsync();
        }
        catch (Exception ex)
        {
            Logger.Error("로컬 계정 목록 조회 실패", ex);
            ValidationText.Text = "계정 목록을 불러오지 못했습니다. 계정 이름은 직접 입력할 수 있습니다.";
        }
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        var account = _fixedAccount
            ?? (AccountComboBox.SelectedItem as LocalAccountInfo)?.Name
            ?? AccountComboBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(account) || !_accountService.IsValidAccount(account))
        {
            ValidationText.Text = "Windows에서 확인할 수 있는 유효한 사용자 또는 그룹을 입력하세요.";
            return;
        }
        AccountName = account;
        var tag = (PermissionComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        Permission = Enum.TryParse<PermissionLevel>(tag, out var value) ? value : PermissionLevel.Read;
        DialogResult = true;
    }
}
