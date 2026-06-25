using System.IO;

namespace DuplicateFileTool;

/// <summary>
/// Identifies what changed about a path's membership in the <see cref="DeletionSelection"/>.
/// </summary>
internal enum DeletionSelectionChange
{
    /// <summary>The path was added to the selection (it was not present before).</summary>
    Added,
    /// <summary>The path was removed from the selection (it was present before).</summary>
    Removed,
    /// <summary>The whole selection was emptied via <see cref="DeletionSelection.Clear"/>. The path is unspecified.</summary>
    Reset
}

internal sealed class DeletionSelectionChangedEventArgs(DeletionSelectionChange change, string? normalizedPath, long affectedSize) : EventArgs
{
    public DeletionSelectionChange Change { get; } = change;

    /// <summary>
    /// The normalized full path whose membership changed. Null for <see cref="DeletionSelectionChange.Reset"/>,
    /// which affects every path at once.
    /// </summary>
    public string? NormalizedPath { get; } = normalizedPath;

    /// <summary>
    /// The size (bytes) of the path that was added or removed, so a subscriber can maintain a running total via
    /// deltas without re-reading the set. Zero for <see cref="DeletionSelectionChange.Reset"/>.
    /// </summary>
    public long AffectedSize { get; } = affectedSize;
}

/// <summary>
/// Engine-owned, path-keyed set of files marked for deletion. It is the single source of truth for the
/// "marked for deletion" selection so the same file marked from two different views resolves to one entry.
///
/// Keyed by the file's normalized full path (see <see cref="Normalize"/>): case-insensitive (Windows) and
/// tolerant of the project's long-path conventions (the <c>\\?\</c> / <c>\\?\UNC\</c> prefixes added by
/// <see cref="FileSystem.MakeLongPath"/> at the Win32 boundary are stripped so a path is keyed the same way
/// whether or not the prefix is present). The stored value is the path's size, which keeps a running total.
///
/// This type is intentionally free of any WPF / <c>System.Windows</c> dependency so it can be verified in
/// isolation. Membership changes are reported through the plain <see cref="Changed"/> event; subscribers
/// (e.g. a future <c>DuplicateFile</c>) can compare <see cref="DeletionSelectionChangedEventArgs.NormalizedPath"/>
/// against their own normalized path to learn whether their membership changed. A batched
/// <see cref="DeletionSelectionChange.Reset"/> signal is raised by <see cref="Clear"/>.
///
/// The mutating operations (<see cref="Add"/>, <see cref="Remove"/>, <see cref="Clear"/>) and the
/// <see cref="Count"/>/<see cref="Size"/> reads are guarded by a lock so they are safe to call concurrently
/// (a later issue mutates the set from a background thread). The <see cref="Changed"/> event is raised
/// outside the lock to avoid holding it across subscriber callbacks.
/// </summary>
internal sealed class DeletionSelection
{
    private readonly object _lock = new();
    // Value is the file size; summing the values gives the running total size.
    private readonly Dictionary<string, long> _entries = new(StringComparer.OrdinalIgnoreCase);
    private long _totalSize;

    // Normalized paths of directories the user marked as a whole (issue 012, OQ-1 hook). This set is a sibling to
    // the file set and is consumed by issue 008 to force-remove user-selected folders regardless of the
    // RemoveEmptyDirectories setting. It is FILE-COUNT-INVISIBLE: its mutations never raise the Changed event,
    // because the engine treats every Changed event as a ±1 file delta (a directory entry must not be miscounted
    // as a file). The directory row still updates from its descendant file events plus an explicit self-notify.
    private readonly HashSet<string> _directories = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Raised after a path is added/removed or the whole set is cleared.</summary>
    public event EventHandler<DeletionSelectionChangedEventArgs>? Changed;

    /// <summary>The number of paths currently in the selection.</summary>
    public int Count
    {
        get { lock (_lock) return _entries.Count; }
    }

    /// <summary>The running total size (bytes) of every path currently in the selection.</summary>
    public long Size
    {
        get { lock (_lock) return _totalSize; }
    }

