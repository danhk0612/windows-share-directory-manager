using WindowsShareManager.Models;
using WindowsShareManager.Utilities;

namespace WindowsShareManager.Services;

public sealed class PrinterShareService
{
    private readonly PowerShellRunner _runner = new();

    public async Task<IReadOnlyList<PrinterInfo>> GetPrintersAsync(
        CancellationToken cancellationToken = default)
    {
        const string script = """
            $items = Get-Printer | Where-Object {
              [string]$_.Type -ne 'Connection'
            } | ForEach-Object {
              [pscustomobject]@{
                Name = [string]$_.Name
                DriverName = [string]$_.DriverName
                PortName = [string]$_.PortName
                PrinterStatus = [string]$_.PrinterStatus
                Shared = [bool]$_.Shared
                ShareName = [string]$_.ShareName
                Comment = [string]$_.Comment
                Location = [string]$_.Location
                Type = [string]$_.Type
              }
            }
            ConvertTo-Json -InputObject @($items) -Depth 3 -Compress
            """;

        Logger.Info("로컬 프린터 목록 조회 시작");
        var printers = await _runner.RunJsonAsync<List<PrinterInfo>>(script, cancellationToken: cancellationToken)
            ?? [];
        Logger.Info($"로컬 프린터 목록 조회 완료: {printers.Count}개");
        return printers.OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public async Task SetShareAsync(
        string printerName,
        string shareName,
        string comment,
        string location,
        CancellationToken cancellationToken = default)
    {
        var validation = InputValidator.ValidateShareName(shareName);
        if (validation is not null)
        {
            throw new ArgumentException(validation, nameof(shareName));
        }

        var printers = await GetPrintersAsync(cancellationToken);
        if (printers.Any(x =>
                x.Shared &&
                !x.Name.Equals(printerName, StringComparison.OrdinalIgnoreCase) &&
                x.ShareName.Equals(shareName, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("같은 공유 이름을 사용하는 다른 프린터가 이미 존재합니다.");
        }

        const string script = """
            Set-Printer -Name $env:WSM_PRINTER_NAME `
              -Shared $true `
              -ShareName $env:WSM_PRINTER_SHARE_NAME `
              -Comment $env:WSM_PRINTER_COMMENT `
              -Location $env:WSM_PRINTER_LOCATION
            """;
        await _runner.RunAsync(script, ToEnvironment(printerName, shareName, comment, location), cancellationToken);

        var updated = (await GetPrintersAsync(cancellationToken))
            .FirstOrDefault(x => x.Name.Equals(printerName, StringComparison.OrdinalIgnoreCase));
        if (updated is null || !updated.Shared ||
            !updated.ShareName.Equals(shareName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("프린터 공유 변경 후 Windows에서 적용 상태를 확인하지 못했습니다.");
        }

        Logger.Info($"프린터 공유 설정 완료: {printerName}, 공유 이름: {shareName}");
    }

    public async Task DisableShareAsync(
        string printerName,
        CancellationToken cancellationToken = default)
    {
        const string script =
            "Set-Printer -Name $env:WSM_PRINTER_NAME -Shared $false";
        await _runner.RunAsync(script, new Dictionary<string, string?>
        {
            ["WSM_PRINTER_NAME"] = printerName
        }, cancellationToken);

        var updated = (await GetPrintersAsync(cancellationToken))
            .FirstOrDefault(x => x.Name.Equals(printerName, StringComparison.OrdinalIgnoreCase));
        if (updated is null || updated.Shared)
        {
            throw new InvalidOperationException("프린터 공유 해제 후 Windows에서 적용 상태를 확인하지 못했습니다.");
        }

        Logger.Info($"프린터 공유 해제 완료: {printerName}");
    }

    private static Dictionary<string, string?> ToEnvironment(
        string printerName,
        string shareName,
        string comment,
        string location) => new()
    {
        ["WSM_PRINTER_NAME"] = printerName,
        ["WSM_PRINTER_SHARE_NAME"] = shareName,
        ["WSM_PRINTER_COMMENT"] = comment,
        ["WSM_PRINTER_LOCATION"] = location
    };
}
