using System.Windows;
using WindowsShareManager.Models;
using WindowsShareManager.Services;
using WindowsShareManager.Utilities;

namespace WindowsShareManager.Views;

public partial class ServiceStartWindow : Window
{
    private readonly ServiceManagementService _service;
    private readonly ServiceControlInfo _serviceInfo;

    public ServiceStartWindow(
        ServiceManagementService service,
        ServiceControlInfo serviceInfo)
    {
        InitializeComponent();
        _service = service;
        _serviceInfo = serviceInfo;

        ServiceNameText.Text = $"{serviceInfo.DisplayName} ({serviceInfo.Name})";
        CurrentStateText.Text =
            $"현재 상태: {serviceInfo.StatusDisplay} · 시작 유형: {serviceInfo.StartModeDisplay}";
        NoticeText.Text = ServiceManagementService.IsTriggerIdleExpected(serviceInfo.Name)
            ? "이 서비스는 필요한 작업이 없으면 다시 중지될 수 있습니다. 중지 상태가 항상 오류를 의미하지는 않습니다."
            : "시작 유형 변경은 이후 Windows 부팅에도 적용됩니다. 현재 PC의 용도에 맞는 방식을 선택하세요.";

        if (serviceInfo.IsDisabled)
        {
            StartOnlyRadio.IsEnabled = false;
            ManualRadio.Visibility = Visibility.Visible;
            ManualDescription.Visibility = Visibility.Visible;
            ManualRadio.IsChecked = true;
        }

        if (ServiceManagementService.AllowsAutomatic(serviceInfo.Name))
        {
            AutomaticRadio.Visibility = Visibility.Visible;
            AutomaticDescription.Visibility = Visibility.Visible;
        }

        if (ServiceManagementService.AllowsDelayedAutomatic(serviceInfo.Name))
        {
            DelayedAutomaticRadio.Visibility = Visibility.Visible;
            DelayedDescription.Visibility = Visibility.Visible;
        }
    }

    private async void Apply_Click(object sender, RoutedEventArgs e)
    {
        var option = GetSelectedOption();
        ApplyButton.IsEnabled = false;
        ResultText.Foreground = System.Windows.Media.Brushes.DimGray;
        ResultText.Text = "서비스 설정을 적용하는 중입니다...";
        try
        {
            var updated = await _service.ApplyStartOptionAsync(_serviceInfo.Name, option);
            if (ServiceManagementService.IsTriggerIdleExpected(updated.Name) && !updated.IsRunning)
            {
                Logger.Info($"{updated.Name} 서비스가 시작 요청 후 정상 대기 상태로 전환됐습니다.");
            }
            DialogResult = true;
        }
        catch (Exception ex)
        {
            Logger.Error($"서비스 시작 설정 실패: {_serviceInfo.Name}", ex);
            ResultText.Foreground = System.Windows.Media.Brushes.DarkRed;
            ResultText.Text = $"서비스 설정을 적용하지 못했습니다. {ex.Message}";
            ApplyButton.IsEnabled = true;
        }
    }

    private ServiceStartOption GetSelectedOption()
    {
        if (ManualRadio.IsChecked == true) return ServiceStartOption.ManualAndStart;
        if (AutomaticRadio.IsChecked == true) return ServiceStartOption.AutomaticAndStart;
        if (DelayedAutomaticRadio.IsChecked == true)
            return ServiceStartOption.DelayedAutomaticAndStart;
        return ServiceStartOption.StartOnly;
    }

    private void OpenServices_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SystemSettingsLauncher.Open("Services");
        }
        catch (Exception ex)
        {
            ResultText.Text = $"서비스 관리 화면을 열지 못했습니다. {ex.Message}";
        }
    }
}
