using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Media;

namespace DuplicateFileTool;

/// <summary>
/// A node view-model for the folder-comparison trees. Represents one file or directory row.
/// Mirrors <see cref="FileTreeItem"/>'s placeholder lazy-load and open/close shell icons and
/// dirs-first child ordering, but deliberately omits the search tree's static selection event
/// and refresh/activate behaviors to avoid cross-impact on the Search page.
/// Reparse points (junctions/symlinks) are shown as leaves and are never enumerated.
/// </summary>
internal sealed class FolderItem : NotifyPropertyChanged
{
    #region Backing Fields
    private bool _isExpanded;
    private ImageSource? _icon;
    // The in-flight eager subtree scan for this directory node, or null when no scan is running (issue 012).
    private CancellationTokenSource? _scanCts;
    // The number of in-flight eager subtree scans (issue 012) running on THIS node or any of its descendants
    // (issue 022). Maintained by BeginScan/EndScan walking up the Parent chain; drives IsScanBusy. Mutated only
    // on the UI thread, so a plain int (no interlocking) is sufficient.
    private int _subtreeScanCount;
    // A directory node's recursively-summed content size, filled in by the background StartSizeScan walk and then
    // surfaced through Size. A directory's own Win32 entry size is 0, so without this a folder row shows 0 even
    // when it holds files. Unused for file nodes (they report their own entry size). Set on the UI thread.
    private long _directorySize;
    #endregion

    private static readonly FolderItem ChildPlaceholder = new();
    private bool ChildrenContainsOnlyPlaceholder => Children.Count == 1 && ReferenceEquals(Children[0], ChildPlaceholder);

    private FileData ItemFileData { get; }

    /// <summary>
    /// The duplicate-finding engine, source of the deletion selection and the membership/classification index
    /// (<see cref="DuplicatesEngine.WouldLeaveZeroSurvivors"/>, <see cref="DuplicatesEngine.GetGroupForPath"/>,
    /// <see cref="DuplicatesEngine.CurrentComparisonGroup"/>). Null only for the shared
    /// <see cref="ChildPlaceholder"/>, which never queries it.
    /// </summary>
    private DuplicatesEngine? Engine { get; }

    /// <summary>
    /// The engine-owned, single-source-of-truth set of files marked for deletion. A file node's marked
    /// state is membership in this set; a directory node derives its state from its descendant files.
    /// Sourced from <see cref="Engine"/>; null only for the shared <see cref="ChildPlaceholder"/>, which
    /// never queries it.
    /// </summary>
    private DeletionSelection? DeletionSelection => Engine?.DeletionSelection;

    /// <summary>The parent node, or null for a root node.</summary>
    public FolderItem? Parent { get; }

    /// <summary>
    /// Depth from the column root. The root itself is 0 and is never shown as a row; its children — the top-level
    /// rows — are 1, their children 2, and so on. Drives the Name-cell indent that gives the folder tree a normal
    /// hierarchical indent: the shared <see cref="TreeListViewItem"/> template deliberately does not indent rows
    /// (so columns stay aligned), so the indent is applied inside the Name column's cell instead.
    /// </summary>
    public int Level { get; }

    public string FullName => ItemFileData.FullName;
    public string Name { get; }

    /// <summary>
    /// The displayed size. A file reports its own entry size; a directory reports the recursively-summed size of
    /// every file in its subtree (Win32 gives a directory entry a size of 0), computed once by the background
    /// <see cref="StartSizeScan"/> walk and cached in <see cref="_directorySize"/>. Zero until that walk commits.
    /// </summary>
    public long Size => IsDirectory ? _directorySize : ItemFileData.Size;
    public DateTime LastModified => ItemFileData.LastWriteTime;
    public bool IsDirectory => ItemFileData.Attributes.IsDirectory || ItemFileData.Attributes.IsDevice;

    /// <summary>
    /// True for junctions/symlinks. Such entries are shown as leaves and are never enumerated,
    /// so a junction can never loop or reach through to its target.
    /// </summary>
    public bool IsReparsePoint => ItemFileData.Attributes.IsReparsePoint;

    /// <summary>True when this node can host children (a directory that is not a reparse point).</summary>
    public bool CanExpand => IsDirectory && !IsReparsePoint;

