using System.Security.Principal;
using WindowsShareManager.Models;

namespace WindowsShareManager.Services;

public sealed class LocalAccountService
{
    private readonly PowerShellRunner _runner = new();

    public string EveryoneAccountName =>
        new SecurityIdentifier(WellKnownSidType.WorldSid, null)
            .Translate(typeof(NTAccount))
            .Value;

    public async Task<IReadOnlyList<LocalAccountInfo>> GetAccountsAsync(
        CancellationToken cancellationToken = default)
    {
        const string script = """
            $computer = $env:COMPUTERNAME
            $users = Get-CimInstance Win32_UserAccount -Filter 'LocalAccount=True' | ForEach-Object {
              [pscustomobject]@{
                Name = "$computer\$($_.Name)"
                Kind = '사용자'
                Disabled = [bool]$_.Disabled
              }
            }
            $groups = Get-CimInstance Win32_Group -Filter 'LocalAccount=True' | ForEach-Object {
              [pscustomobject]@{
                Name = "$computer\$($_.Name)"
                Kind = '그룹'
                Disabled = $false
              }
            }
            ConvertTo-Json -InputObject @($users + $groups) -Compress
            """;
        var accounts = await _runner.RunJsonAsync<List<LocalAccountInfo>>(script, cancellationToken: cancellationToken)
            ?? [];
        return accounts.OrderBy(x => x.Kind).ThenBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public bool IsValidAccount(string accountName)
    {
        try
        {
            _ = new NTAccount(accountName).Translate(typeof(SecurityIdentifier));
            return true;
        }
        catch (IdentityNotMappedException)
        {
            return false;
        }
    }
}
