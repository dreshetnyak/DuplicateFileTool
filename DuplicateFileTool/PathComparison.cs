using System.IO;

namespace DuplicateFileTool;

internal static class PathComparison
{
    public static string Normalize(string path)
    {
        var normalizedPath = path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        return Path.TrimEndingDirectorySeparator(normalizedPath);
    }

    public static bool IsSameOrDescendant(string path, string ancestor)
    {
        if (string.Equals(path, ancestor, StringComparison.OrdinalIgnoreCase))
            return true;
        if (!path.StartsWith(ancestor, StringComparison.OrdinalIgnoreCase) || path.Length <= ancestor.Length)
            return false;
        if (Path.EndsInDirectorySeparator(ancestor))
            return true;

        var boundary = path[ancestor.Length];
        return boundary == Path.DirectorySeparatorChar || boundary == Path.AltDirectorySeparatorChar;
    }
}
