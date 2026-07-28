using System.Windows;
using WindowsShareManager.Models;
using WindowsShareManager.Services;
using WindowsShareManager.Utilities;

namespace WindowsShareManager.Views;

public partial class PrinterShareWindow : Window
{
    private readonly PrinterInfo _printer;
    private readonly PrinterShareService _service;

    public PrinterShareWindow(PrinterInfo printer, PrinterShareService service)
    {
        InitializeComponent();
        _printer = printer;
        _service = service;
        PrinterNameTextBox.Text = printer.Name;
        ShareNameTextBox.Text = printer.Shared && !string.IsNullOrWhiteSpace(printer.ShareName)
            ? printer.ShareName
            : printer.Name;
        CommentTextBox.Text = printer.Comment;
        LocationTextBox.Text = printer.Location;
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        ValidationText.Text = "";
        var shareName = ShareNameTextBox.Text.Trim();
        var validation = InputValidator.ValidateShareName(shareName);
        if (validation is not null)
        {
            ValidationText.Text = validation;
            return;
        }

        var networkPath = $@"\\{Environment.MachineName}\{shareName}";
        var result = MessageBox.Show(
            this,
            $"프린터: {_printer.Name}\n공유 이름: {shareName}\n네트워크 경로: {networkPath}\n\n" +
            "프린터 공유 설정을 저장하시겠습니까?",
            "프린터 공유 확인",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No);
        if (result != MessageBoxResult.Yes) return;

        SaveButton.IsEnabled = false;
        try
        {
            await _service.SetShareAsync(
                _printer.Name,
                shareName,
                CommentTextBox.Text.Trim(),
                LocationTextBox.Text.Trim());
            DialogResult = true;
        }
        catch (Exception ex)
        {
            Logger.Error($"프린터 공유 설정 실패: {_printer.Name}", ex);
            ValidationText.Text = $"프린터 공유를 변경하지 못했습니다. {ex.Message}";
            SaveButton.IsEnabled = true;
        }
    }
}