    /// <summary>
    /// True when deleting the current selection would leave no surviving copy of this file (a marked
    /// non-duplicate, or a marked duplicate whose entire group is marked). Always false for directory nodes.
    /// Queried live from the engine's classification index, so it flips as group siblings are marked/unmarked
    /// anywhere; <see cref="OnDeletionSelectionChanged"/> re-raises it on every selection change.
    /// </summary>
    public bool IsZeroSurvivor => !IsDirectory && (Engine?.WouldLeaveZeroSurvivors(FullName) ?? false);

    /// <summary>
    /// True when this file is the current group's member that this column's own folder directly contains — the file the
    /// column corresponds to. It must be a current-group member (its <see cref="DuplicatesEngine.GetGroupForPath"/> is
    /// the <see cref="DuplicatesEngine.CurrentComparisonGroup"/>) AND a direct child of the column root, so a
    /// same-content copy sitting in a SUBFOLDER of this column (which is its own column's corresponding file) is not
    /// marked here — that was the double-mark bug a whole-group flag caused. Always false for directory nodes and when
    /// no current group is set. Re-raised on a current-group switch (see <see cref="OnEnginePropertyChanged"/>); a
    /// switch also rebuilds the columns, so fresh rows compute it at realization.
    /// </summary>
    public bool IsCorrespondingGroupFile =>
        !IsDirectory
        && Parent is { Parent: null } // a direct child of the column root, i.e. living in the column's own folder
        && Engine is { CurrentComparisonGroup: not null }
        && ReferenceEquals(Engine.GetGroupForPath(FullName), Engine.CurrentComparisonGroup);

    /// <summary>
    /// True when this file was detected as a duplicate in ANY group — the current one or any other. Always false for
    /// directory nodes. Answered from the engine's O(1) membership index (<see cref="DuplicatesEngine.IsDuplicate"/>),
    /// which is already complete by the time a folder-comparison column is built (columns are built only after a group
    /// is selected, i.e. after the search populated the index, and are rebuilt on every group switch), so the value is
    /// correct at the moment each row is realized. Deliberately NOT re-raised while the tree is shown: the membership
    /// set does not change during normal viewing, and the columns are a snapshot of the selected group (rebuilt on a
    /// group switch, not live-refreshed after a deletion), so a re-raise would only matter for cases the column itself
    /// already does not track. The column's corresponding group file is a duplicate too, so it satisfies this as well;
    /// the row style paints it with the corresponding-file tint instead — that trigger is ordered AFTER this one (see
    /// FolderTree.xaml).
    /// </summary>
    public bool IsDuplicateAnywhere => !IsDirectory && (Engine?.IsDuplicate(FullName) ?? false);

    /// <summary>
    /// True while an eager subtree scan (issue 012) is running on this node or any of its descendants. The column's
    /// busy overlay (issue 022) binds to the column-root node's value: marking a sub-directory makes that node and
    /// every ancestor (up to the root) report busy via the <see cref="_subtreeScanCount"/> walk in
    /// <see cref="BeginScan"/>/<see cref="EndScan"/>.
    /// </summary>
    public bool IsScanBusy => _subtreeScanCount > 0;