    /// <summary>
    /// Adds the path with its size. Adding a path that is already present is a no-op (the count and total
    /// size are not changed and no event is raised), so the same file marked from two views counts once.
    /// </summary>
    /// <returns>True if the path was added; false if it was already present.</returns>
    public bool Add(string path, long size)
    {
        var key = Normalize(path);
        lock (_lock)
        {
            if (!_entries.TryAdd(key, size))
                return false;
            _totalSize += size;
        }

        OnChanged(DeletionSelectionChange.Added, key, size);
        return true;
    }

    /// <summary>
    /// Removes the path from the selection. Removing a path that is not present is a safe no-op.
    /// </summary>
    /// <returns>True if the path was removed; false if it was not present.</returns>
    public bool Remove(string path)
    {
        var key = Normalize(path);
        long size;
        lock (_lock)
        {
            if (!_entries.Remove(key, out size))
                return false;
            _totalSize -= size;

            // Invariant: an explicitly-selected directory is "fully selected" only while every descendant file
            // is in the set. If a descendant file just left, no ancestor directory can still be fully selected,
            // so evict every ancestor of this file from the directories set. Silent: the file's own Removed event
            // (raised below) already covers totals/UI; the directory set is file-count-invisible.
            EvictAncestorDirectories(key);
        }

        OnChanged(DeletionSelectionChange.Removed, key, size);
        return true;
    }

