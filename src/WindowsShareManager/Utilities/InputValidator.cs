using System.IO;

namespace WindowsShareManager.Utilities;

public static class InputValidator
{
    private static readonly char[] InvalidShareNameCharacters =
        ['"', '/', '\\', '[', ']', ':', '|', '<', '>', '+', '=', ';', ',', '?', '*'];

    public static string? ValidateFolderPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "폴더 경로를 입력하세요.";
        if (path.StartsWith(@"\\", StringComparison.Ordinal))
            return "네트워크 UNC 경로는 공유 대상으로 사용할 수 없습니다.";
        if (!Path.IsPathFullyQualified(path))
            return "드라이브 문자를 포함한 전체 로컬 경로를 입력하세요.";
        if (!Directory.Exists(path))
            return "존재하는 로컬 폴더를 선택하세요.";
        return null;
    }

    public static string? ValidateShareName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "공유 이름을 입력하세요.";
        if (name.Length > 80)
            return "공유 이름은 80자 이하여야 합니다.";
        if (name.IndexOfAny(InvalidShareNameCharacters) >= 0)
            return "공유 이름에 사용할 수 없는 문자가 포함되어 있습니다.";
        if (name.EndsWith('$'))
            return "관리 공유로 오인될 수 있는 '$'로 끝나는 이름은 사용할 수 없습니다.";
        if (name.EndsWith('.') || name.EndsWith(' '))
            return "공유 이름은 마침표나 공백으로 끝날 수 없습니다.";
        return null;
    }
}
