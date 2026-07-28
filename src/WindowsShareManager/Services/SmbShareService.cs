using WindowsShareManager.Models;

namespace WindowsShareManager.Services;

public sealed class SmbShareService
{
    private readonly PowerShellRunner _runner = new();

    private const string ReadSharesScript = """
        $items = Get-SmbShare | Where-Object {
          -not $_.Special -and
          $_.Name -notin @('ADMIN$','IPC$','PRINT$') -and
          $_.Name -notmatch '^[A-Za-z]\$$'
        } | ForEach-Object {
          $share = $_
          $access = @(Get-SmbShareAccess -Name $share.Name | ForEach-Object {
            [pscustomobject]@{
              AccountName = $_.AccountName
              AccessControlType = [string]$_.AccessControlType
              AccessRight = [string]$_.AccessRight
            }
          })
          [pscustomobject]@{
            Name = $share.Name
            Path = $share.Path
            Description = $share.Description
            Special = [bool]$share.Special
            Permissions = $access
          }
        }
        ConvertTo-Json -InputObject @($items) -Depth 5 -Compress
        """;

    public async Task<IReadOnlyList<ShareInfo>> GetSharesAsync(CancellationToken cancellationToken = default)
    {
        Logger.Info("공유 목록 조회 시작");
        var shares = await _runner.RunJsonAsync<List<ShareInfo>>(ReadSharesScript, cancellationToken: cancellationToken)
            ?? [];
        shares = shares
            .Where(x => !IsSystemShare(x.Name, x.Special))
            .ToList();
        Logger.Info($"공유 목록 조회 완료: {shares.Count}개");
        return shares.OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public async Task<ShareInfo?> GetShareAsync(string name, CancellationToken cancellationToken = default)
    {
        var shares = await GetSharesAsync(cancellationToken);
        return shares.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    public async Task CreateShareAsync(CreateShareRequest request, CancellationToken cancellationToken = default)
    {
        Logger.Info($"공유 생성 시작: {request.Name}, 경로: {request.Path}");
        const string script = """
            $params = @{
              Name = $env:WSM_NAME
              Path = $env:WSM_PATH
              Description = $env:WSM_DESCRIPTION
            }
            switch ($env:WSM_PERMISSION) {
              'Read' { $params.ReadAccess = $env:WSM_ACCOUNT }
              'Change' { $params.ChangeAccess = $env:WSM_ACCOUNT }
              'Full' { $params.FullAccess = $env:WSM_ACCOUNT }
              default { throw '지원하지 않는 공유 권한입니다.' }
            }
            New-SmbShare @params | Out-Null
            """;

        await _runner.RunAsync(script, ToEnvironment(request), cancellationToken);
        var created = await GetShareAsync(request.Name, cancellationToken);
        if (created is null)
        {
            throw new InvalidOperationException("공유 생성 명령 후 Windows에서 해당 공유를 다시 확인하지 못했습니다.");
        }

        Logger.Info($"공유 생성 완료: {request.Name}");
    }

    public async Task UpdateDescriptionAsync(
        string shareName,
        string description,
        CancellationToken cancellationToken = default)
    {
        const string script =
            "Set-SmbShare -Name $env:WSM_NAME -Description $env:WSM_DESCRIPTION -Force | Out-Null";
        await _runner.RunAsync(script, new Dictionary<string, string?>
        {
            ["WSM_NAME"] = shareName,
            ["WSM_DESCRIPTION"] = description
        }, cancellationToken);
        Logger.Info($"공유 설명 변경 완료: {shareName}");
    }

    public async Task SetPermissionAsync(
        string shareName,
        string accountName,
        PermissionLevel permission,
        CancellationToken cancellationToken = default)
    {
        const string script = """
            $existing = @(Get-SmbShareAccess -Name $env:WSM_NAME | Where-Object {
              $_.AccountName -ieq $env:WSM_ACCOUNT -and $_.AccessControlType -eq 'Allow'
            })
            if ($existing.Count -gt 0) {
              Revoke-SmbShareAccess -Name $env:WSM_NAME -AccountName $env:WSM_ACCOUNT -Force | Out-Null
            }
            Grant-SmbShareAccess -Name $env:WSM_NAME -AccountName $env:WSM_ACCOUNT `
              -AccessRight $env:WSM_PERMISSION -Force | Out-Null
            """;
        await _runner.RunAsync(script, new Dictionary<string, string?>
        {
            ["WSM_NAME"] = shareName,
            ["WSM_ACCOUNT"] = accountName,
            ["WSM_PERMISSION"] = permission.ToSmbAccessRight()
        }, cancellationToken);
        Logger.Info($"SMB 권한 변경 완료: {shareName}, {accountName}, {permission}");
    }

    public async Task RemovePermissionAsync(
        string shareName,
        string accountName,
        CancellationToken cancellationToken = default)
    {
        const string script =
            "Revoke-SmbShareAccess -Name $env:WSM_NAME -AccountName $env:WSM_ACCOUNT -Force | Out-Null";
        await _runner.RunAsync(script, new Dictionary<string, string?>
        {
            ["WSM_NAME"] = shareName,
            ["WSM_ACCOUNT"] = accountName
        }, cancellationToken);
        Logger.Info($"SMB 권한 제거 완료: {shareName}, {accountName}");
    }

    public async Task DeleteShareAsync(string shareName, CancellationToken cancellationToken = default)
    {
        const string script = """
            $share = Get-SmbShare -Name $env:WSM_NAME
            if ($share.Special) { throw '시스템 또는 관리 공유는 삭제할 수 없습니다.' }
            Remove-SmbShare -Name $env:WSM_NAME -Force
            """;
        await _runner.RunAsync(script, new Dictionary<string, string?>
        {
            ["WSM_NAME"] = shareName
        }, cancellationToken);

        if (await GetShareAsync(shareName, cancellationToken) is not null)
        {
            throw new InvalidOperationException("공유 삭제 명령 후에도 Windows에서 해당 공유가 확인됩니다.");
        }
        Logger.Info($"공유 삭제 완료: {shareName}");
    }

    private static Dictionary<string, string?> ToEnvironment(CreateShareRequest request) => new()
    {
        ["WSM_NAME"] = request.Name,
        ["WSM_PATH"] = request.Path,
        ["WSM_DESCRIPTION"] = request.Description,
        ["WSM_ACCOUNT"] = request.AccountName,
        ["WSM_PERMISSION"] = request.Permission.ToSmbAccessRight()
    };

    public static bool IsSystemShare(string name, bool special)
    {
        if (special) return true;
        if (name.Equals("ADMIN$", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("IPC$", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("PRINT$", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return name.Length == 2 &&
               char.IsLetter(name[0]) &&
               name[1] == '$';
    }
}