    public ObservableCollection<FolderItem> Children { get; } = [];

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value)
                return;

            if (value)
                LoadChildren();
            else
                RemoveChildren();

            _isExpanded = value;
            UpdateIcon();
            OnPropertyChanged();
        }
    }

    public ImageSource? Icon
    {
        get => _icon;
        private set
        {
            _icon = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// The mark-for-deletion state, derived from the engine-owned <see cref="DeletionSelection"/> set;
    /// there is no per-node backing flag.
    /// <para>
    /// File node: membership of its own path in the set (get = contains, set = add/remove with size).
    /// </para>
    /// <para>
    /// Directory node: a binary derived state — true only when it has at least one descendant file and
    /// every descendant file is in the set. For this issue it is computed from the currently-LOADED
    /// children (a child sub-directory's own <see cref="IsMarkedForDeletion"/> recurses for us); a
    /// collapsed/unloaded directory (only the placeholder child) reports false until issue 012 makes
    /// collapsed folders answer correctly via eager subtree enumeration. The setter marks/unmarks all
    /// currently-loaded descendant FILE nodes.
    /// </para>
    /// Neither setter raises <see cref="NotifyPropertyChanged.OnPropertyChanged"/> directly — a real
    /// change raises <see cref="DeletionSelection.Changed"/>, and <see cref="OnDeletionSelectionChanged"/>
    /// turns that into the notification, mirroring DuplicateFile.
    /// </summary>
    public bool IsMarkedForDeletion
    {
        get
        {
            if (DeletionSelection is null)
                return false;

            if (!IsDirectory)
                return DeletionSelection.Contains(FullName);

            // A directory marked as a whole (its subtree eagerly enumerated by the setter, issue 012) shows marked
            // even while collapsed, because it was recorded in the explicitly-selected-directories set. Once any
            // descendant file is individually unmarked, DeletionSelection.Remove evicts every ancestor directory
            // from that set, so this branch then yields false and the loaded-children derivation below also fails.
            if (DeletionSelection.ContainsDirectory(FullName))
                return true;

            // A collapsed/unloaded directory that was not explicitly marked only holds the placeholder; it cannot
            // answer from its loaded children, so it reports false until it is expanded or explicitly marked.
            if (ChildrenContainsOnlyPlaceholder || Children.Count == 0)
                return false;

            // Marked iff there is at least one descendant file and every descendant file is marked. Empty
            // directories (dirs that contain only empty dirs) never become marked. A child directory's own
            // IsMarkedForDeletion already recurses, so this single pass covers the whole loaded subtree.
            var hasDescendantFile = false;
            foreach (var child in Children)
            {
                if (child.IsDirectory)
                {
                    // An empty (no descendant file) child dir is neither a blocker nor a contributor.
                    if (!child.HasLoadedDescendantFile())
                        continue;
                    hasDescendantFile = true;
                    if (!child.IsMarkedForDeletion)
                        return false;
                }
                else
                {
                    hasDescendantFile = true;
                    if (!child.IsMarkedForDeletion)
                        return false;
                }
            }

            return hasDescendantFile;
        }
        set
        {
            if (DeletionSelection is null)
                return;

            if (!IsDirectory)
            {
                // Add/Remove are no-ops (and raise no event) when already in the desired state, so a redundant
                // set does nothing. A real change raises DeletionSelection.Changed, which the handler turns into
                // the PropertyChanged notification — keeping the notify logic in one place (mirrors DuplicateFile).
                if (value)
                    DeletionSelection.Add(FullName, Size);
                else
                    DeletionSelection.Remove(FullName);
                return;
            }

            // Directory: eagerly enumerate the WHOLE subtree (not just loaded children) on a background thread and
            // mark/unmark every descendant file, so a collapsed folder marks the files it has not loaded too.
            if (value)
                StartMarkScan();
            else
                UnmarkSubtree();
        }
    }

    /// <summary>
    /// Cancels any in-flight subtree scan for this directory node and starts a new cancellable background scan that
    /// walks the whole subtree (skipping reparse-point directories) and, ON COMPLETION ONLY, atomically commits the
    /// collected files plus this directory to the <see cref="DeletionSelection"/> on the UI thread. If the scan is
    /// cancelled before it finishes, nothing is committed (no partial half-marked folder is left behind).
    /// </summary>
    private void StartMarkScan()
    {
        var deletionSelection = DeletionSelection;
        if (deletionSelection is null)
            return;

        CancelPendingScan();
        var cts = new CancellationTokenSource();
        _scanCts = cts;
        var token = cts.Token;
        var rootPath = FullName;

        // Mark this node and every ancestor busy BEFORE the work starts. This runs on the UI thread (the setter does),
        // so the overlay shows up immediately. Exactly one EndScan below balances it on every exit path.
        BeginScan();
        // Tracks that this run still owns its BeginScan, so EndScan fires once and only once regardless of which path
        // (success/cancel/exception) runs first. Mutated only on the UI thread (the lambda below dispatcher-invokes).
        var ended = false;
        void EndScanOnce()
        {
            if (ended)
                return;
            ended = true;
            EndScan();
        }

        _ = Task.Run(() =>
        {
            try
            {
                var files = new List<(string FullName, long Size)>();
                CollectSubtreeFiles(rootPath, files, token);

                // Atomic commit: only after the walk completes (not cancelled) do we apply ANY change, on the UI
                // thread. A cancellation observed mid-walk throws OperationCanceledException and applies nothing.
                token.ThrowIfCancellationRequested();
                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    if (token.IsCancellationRequested)
                        return;
                    deletionSelection.AddDirectory(rootPath);
                    foreach (var (fullName, size) in files)
                        deletionSelection.Add(fullName, size);
                    // Self-notify covers the empty-folder case (no file events fire). For non-empty folders the
                    // per-file Added events already notify this node via the subtree-prefix handler — harmless to
                    // also self-notify.
                    OnPropertyChanged(nameof(IsMarkedForDeletion));
                });
            }
            catch (OperationCanceledException)
            {
                // Cancelled before commit: leave the selection untouched (atomic per folder).
            }
            finally
            {
                // Balance the BeginScan exactly once for THIS run on every path (success, cancel, exception). EndScan
                // raises a UI-bound PropertyChanged, so marshal it to the UI thread. Invoke (not BeginInvoke) so the
                // success path runs it on the same dispatcher pump as the commit above; EndScanOnce guards re-entry.
                // If the app is shutting down (Application.Current null), there is no UI to update — let the counter go.
                System.Windows.Application.Current?.Dispatcher.Invoke(EndScanOnce);

                // Clear our reference only if it still points at this run (a newer scan may have replaced it).
                Interlocked.CompareExchange(ref _scanCts, null, cts);
                cts.Dispose();
            }
        }, token);
    }

    /// <summary>
    /// Marks this node and every ancestor up the <see cref="Parent"/> chain as having one more in-flight subtree scan
    /// (issue 022). On any node whose count crosses 0→1, raises <see cref="IsScanBusy"/> so the column overlay shows.
    /// Must be called on the UI thread (it raises a UI-bound PropertyChanged); the 012 setter already runs there.
    /// </summary>
    private void BeginScan()
    {
        for (var node = this; node is not null; node = node.Parent)
        {
            node._subtreeScanCount++;
            if (node._subtreeScanCount == 1)
                node.OnPropertyChanged(nameof(IsScanBusy));
        }
    }

    /// <summary>
    /// Balances a <see cref="BeginScan"/>: decrements this node and every ancestor's in-flight scan count, and on any
    /// node whose count crosses 1→0 raises <see cref="IsScanBusy"/> so the column overlay clears. Guards against a
    /// negative count. Must be called on the UI thread (it raises a UI-bound PropertyChanged).
    /// </summary>
    private void EndScan()
    {
        for (var node = this; node is not null; node = node.Parent)
        {
            if (node._subtreeScanCount == 0)
                continue;
            node._subtreeScanCount--;
            if (node._subtreeScanCount == 0)
                node.OnPropertyChanged(nameof(IsScanBusy));
        }
    }

    /// <summary>
    /// Cancels any in-flight scan and removes every marked descendant file plus this directory (and its
    /// sub-directories) from the selection. No disk walk is needed — the marked entries are exactly what is in the
    /// set under this path. Each removed file fires a Removed event; the self-notify covers the empty-folder case.
    /// </summary>
    private void UnmarkSubtree()
    {
        var deletionSelection = DeletionSelection;
        if (deletionSelection is null)
            return;

        CancelPendingScan();
        deletionSelection.RemoveAllUnder(FullName);
        OnPropertyChanged(nameof(IsMarkedForDeletion));
    }

    /// <summary>
    /// Cancels this node's in-flight subtree scan, if any. Issues 018/020 call this on a group change (OQ-5).
    /// Calling it when nothing is running is a safe no-op.
    /// </summary>
    public void CancelPendingScan()
    {
        var cts = Interlocked.Exchange(ref _scanCts, null);
        if (cts is null)
            return;
        try { cts.Cancel(); }
        catch (ObjectDisposedException) { /* the scan finished and disposed it first; nothing to cancel. */ }
    }

    /// <summary>
    /// Recursively collects every regular file (full path + size) under <paramref name="dirPath"/> using one
    /// <see cref="DirectoryEnumeration"/> per directory level. Reparse-point directories are never entered (a
    /// junction can never loop or reach through to its target); reparse-point FILES are collected as normal files.
    /// The cancellation token is honored between entries.
    /// </summary>
    private static void CollectSubtreeFiles(string dirPath, List<(string FullName, long Size)> files, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        List<FileData> level;
        try
        {
            level = [.. new DirectoryEnumeration(dirPath)];
        }
        catch (Exception)
        {
            // An inaccessible directory contributes nothing, mirroring LoadChildren's swallow.
            return;
        }

        foreach (var item in level)
        {
            token.ThrowIfCancellationRequested();

            var isDirectory = item.Attributes.IsDirectory || item.Attributes.IsDevice;
            if (isDirectory)
            {
                // Skip reparse-point directories entirely (never enumerate into a junction/symlink).
                if (item.Attributes.IsReparsePoint)
                    continue;
                CollectSubtreeFiles(item.FullName, files, token);
            }
            else
            {
                files.Add((item.FullName, item.Size));
            }
        }
    }

    /// <summary>
    /// True when this loaded subtree contains at least one real (non-placeholder) file node. Used by the
    /// directory getter so a child directory that holds only empty subdirectories does not count as a
    /// descendant file and cannot make its parent appear marked.
    /// </summary>
    private bool HasLoadedDescendantFile()
    {
        if (ChildrenContainsOnlyPlaceholder)
            return false;

        foreach (var child in Children)
        {
            if (!child.IsDirectory)
                return true;
            if (child.HasLoadedDescendantFile())
                return true;
        }

        return false;
    }

    /// <summary>
    /// Fired when any writer mutates the set (this node's setter, the results tree, a Reset, or the future
    /// folder-tree writers). Cheaply early-returns when the change does not touch this node, then raises
    /// <see cref="IsMarkedForDeletion"/> so the row re-reads its derived state.
    /// </summary>
    private void OnDeletionSelectionChanged(object? sender, DeletionSelectionChangedEventArgs eventArgs)
    {
        // A file's zero-survivor state can flip whenever ANY sibling in its group is marked/unmarked — and a group
        // sibling may live in a completely different folder/tree, so this is NOT gated on the own-path/subtree-prefix
        // check below. Directory nodes are always non-zero-survivors, so they skip this.
        if (!IsDirectory)
            OnPropertyChanged(nameof(IsZeroSurvivor));

        if (eventArgs.Change == DeletionSelectionChange.Reset)
        {
            OnPropertyChanged(nameof(IsMarkedForDeletion));
            return;
        }

        var changedPath = eventArgs.NormalizedPath;
        if (changedPath is null)
            return;

        bool affectsThisNode;
        if (!IsDirectory)
        {
            affectsThisNode = string.Equals(changedPath, DuplicateFileTool.DeletionSelection.Normalize(FullName), StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            // A directory's derived state can change whenever any descendant's membership changes — i.e. when the
            // changed path lies within this directory's subtree — or when the changed path equals the directory's
            // own normalized path (if a directory path ever surfaces as a file event; harmless otherwise, since the
            // directory-set methods are silent and the directory row's normal notify path is the descendant file
            // events plus the explicit self-notify in the setter).
            var normalizedSelf = DuplicateFileTool.DeletionSelection.Normalize(FullName);
            var prefix = normalizedSelf.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                         + Path.DirectorySeparatorChar;
            affectsThisNode = string.Equals(changedPath, normalizedSelf, StringComparison.OrdinalIgnoreCase)
                              || changedPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        if (!affectsThisNode)
            return;

        OnPropertyChanged(nameof(IsMarkedForDeletion));
    }

    /// <summary>
    /// Fired when the engine raises a property change. Only the current-comparison-group switch is relevant: it changes
    /// which file is each column's corresponding group file and a duplicate's zero-survivor verdict (computed from its
    /// group), so both flags are re-raised. Subscribed weakly so rebuilt folder trees do not pin stale nodes.
    /// </summary>
    private void OnEnginePropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName != nameof(DuplicatesEngine.CurrentComparisonGroup))
            return;

        OnPropertyChanged(nameof(IsCorrespondingGroupFile));
        OnPropertyChanged(nameof(IsZeroSurvivor));
    }

    private FolderItem()
    {
        ItemFileData = FileData.Empty;
        Name = "";
    }

    public FolderItem(FileData fileData, DuplicatesEngine engine, FolderItem? parent = null)
    {
        ItemFileData = fileData;
        Engine = engine;
        Parent = parent;
        Level = parent is null ? 0 : parent.Level + 1;
        Name = fileData.FileName;

        UpdateIcon();

        if (CanExpand)
            Children.Add(ChildPlaceholder);

        // Weak subscriptions: each folder tree is rebuilt on demand, but the engine and its DeletionSelection are
        // long-lived. A strong handler would pin every stale FolderItem alive and let its handler keep firing forever
        // (the accumulating-handler bug class fixed before in this project). Subscribing weakly lets dead nodes be
        // collected so their handlers stop firing — no leak across rebuilds. Mirrors DuplicateFile in DuplicatesEngine.cs.
        WeakEventManager<DeletionSelection, DeletionSelectionChangedEventArgs>.AddHandler(
            engine.DeletionSelection, nameof(DuplicateFileTool.DeletionSelection.Changed), OnDeletionSelectionChanged);
        WeakEventManager<INotifyPropertyChanged, PropertyChangedEventArgs>.AddHandler(
            engine, nameof(INotifyPropertyChanged.PropertyChanged), OnEnginePropertyChanged);

        StartSizeScan();
    }

    /// <summary>
    /// Starts a background walk that sums the sizes of every file under this directory and publishes the total as
    /// <see cref="Size"/> (Win32 reports a directory entry's own size as 0). Skipped for files/junctions and for the
    /// column-root node — the root is never drawn as a row (only its children are), so summing its whole, largest
    /// subtree would be wasted work. Reparse-point sub-directories are never entered, mirroring the mark/deletion walks.
    /// The result is dropped if this node is discarded before the walk finishes (it only sets a field on a dead node).
    /// </summary>
    private void StartSizeScan()
    {
        if (!CanExpand || Parent is null)
            return;

        var rootPath = FullName;
        _ = Task.Run(() =>
        {
            long total;
            try { total = SumSubtreeSize(rootPath); }
            catch (Exception) { return; } // an inaccessible folder contributes nothing; leave the size at 0.

            System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                _directorySize = total;
                OnPropertyChanged(nameof(Size));
            });
        });
    }

    /// <summary>
    /// Recursively sums the sizes of every regular file under <paramref name="dirPath"/> using one
    /// <see cref="DirectoryEnumeration"/> per directory level. Reparse-point directories are never entered (a
    /// junction can never loop or reach through to its target); an inaccessible directory contributes nothing.
    /// </summary>
    private static long SumSubtreeSize(string dirPath)
    {
        List<FileData> level;
        try { level = [.. new DirectoryEnumeration(dirPath)]; }
        catch (Exception) { return 0; }

        var sum = 0L;
        foreach (var item in level)
        {
            if (item.Attributes.IsDirectory || item.Attributes.IsDevice)
            {
                if (item.Attributes.IsReparsePoint)
                    continue;
                sum += SumSubtreeSize(item.FullName);
            }
            else
            {
                sum += item.Size;
            }
        }

        return sum;
    }

    private void UpdateIcon() =>
        Icon = FileSystemIcon.GetImageSource(ItemFileData.FullName, IsExpanded ? Win32.ItemState.Open : Win32.ItemState.Close);

    private void LoadChildren()
    {
        if (!CanExpand)
            return;

        if (ChildrenContainsOnlyPlaceholder)
            Children.Clear();

        try
        {
            var directoryContent = new List<FileData>(new DirectoryEnumeration(ItemFileData.FullName));
            SortFileData(directoryContent);
            foreach (var fileData in directoryContent)
                Children.Add(new FolderItem(fileData, Engine!, this));
        }
        catch (Exception)
        {
            Children.Clear();
        }
    }

    private void RemoveChildren()
    {
        Children.Clear();
        if (CanExpand)
            Children.Add(ChildPlaceholder);
    }

    private static void SortFileData(List<FileData> directoryContent)
    {
        directoryContent.Sort((left, right) =>
        {
            if (left.Attributes.IsDirectory == right.Attributes.IsDirectory)
                return string.Compare(left.FileName, right.FileName, StringComparison.OrdinalIgnoreCase);
            return left.Attributes.IsDirectory ? -1 : 1;
        });
    }
}
