using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace WindowsShareManager.Services;

internal sealed class PowerShellRunner
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    private const string Prelude =
        "$ErrorActionPreference='Stop';" +
        "$ProgressPreference='SilentlyContinue';" +
        "$OutputEncoding=[Console]::OutputEncoding=[Text.UTF8Encoding]::new($false);";

    public async Task<string> RunAsync(
        string script,
        IReadOnlyDictionary<string, string?>? environment = null,
        CancellationToken cancellationToken = default)
    {
        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(Prelude + script));
        var startInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand {encoded}",
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = new UTF8Encoding(false),
            StandardErrorEncoding = new UTF8Encoding(false)
        };

        if (environment is not null)
        {
            foreach (var pair in environment)
            {
                startInfo.Environment[pair.Key] = pair.Value ?? "";
            }
        }

        using var process = new Process { StartInfo = startInfo };
        process.Start();
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(DefaultTimeout);

        try
        {
            await process.WaitForExitAsync(timeoutSource.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            TryKill(process);
            throw new TimeoutException(
                $"Windows 관리 명령이 {DefaultTimeout.TotalSeconds:0}초 안에 완료되지 않았습니다.");
        }
        catch
        {
            TryKill(process);
            throw;
        }

        var output = await outputTask;
        var error = await errorTask;

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(error)
                    ? $"Windows 관리 명령이 종료 코드 {process.ExitCode}로 실패했습니다."
                    : error.Trim());
        }

        if (!string.IsNullOrWhiteSpace(error))
        {
            Logger.Info($"Windows 관리 명령 경고: {error.Trim()}");
        }

        return output.Trim();
    }

    public async Task<T?> RunJsonAsync<T>(
        string script,
        IReadOnlyDictionary<string, string?>? environment = null,
        CancellationToken cancellationToken = default)
    {
        var json = await RunAsync(script, environment, cancellationToken);
        if (string.IsNullOrWhiteSpace(json))
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // 이미 종료됐거나 종료할 수 없는 경우 원래 오류를 유지한다.
        }
    }
}
