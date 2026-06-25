namespace DuplicateFileTool;

/// <summary>
/// Pure, WPF-independent helper for deriving a duplicate group's distinct containing folders in the group's own
/// order. The rail (folder-selection list) and the column builder both consume this so the rail order, the column
/// order, and the results-tree order agree.
///
/// "The group's own order" is the order the duplicate files are listed in the results tree (ascending full path,
/// fixed at group construction). Taking the distinct containing folders in first-appearance order over that
/// sequence yields a well-defined ordering without any sort.
///
/// This type takes strings in and strings out so it has zero dependency on <c>DuplicateFile</c> / WPF and is
/// trivially verifiable in isolation. It is the highest test seam of the folder-selection rail feature.
/// </summary>
internal static class GroupFolders
{
    /// <summary>
    /// Returns the distinct directory paths in first-appearance order, deduplicated case-insensitively (and
    /// tolerant of long-path prefixes) via <see cref="DeletionSelection.Normalize"/>. Same-folder duplicates
    /// collapse to one entry. For each distinct folder the FIRST real (un-normalized) path seen is returned, so
    /// the caller lays out the path as it appears on disk. No sorting is performed.
    /// </summary>
    public static IReadOnlyList<string> OrderedDistinct(IEnumerable<string> directoryPaths)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ordered = new List<string>();
        foreach (var path in directoryPaths)
        {
            if (seen.Add(DeletionSelection.Normalize(path)))
                ordered.Add(path);
        }
        return ordered;
    }
}