    /// <summary>
    /// Removes from the directories set every entry that is an ancestor directory of <paramref name="fileKey"/>
    /// (i.e. <paramref name="fileKey"/> starts with <c>dir + separator</c>). Caller must hold the lock.
    /// </summary>
    private void EvictAncestorDirectories(string fileKey)
    {
        if (_directories.Count == 0)
            return;

        List<string>? ancestors = null;
        foreach (var dir in _directories)
        {
            var prefix = dir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (fileKey.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                (ancestors ??= []).Add(dir);
        }

        if (ancestors is null)
            return;
        foreach (var dir in ancestors)
            _directories.Remove(dir);
    }

    /// <summary>
    /// Removes the file entry as part of a deletion run, WITHOUT raising <see cref="Changed"/> and WITHOUT
    /// evicting ancestor directories. The deletion run's throttled <c>DuplicatesEngine.DeletionStateChanged</c>
    /// already decrements the displayed totals per file, so a silent set removal keeps the set consistent with
    /// the engine totals without double-counting or unthrottled UI churn. The directories set is left intact so
    /// issue 008 can still force-remove user-selected folders after the file passes finish.
    /// </summary>
    /// <returns>True if the path was removed; false if it was not present.</returns>
    public bool RemoveSilent(string path)
    {
        var key = Normalize(path);
        lock (_lock)
        {
            if (!_entries.Remove(key, out var size))
                return false;
            _totalSize -= size;
            return true;
        }
    }

    /// <summary>Returns a snapshot copy of every file entry's normalized path, taken under the lock.</summary>
    public IReadOnlyCollection<string> GetFilePaths()
    {
        lock (_lock) return [.. _entries.Keys];
    }

    /// <summary>Tells whether the path is currently in the selection.</summary>
    public bool Contains(string path)
    {
        var key = Normalize(path);
        lock (_lock) return _entries.ContainsKey(key);
    }

    /// <summary>Empties the selection; <see cref="Count"/> and <see cref="Size"/> both become 0.</summary>
    public void Clear()
    {
        lock (_lock)
        {
            // The directories set is silent, so clear it whether or not there are file entries; but only the file
            // entries gate the single Reset event (it carries the totals reset).
            _directories.Clear();
            if (_entries.Count == 0)
                return;
            _entries.Clear();
            _totalSize = 0;
        }

        OnChanged(DeletionSelectionChange.Reset, null, 0);
    }

    /// <summary>
    /// Records a directory as explicitly selected as a whole (issue 012). Adding a directory already present is a
    /// no-op. <b>This never raises <see cref="Changed"/></b> (a directory must not be counted as a file in the
    /// engine totals). The directory row updates from its descendant file events and an explicit self-notify.
    /// </summary>
    /// <returns>True if the directory was added; false if it was already present.</returns>
    public bool AddDirectory(string dirPath)
    {
        var key = Normalize(dirPath);
        lock (_lock) return _directories.Add(key);
    }

    /// <summary>
    /// Removes a directory from the explicitly-selected-directories set. Removing one not present is a safe no-op.
    /// <b>This never raises <see cref="Changed"/></b> (see <see cref="AddDirectory"/>).
    /// </summary>
    /// <returns>True if the directory was removed; false if it was not present.</returns>
    public bool RemoveDirectory(string dirPath)
    {
        var key = Normalize(dirPath);
        lock (_lock) return _directories.Remove(key);
    }

    /// <summary>Tells whether the directory is currently in the explicitly-selected-directories set.</summary>
    public bool ContainsDirectory(string dirPath)
    {
        var key = Normalize(dirPath);
        lock (_lock) return _directories.Contains(key);
    }

    /// <summary>
    /// Returns a snapshot copy of every explicitly-selected directory's normalized path, taken under the lock.
    /// Consumed by issue 008 to force-remove user-selected folders regardless of the RemoveEmptyDirectories setting.
    /// </summary>
    public IReadOnlyCollection<string> GetSelectedDirectories()
    {
        lock (_lock) return [.. _directories];
    }

    /// <summary>
    /// Removes every FILE entry whose key is under <c>Normalize(prefix) + separator</c> — firing one
    /// <see cref="DeletionSelectionChange.Removed"/> event PER removed file (with its size) so the engine totals
    /// decrement correctly and the affected rows update — AND removes from the directories set the prefix itself
    /// and every directory under it (silently). Used by a folder unmark (issue 012) and by issue 017.
    /// </summary>
    public void RemoveAllUnder(string prefix)
    {
        var normalizedPrefix = Normalize(prefix);
        var subtreePrefix = normalizedPrefix.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

        List<KeyValuePair<string, long>> removedFiles = [];
        lock (_lock)
        {
            // Iterate a snapshot of matching keys (the set is bounded by the selection, not the disk).
            List<string>? fileKeys = null;
            foreach (var key in _entries.Keys)
            {
                if (key.StartsWith(subtreePrefix, StringComparison.OrdinalIgnoreCase))
                    (fileKeys ??= []).Add(key);
            }
            if (fileKeys is not null)
            {
                foreach (var key in fileKeys)
                {
                    if (!_entries.Remove(key, out var size))
                        continue;
                    _totalSize -= size;
                    removedFiles.Add(new KeyValuePair<string, long>(key, size));
                }
            }

            // Drop the prefix directory itself and any descendant directory from the directories set (silent).
            List<string>? dirKeys = null;
            foreach (var dir in _directories)
            {
                if (string.Equals(dir, normalizedPrefix, StringComparison.OrdinalIgnoreCase) ||
                    dir.StartsWith(subtreePrefix, StringComparison.OrdinalIgnoreCase))
                    (dirKeys ??= []).Add(dir);
            }
            if (dirKeys is not null)
                foreach (var dir in dirKeys)
                    _directories.Remove(dir);
        }

        // Fire one Removed event per file outside the lock so the engine totals and the rows update.
        foreach (var (key, size) in removedFiles)
            OnChanged(DeletionSelectionChange.Removed, key, size);
    }

    private void OnChanged(DeletionSelectionChange change, string? normalizedPath, long affectedSize) =>
        Changed?.Invoke(this, new DeletionSelectionChangedEventArgs(change, normalizedPath, affectedSize));

    /// <summary>
    /// Produces the canonical dictionary key for a full file path. Strips the project's long-path prefixes
    /// (<c>\\?\</c> and <c>\\?\UNC\</c>, added by <see cref="FileSystem.MakeLongPath"/>) so a path keys the
    /// same way with or without them; the dictionary itself is <see cref="StringComparer.OrdinalIgnoreCase"/>,
    /// which provides the case-insensitive (Windows) match. The stripping mirrors
    /// <c>DuplicatesSearch.GetRootPath</c>.
    /// </summary>
    public static string Normalize(string path)
    {
        if (path is null)
            return "";
        if (path.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase))
            return @"\\" + path[8..];
        if (path.StartsWith(@"\\?\", StringComparison.Ordinal))
            return path[4..];
        return path;
    }
}
