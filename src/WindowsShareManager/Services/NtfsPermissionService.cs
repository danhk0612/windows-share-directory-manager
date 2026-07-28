using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using WindowsShareManager.Models;

namespace WindowsShareManager.Services;

public sealed class NtfsPermissionService
{
    public void SetPermission(string folderPath, string accountName, PermissionLevel permission)
    {
        var directory = new DirectoryInfo(folderPath);
        var security = directory.GetAccessControl(AccessControlSections.Access);
        var sid = ResolveSid(accountName);

        RemoveExplicitAllowRules(security, sid);

        var rights = permission switch
        {
            PermissionLevel.Read => FileSystemRights.ReadAndExecute | FileSystemRights.Synchronize,
            PermissionLevel.Change => FileSystemRights.Modify | FileSystemRights.Synchronize,
            PermissionLevel.Full => FileSystemRights.FullControl,
            _ => throw new ArgumentOutOfRangeException(nameof(permission))
        };

        security.AddAccessRule(new FileSystemAccessRule(
            sid,
            rights,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
            PropagationFlags.None,
            AccessControlType.Allow));
        directory.SetAccessControl(security);
        Logger.Info($"NTFS 권한 변경 완료: {folderPath}, {accountName}, {permission}");
    }

    public void RemovePermission(string folderPath, string accountName)
    {
        var directory = new DirectoryInfo(folderPath);
        var security = directory.GetAccessControl(AccessControlSections.Access);
        RemoveExplicitAllowRules(security, ResolveSid(accountName));
        directory.SetAccessControl(security);
        Logger.Info($"명시적 NTFS 허용 권한 제거 완료: {folderPath}, {accountName}");
    }

    private static SecurityIdentifier ResolveSid(string accountName) =>
        (SecurityIdentifier)new NTAccount(accountName).Translate(typeof(SecurityIdentifier));

    private static void RemoveExplicitAllowRules(DirectorySecurity security, SecurityIdentifier sid)
    {
        var rules = security.GetAccessRules(
            includeExplicit: true,
            includeInherited: false,
            targetType: typeof(SecurityIdentifier));

        foreach (FileSystemAccessRule rule in rules)
        {
            if (rule.AccessControlType == AccessControlType.Allow &&
                rule.IdentityReference.Equals(sid))
            {
                security.RemoveAccessRuleSpecific(rule);
            }
        }
    }
}
