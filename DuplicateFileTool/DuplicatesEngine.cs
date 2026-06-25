using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using DuplicateFileTool.Converters;
using DuplicateFileTool.Properties;
using Application = System.Windows.Application;

namespace DuplicateFileTool;

[DebuggerDisplay("{FileData.FullName,nq}")]
internal sealed class DuplicateFile : NotifyPropertyChanged
{
    private bool _isSelected;

    public static event EventHandler? ItemSelected;

    // The engine-owned set is the single source of truth for "marked for deletion"; this file is a view over it.
    private DeletionSelection DeletionSelection { get; }
    private DuplicateGroup ParentGroup { get; }
    public FileData FileData { get; }
    public int MatchValue { get; }
    public int CompleteMatch { get; }
    public int CompleteMismatch { get; }

    public DuplicateFile(DuplicateGroup parentGroup, MatchResult matchResult, DeletionSelection deletionSelection)
    {
        ParentGroup = parentGroup;
        FileData = matchResult.ComparableFile.FileData;
        MatchValue = matchResult.MatchValue;
        CompleteMatch = matchResult.CompleteMatch;
        CompleteMismatch = matchResult.CompleteMismatch;
        DeletionSelection = deletionSelection;

        // Weak subscription: every search clears DuplicateGroups and rebuilds these view-models, but DeletionSelection
        // is long-lived (engine-owned). A strong handler would pin every stale DuplicateFile alive and let its handler
        // keep firing on later searches (the accumulating-handler bug class fixed before in this project). Subscribing
        // weakly lets dead view-models be collected, so their handlers stop firing — no leak across repeated searches.
        WeakEventManager<DeletionSelection, DeletionSelectionChangedEventArgs>.AddHandler(
            deletionSelection, nameof(DeletionSelection.Changed), OnDeletionSelectionChanged);
    }

    public string FileFullName => FileData.FullName;
    public string FileSize => FileData.Size.BytesLengthToString();
    public string LastWriteTimeText => FileData.LastWriteTime.ToString("g");
    public bool IsMarkForDeletionVisible => !IsMarkedForDeletion && ParentGroup.DuplicateFiles.Count(file => !file.IsMarkedForDeletion) > 1;
    public bool IsMarkedForDeletion
    {
        // Membership lives in the set, keyed by normalized path; there is no per-file backing flag.
        get => DeletionSelection.Contains(FileData.FullName);
        set
        {
            // Add/Remove are no-ops (and raise no event) when the file is already in the desired state, so a redundant
            // set does nothing. A real change raises DeletionSelection.Changed, which OnDeletionSelectionChanged turns
            // into the PropertyChanged notifications (self + siblings) below — keeping the notify logic in one place.
            if (value)
                DeletionSelection.Add(FileData.FullName, FileData.Size);
            else
                DeletionSelection.Remove(FileData.FullName);
        }
    }

    // Fired when any writer mutates the set (results-tree setter above, Reset, or — later — the folder tree). When the
    // change concerns this file's path (or is a Reset, which affects every path), update this row and re-raise the
    // siblings' IsMarkForDeletionVisible, because the group's unmarked-copy count just changed. This replicates the
    // sibling-notify behavior the old setter performed inline.
    private void OnDeletionSelectionChanged(object? sender, DeletionSelectionChangedEventArgs eventArgs)
    {
        var affectsThisFile = eventArgs.Change == DeletionSelectionChange.Reset
                              || string.Equals(eventArgs.NormalizedPath, DeletionSelection.Normalize(FileData.FullName), StringComparison.OrdinalIgnoreCase);
        if (!affectsThisFile)
            return;

        OnPropertyChanged(nameof(IsMarkedForDeletion));
        OnPropertyChanged(nameof(IsMarkForDeletionVisible));

        foreach (var duplicatedFile in ParentGroup.DuplicateFiles)
        {
            if (duplicatedFile == this)
                continue;
            duplicatedFile.OnPropertyChanged(nameof(IsMarkForDeletionVisible));
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
                return;
            _isSelected = value;
#pragma warning disable S4220
            ItemSelected?.Invoke(this, EventArgs.Empty);
#pragma warning restore S4220
        }
    }

    // Leaf rows never expand, but the results tree's shared ItemContainerStyle (MainWindow.xaml) binds
    // TreeListViewItem.IsExpanded for every row — groups and files alike. Without this property that TwoWay
    // binding logs a harmless "property not found" trace (System.Windows.Data Error: 40) on every file row.
    // A plain store is enough: a file is always collapsed, so nothing reads it back and no change notification is needed.
    public bool IsExpanded { get; set; }
}

[DebuggerDisplay("Group: {GroupNumber,nq}, Files: {FilesCount,nq}, Duplicated: {DuplicatedSizeText,nq}")]
internal sealed class DuplicateGroup : NotifyPropertyChanged
{
    private int _groupNumber;
    private int _filesCount;
    private long _duplicatedSize;
    private string _duplicatedSizeText = "";
    private bool _isSelected;
    private bool _isExpanded = true;

    public static event EventHandler? ItemSelected;

    public int GroupNumber
    {
        get => _groupNumber;
        set
        {
            if (_groupNumber == value)
                return;
            _groupNumber = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(GroupCaption));
        }
    }
    public int FilesCount
    {
        get => _filesCount;
        set
        {
            if (_filesCount == value)
                return;
            _filesCount = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(GroupCaption));
        }
    }
    public string GroupCaption => string.Format(Resources.Ui_Results_Group_Caption, GroupNumber, FilesCount);
    public long DuplicatedSize
    {
        get => _duplicatedSize;
        set
        {
            if (_duplicatedSize == value)
                return;
            _duplicatedSize = value;
            OnPropertyChanged();
        }
    }
    public string DuplicatedSizeText
    {
        get => _duplicatedSizeText;
        set
        {
            if (_duplicatedSizeText == value)
                return;
            _duplicatedSizeText = value;
            OnPropertyChanged();
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            _isSelected = value;
            OnPropertyChanged();
#pragma warning disable S4220
            ItemSelected?.Invoke(this, EventArgs.Empty);
#pragma warning restore S4220
        }
    }
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value)
                return;
            _isExpanded = value;
            OnPropertyChanged();
        }
    }
    public ObservableCollection<DuplicateFile> DuplicateFiles { get; }

    public DuplicateGroup(IEnumerable<MatchResult> duplicateFiles, DeletionSelection deletionSelection)
    {
        DuplicateFiles = [];
        foreach (var duplicateFile in duplicateFiles.OrderBy(df => df.ComparableFile.FileData.FullName))
            DuplicateFiles.Add(new DuplicateFile(this, duplicateFile, deletionSelection));
        OnDuplicateFilesCollectionChanged(this);
        DuplicateFiles.CollectionChanged += OnDuplicateFilesCollectionChanged;
    }

    private void OnDuplicateFilesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs? eventArgs = null)
    {
        FilesCount = DuplicateFiles.Count;
        var duplicatedSize = GetDuplicatedSize();
        DuplicatedSize = duplicatedSize;
        DuplicatedSizeText = duplicatedSize.BytesLengthToString();
    }

    private long GetDuplicatedSize()
    {
        var size = 0L;
        var matchExcluded = false;
        foreach (var duplicateFile in DuplicateFiles)
        {
            if (!matchExcluded && duplicateFile.MatchValue == duplicateFile.CompleteMatch)
                matchExcluded = true;
            else
                size += duplicateFile.FileData.Size;
        }

        return size;
    }
}

[Localizable(true)]
internal sealed class DuplicatesEngine : NotifyPropertyChanged
{
    #region Backing Fields
    private int _includedFilesCount;
    private int _progressPercentage;
    private string _progressText = "";
    private int _currentFileIndex;
    private int _totalFilesCount;
    private int _candidateGroupsCount;
    private int _candidateFilesCount;
    private long _candidatesTotalSize;
    private int _duplicateGroupsCount;
    private int _duplicateFilesCount;
    private long _duplicatedTotalSize;
    private long _toBeDeletedSize;
    private long _toBeDeletedCount;
    private DuplicateGroup? _currentComparisonGroup;
    private string? _selectedDuplicateFilePath;

    #endregion

    // Progress events arrive per processed file; pushing each one through PropertyChanged floods the UI thread.
    // The handlers below always record the latest values in the backing fields but raise change notifications
    // at most every 250 ms for text/counters and every 100 ms for the progress bar; final values are flushed explicitly.
    private readonly UpdateThrottle _textUpdateThrottle = new(250);
    private readonly UpdateThrottle _barUpdateThrottle = new(100);
    private string _currentProgressPath = "";
    private string _lastDeletionMessage = "";

    private FilesSearch Files { get; } = new();
    private DuplicateCandidates Candidates { get; } = new();
    private DuplicatesSearch Duplicates { get; } = new();
    private DuplicatesRemover DuplicatesRemover { get; } = new();

    public int IncludedFilesCount
    {
        get => _includedFilesCount;
        private set
        {
            _includedFilesCount = value; 
            OnPropertyChanged();
        }
    }
    public int ProgressPercentage //0 - 10000
    {
        get => _progressPercentage;
        private set
        {
            _progressPercentage = value; 
            OnPropertyChanged();
        }
    }
    public string ProgressText
    {
        get => _progressText;
        set
        {
            _progressText = value;
            OnPropertyChanged();
        }
    }
    public int CurrentFileIndex
    {
        get => _currentFileIndex;
        private set
        {
            _currentFileIndex = value; 
            OnPropertyChanged();
        }
    }
    public int TotalFilesCount
    {
        get => _totalFilesCount;
        private set
        {
            _totalFilesCount = value; 
            OnPropertyChanged();
        }
    }

    public int CandidateGroupsCount
    {
        get => _candidateGroupsCount;
        private set
        {
            _candidateGroupsCount = value; 
            OnPropertyChanged();
        }
    }
    public int CandidateFilesCount
    {
        get => _candidateFilesCount;
        private set
        {
            _candidateFilesCount = value;
            OnPropertyChanged();
        }
    }
    public long CandidatesTotalSize
    {
        get => _candidatesTotalSize;
        private set
        {
            _candidatesTotalSize = value; 
            OnPropertyChanged();
        }
    }

    public int DuplicateGroupsCount
    {
        get => _duplicateGroupsCount;
        private set
        {
            _duplicateGroupsCount = value; 
            OnPropertyChanged();
        }
    }
    public int DuplicateFilesCount
    {
        get => _duplicateFilesCount;
        private set
        {
            _duplicateFilesCount = value; 
            OnPropertyChanged();
        }
    }
    public long DuplicatedTotalSize
    {
        get => _duplicatedTotalSize;
        private set
        {
            _duplicatedTotalSize = value; 
            OnPropertyChanged();
        }
    }
    public long ToBeDeletedSize
    {
        get => _toBeDeletedSize;
        set
        {
            _toBeDeletedSize = value;
            OnPropertyChanged();
        }
    }
    public long ToBeDeletedCount
    {
        get => _toBeDeletedCount;
        set
        {
            _toBeDeletedCount = value;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<ErrorMessage> Errors { get; } = [];
    public ObservableCollection<DuplicateGroup> DuplicateGroups { get; } = [];

    // The single, engine-owned source of truth for files marked for deletion, keyed by normalized full path.
    // Not yet wired into totals or marking (issues 003/004 make IsMarkedForDeletion a view over this); it is
    // exposed here so views and commands can route their marks through one set.
    public DeletionSelection DeletionSelection { get; } = new();

    // The duplicate group currently selected in the results tree, surfaced to the folder-comparison control so a
    // folder-tree row can tell whether it belongs to that group (FolderItem.BelongsToCurrentGroup). Purely a holder;
    // issue 020 drives it from results-tree selection. Initially null, so belonging is false until then.
    public DuplicateGroup? CurrentComparisonGroup
    {
        get => _currentComparisonGroup;
        set
        {
            if (ReferenceEquals(_currentComparisonGroup, value))
                return;
            _currentComparisonGroup = value;
            OnPropertyChanged();
        }
    }

    // The full path of the duplicate file currently selected in the results tree (null when none), surfaced to the
    // folder-comparison control so the column whose folder contains that file can light up its outer background
    // (FolderComparisonItem.IsSelectedColumn). Persisted on the engine (not just the transient DuplicateFile.ItemSelected
    // event) so a column rebuilt right after a selection can restore the highlight from this value. Issue 021 drives it
    // from results-tree selection; set on select only (never cleared on deselect, to avoid flicker), mirroring
    // CurrentComparisonGroup.
    public string? SelectedDuplicateFilePath
    {
        get => _selectedDuplicateFilePath;
        set
        {
            if (ReferenceEquals(_selectedDuplicateFilePath, value) ||
                string.Equals(_selectedDuplicateFilePath, value, StringComparison.Ordinal))
                return;
            _selectedDuplicateFilePath = value;
            OnPropertyChanged();
        }
    }

    #region Group Membership Index

    // Path -> owning duplicate group, keyed by DeletionSelection.Normalize(...) so the same file resolves to one
    // entry regardless of long-path prefixes or letter case. Maintained incrementally from the collection-changed
    // notifications of DuplicateGroups (group add/remove/reset) and of each group's DuplicateFiles (files removed
    // during a deletion run), so it never holds stale entries. All mutations of those collections happen on the UI
    // dispatcher (group adds in the DuplicatesGroupFound handler / Clear, file and group removals in
    // DuplicatesRemover), so the handlers below always run on the same thread; the lock only guards against query
    // methods being called from a different thread.
    private readonly object _membershipIndexLock = new();
    private readonly Dictionary<string, DuplicateGroup> _groupByPath = new(StringComparer.OrdinalIgnoreCase);
    // Groups whose DuplicateFiles.CollectionChanged we are currently subscribed to. Tracked so a Reset of the
    // group collection (DuplicateGroups.Clear, fired without OldItems) can unsubscribe every per-group handler
    // and not leak a subscription per search.
    private readonly HashSet<DuplicateGroup> _subscribedGroups = [];

    /// <summary>Tells whether the given path is a member of any duplicate group.</summary>
    public bool IsDuplicate(string fullPath)
    {
        var key = DeletionSelection.Normalize(fullPath);
        lock (_membershipIndexLock)
            return _groupByPath.ContainsKey(key);
    }

    /// <summary>Returns the duplicate group the given path belongs to, or null when the path is not a duplicate.</summary>
    public DuplicateGroup? GetGroupForPath(string fullPath)
    {
        var key = DeletionSelection.Normalize(fullPath);
        lock (_membershipIndexLock)
            return _groupByPath.GetValueOrDefault(key);
    }

    /// <summary>
    /// Answers, against the live <see cref="DeletionSelection"/> contents, whether deleting the current selection
    /// would leave no surviving copy of the given path:
    /// <list type="bullet">
    /// <item>a non-duplicate path: true exactly when the path itself is in the selection;</item>
    /// <item>a duplicate path: true only when every file in its group is in the selection.</item>
    /// </list>
    /// The selection is queried on demand, so the answer flips as the last sibling of a group is added/removed.
    /// </summary>
    public bool WouldLeaveZeroSurvivors(string fullPath)
    {
        DuplicateGroup? group;
        var key = DeletionSelection.Normalize(fullPath);
        lock (_membershipIndexLock)
            group = _groupByPath.GetValueOrDefault(key);

        if (group is null)
            return DeletionSelection.Contains(fullPath); // non-duplicate: gone only if it is itself marked

        // Duplicate: a copy survives unless every file in the group is marked.
        foreach (var file in group.DuplicateFiles)
        {
            if (!DeletionSelection.Contains(file.FileData.FullName))
                return false;
        }

        return true;
    }

    private void OnDuplicateGroupsCollectionChangedIndex(object? sender, NotifyCollectionChangedEventArgs eventArgs)
    {
        switch (eventArgs.Action)
        {
            case NotifyCollectionChangedAction.Add:
                if (eventArgs.NewItems != null)
                    foreach (DuplicateGroup group in eventArgs.NewItems)
                        AddGroupToIndex(group);
                break;
            case NotifyCollectionChangedAction.Remove:
                if (eventArgs.OldItems != null)
                    foreach (DuplicateGroup group in eventArgs.OldItems)
                        RemoveGroupFromIndex(group);
                break;
            case NotifyCollectionChangedAction.Replace:
                if (eventArgs.OldItems != null)
                    foreach (DuplicateGroup group in eventArgs.OldItems)
                        RemoveGroupFromIndex(group);
                if (eventArgs.NewItems != null)
                    foreach (DuplicateGroup group in eventArgs.NewItems)
                        AddGroupToIndex(group);
                break;
            case NotifyCollectionChangedAction.Reset:
                RebuildIndexFromGroups();
                break;
            case NotifyCollectionChangedAction.Move:
            default:
                break; // a move does not change membership
        }
    }

    private void AddGroupToIndex(DuplicateGroup group)
    {
        lock (_membershipIndexLock)
        {
            foreach (var file in group.DuplicateFiles)
                _groupByPath[DeletionSelection.Normalize(file.FileData.FullName)] = group;
            if (_subscribedGroups.Add(group))
                group.DuplicateFiles.CollectionChanged += OnGroupFilesCollectionChangedIndex;
        }
    }

    private void RemoveGroupFromIndex(DuplicateGroup group)
    {
        lock (_membershipIndexLock)
        {
            if (_subscribedGroups.Remove(group))
                group.DuplicateFiles.CollectionChanged -= OnGroupFilesCollectionChangedIndex;
            // Drop only the keys that still resolve to this group; another group never owns the same path.
            foreach (var file in group.DuplicateFiles)
            {
                var key = DeletionSelection.Normalize(file.FileData.FullName);
                if (_groupByPath.TryGetValue(key, out var owner) && ReferenceEquals(owner, group))
                    _groupByPath.Remove(key);
            }
        }
    }

    private void OnGroupFilesCollectionChangedIndex(object? sender, NotifyCollectionChangedEventArgs eventArgs)
    {
        // The deletion run mutates a group's DuplicateFiles via RemoveAt (Remove); the other actions are handled
        // defensively so the index can never drift if the collection is ever mutated another way.
        var group = GetOwningGroup(sender);

        lock (_membershipIndexLock)
        {
            if (eventArgs.Action == NotifyCollectionChangedAction.Reset)
            {
                // Reset carries no item lists: drop every key still owned by this group, then re-index survivors.
                if (group != null)
                {
                    var stale = _groupByPath.Where(kvp => ReferenceEquals(kvp.Value, group)).Select(kvp => kvp.Key).ToList();
                    foreach (var key in stale)
                        _groupByPath.Remove(key);
                    foreach (var file in group.DuplicateFiles)
                        _groupByPath[DeletionSelection.Normalize(file.FileData.FullName)] = group;
                }
                return;
            }

            if (eventArgs.OldItems != null)
                foreach (DuplicateFile file in eventArgs.OldItems)
                {
                    var key = DeletionSelection.Normalize(file.FileData.FullName);
                    if (_groupByPath.TryGetValue(key, out var owner) && (group is null || ReferenceEquals(owner, group)))
                        _groupByPath.Remove(key);
                }

            if (eventArgs.NewItems != null && group != null)
                foreach (DuplicateFile file in eventArgs.NewItems)
                    _groupByPath[DeletionSelection.Normalize(file.FileData.FullName)] = group;
        }
    }

    private DuplicateGroup? GetOwningGroup(object? filesCollection)
    {
        foreach (var group in DuplicateGroups)
        {
            if (ReferenceEquals(group.DuplicateFiles, filesCollection))
                return group;
        }
        return null;
    }

    private void RebuildIndexFromGroups()
    {
        lock (_membershipIndexLock)
        {
            // A Reset carries no OldItems, so unsubscribe every previously tracked per-group handler up front to
            // avoid leaking one per search, then re-index and re-subscribe whatever groups remain after the Reset.
            foreach (var group in _subscribedGroups)
                group.DuplicateFiles.CollectionChanged -= OnGroupFilesCollectionChangedIndex;
            _subscribedGroups.Clear();
            _groupByPath.Clear();

            foreach (var group in DuplicateGroups)
            {
                foreach (var file in group.DuplicateFiles)
                    _groupByPath[DeletionSelection.Normalize(file.FileData.FullName)] = group;
                if (_subscribedGroups.Add(group))
                    group.DuplicateFiles.CollectionChanged += OnGroupFilesCollectionChangedIndex;
            }
        }
    }

    #endregion

    public DuplicatesEngine()
    {
        Files.FilesSearchProgress += OnFilesSearchProgress;
        Files.FileSystemError += OnFileSystemError;
            
        Candidates.CandidatesSearchProgress += OnCandidatesSearchProgress;
        Candidates.FileSystemError += OnFileSystemError;

        Duplicates.DuplicatesSearchProgress += OnDuplicatesSearchProgress;
        Duplicates.FileSystemError += OnFileSystemError;
        Duplicates.DuplicatesGroupFound += (_, args) => Application.Current?.Dispatcher.Invoke(() => DuplicateGroups.Add(new DuplicateGroup(args.DuplicatesGroup, DeletionSelection) { GroupNumber = DuplicateGroups.Count + 1 }));

        DuplicateGroups.CollectionChanged += OnDuplicateGroupsCollectionChanged;
        DuplicateGroups.CollectionChanged += OnDuplicateGroupsCollectionChangedIndex;

        DuplicatesRemover.DeletionMessage += OnDeletionMessage;
        DuplicatesRemover.DeletionStateChanged += DeletionStateChanged;

        // Marking (from any view/command) mutates DeletionSelection; the engine maintains the to-be-deleted totals
        // from those change deltas. The deletion run keeps owning the per-file decrements via DeletionStateChanged.
        DeletionSelection.Changed += OnDeletionSelectionChanged;
    }

    // Keeps ToBeDeletedCount/ToBeDeletedSize in step with the selection set as files are marked/unmarked. This drives
    // the "selected for deletion" totals immediately (the public setters raise PropertyChanged); the deletion-time
    // decrements stay in DeletionStateChanged, which is throttled for the progress display.
    private void OnDeletionSelectionChanged(object? sender, DeletionSelectionChangedEventArgs eventArgs)
    {
        switch (eventArgs.Change)
        {
            case DeletionSelectionChange.Added:
                ToBeDeletedCount += 1;
                ToBeDeletedSize += eventArgs.AffectedSize;
                break;
            case DeletionSelectionChange.Removed:
                ToBeDeletedCount -= 1;
                ToBeDeletedSize -= eventArgs.AffectedSize;
                break;
            case DeletionSelectionChange.Reset:
                ToBeDeletedCount = 0;
                ToBeDeletedSize = 0;
                break;
        }
    }

    #region Finding Duplicates

    public async Task FindDuplicates(
        IReadOnlyCollection<SearchPath> searchPaths,
        IInclusionPredicate<FileData> inclusionPredicate,
        ICandidatePredicate duplicateCandidatePredicate,
        IComparableFileFactory comparableFileFactory,
        CancellationToken cancellationToken)
    {
        Clear();

        List<IComparableFile[]>? duplicateCandidates = null;
        try
        {
            //var startFindFiles = DateTime.UtcNow;
            var files = await Files.Find(searchPaths, inclusionPredicate, cancellationToken);
            //var endFindFiles = DateTime.UtcNow;

            //var startCandidates = DateTime.UtcNow;
            duplicateCandidates = await Candidates.Find(files, duplicateCandidatePredicate, comparableFileFactory, cancellationToken);
            //var endCandidates = DateTime.UtcNow;

            //var startSearch = DateTime.UtcNow;
            await Duplicates.Find(duplicateCandidates, comparableFileFactory.Config, cancellationToken);
            //var endSearch = DateTime.UtcNow;

            ////TODO This is a debug code. Remove it after testing.
            //// ReSharper disable LocalizableElement
            //var elapsedFindFiles = endFindFiles - startFindFiles;
            //var elapsedCandidates = endCandidates - startCandidates;
            //var elapsedSearch = endSearch - startSearch;
            //await File.AppendAllTextAsync(
            //    "Timings.txt",
            //    $"Start time : {startFindFiles.ToLocalTime():yyyy-MM-dd HH:mm:ss}{Environment.NewLine}" +
            //    $"Files      : {elapsedFindFiles.TotalMinutes:N0} min, {elapsedFindFiles.Seconds:00} sec.{Environment.NewLine}" +
            //    $"Candidates : {elapsedCandidates.TotalMinutes:N0} min, {elapsedCandidates.Seconds:00} sec.{Environment.NewLine}" +
            //    $"Comparison : {elapsedSearch.TotalMinutes:N0} min, {elapsedSearch.Seconds} sec.{Environment.NewLine}{Environment.NewLine}",
            //    CancellationToken.None);
            //// ReSharper restore LocalizableElement
        }
        catch (OperationCanceledException)
        {
            ProgressText = Resources.Ui_Progress_Duplicates_Search_Cancelled;
            ProgressPercentage = 0;
            RaiseProgressStatsChanged();
            return;
        }
        finally
        {
            if (duplicateCandidates != null)
                DisposeComparableFiles(duplicateCandidates);
            GC.Collect();
        }

        ProgressText = Resources.Ui_Progress_Duplicates_Search_Done;
        ProgressPercentage = 0;
        RaiseProgressStatsChanged();
    }

    public void Clear()
    {
        Errors.Clear();
        DuplicateGroups.Clear();
        // Clearing the results removes the group the folder-comparison columns were built from, so drop the comparison
        // selection too: the control rebuilds only on a CurrentComparisonGroup change, so without this it keeps showing
        // stale columns for the gone group. Mirrors the post-deletion re-drive in MainViewModel.OnDeleteMarkedFilesFinished.
        // Both callers (ClearResults command, FindDuplicates start) run Clear() on the UI thread, so the rebuild is safe.
        CurrentComparisonGroup = null;
        SelectedDuplicateFilePath = null;
        IncludedFilesCount = 0;
        CurrentFileIndex = 0;
        TotalFilesCount = 0;
        CandidateGroupsCount = 0;
        CandidateFilesCount = 0;
        CandidatesTotalSize = 0;
        DuplicateGroupsCount = 0;
        DuplicateFilesCount = 0;
        DuplicatedTotalSize = 0;
        // Start the new search from an empty selection (this also wipes any stale post-deletion entries until issue
        // 007 prunes the set). The raised Reset zeroes ToBeDeletedSize/ToBeDeletedCount via OnDeletionSelectionChanged;
        // the explicit assignments below cover the case where the set was already empty (no event) and are harmless.
        DeletionSelection.Clear();
        ToBeDeletedSize = 0;
        ToBeDeletedCount = 0;
        ProgressText = "";
        ProgressPercentage = 0;
        _currentProgressPath = "";
        _lastDeletionMessage = "";
        GC.Collect();
    }

    private static void DisposeComparableFiles(IEnumerable<IComparableFile[]> fileGroups)
    {
        foreach (var fileGroup in fileGroups)
        {
            foreach (var file in fileGroup)
            {
                if (file is IDisposable disposableFile)
                    disposableFile.Dispose();
            }
        }
    }

    private void OnFilesSearchProgress(object? sender, FilesSearchProgressEventArgs eventArgs)
    {
        if (eventArgs.IsDirectory)
            _currentProgressPath = eventArgs.Path;
        _includedFilesCount = eventArgs.FoundFilesCount;

        if (!_textUpdateThrottle.IsUpdateDue())
            return;
        ProgressText = Resources.Ui_Progress_Scanning + _currentProgressPath;
        OnPropertyChanged(nameof(IncludedFilesCount));
    }

    private void OnCandidatesSearchProgress(object? sender, CandidatesSearchProgressEventArgs eventArgs)
    {
        _currentProgressPath = eventArgs.FilePath;
        SetProgressStats(eventArgs.TotalFilesCount, eventArgs.CurrentFileIndex);
        _candidateGroupsCount = eventArgs.CandidateGroupsCount;
        _candidateFilesCount = eventArgs.CandidateFilesCount;
        _candidatesTotalSize = eventArgs.CandidatesTotalSize;

        if (_barUpdateThrottle.IsUpdateDue())
            OnPropertyChanged(nameof(ProgressPercentage));
        if (!_textUpdateThrottle.IsUpdateDue())
            return;
        ProgressText = Resources.Ui_Progress_Analyzing + _currentProgressPath;
        RaiseProgressStatsChanged();
    }

    private void OnDuplicatesSearchProgress(object? sender, DuplicatesSearchProgressEventArgs eventArgs)
    {
        if (eventArgs.FilePath.Length != 0)
            _currentProgressPath = eventArgs.FilePath;
        if (eventArgs.HasStats)
        {
            SetProgressStats(eventArgs.TotalFilesCount, eventArgs.CurrentFileIndex);
            _duplicateFilesCount = eventArgs.DuplicateFilesCount;
            _duplicatedTotalSize = eventArgs.DuplicatedTotalSize;
        }

        if (_barUpdateThrottle.IsUpdateDue())
            OnPropertyChanged(nameof(ProgressPercentage));
        if (!_textUpdateThrottle.IsUpdateDue())
            return;
        ProgressText = Resources.Ui_Progress_Comparing + _currentProgressPath;
        RaiseProgressStatsChanged();
    }

    private void SetProgressStats(int totalFilesCount, int currentFileIndex)
    {
        _totalFilesCount = totalFilesCount;
        _currentFileIndex = currentFileIndex;
        _progressPercentage = (int)((double)currentFileIndex * 10000 / totalFilesCount);
    }

    private void RaiseProgressStatsChanged()
    {
        OnPropertyChanged(nameof(IncludedFilesCount));
        OnPropertyChanged(nameof(TotalFilesCount));
        OnPropertyChanged(nameof(CurrentFileIndex));
        OnPropertyChanged(nameof(CandidateGroupsCount));
        OnPropertyChanged(nameof(CandidateFilesCount));
        OnPropertyChanged(nameof(CandidatesTotalSize));
        OnPropertyChanged(nameof(DuplicateFilesCount));
        OnPropertyChanged(nameof(DuplicatedTotalSize));
    }

    private void OnFileSystemError(object? sender, FileSystemErrorEventArgs eventArgs)
    {
        var fileSystemError = eventArgs.FileSystemError;
        var path = fileSystemError.Path;
        var message = fileSystemError.Message;
        Application.Current.Dispatcher.Invoke(() => Errors.Add(new ErrorMessage(path, message, MessageType.Error)));
    }

    private void OnDuplicateGroupsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs eventArgs) => 
        DuplicateGroupsCount = DuplicateGroups.Count;

#endregion

    #region Removing Duplicates

    public async Task RemoveDuplicates(ObservableCollection<DuplicateGroup> duplicates, bool removeEmptyDirs, bool deleteToRecycleBin, RecycleFailurePromptHandler promptRecycleFailure, CancellationToken cancellationToken)
    {
        await DuplicatesRemover.RemoveDuplicates(duplicates, DeletionSelection, IsDuplicate, removeEmptyDirs, deleteToRecycleBin, promptRecycleFailure, cancellationToken);
        ProgressText = _lastDeletionMessage;
        ProgressPercentage = 0;
        OnPropertyChanged(nameof(ToBeDeletedSize));
        OnPropertyChanged(nameof(DuplicatedTotalSize));
        OnPropertyChanged(nameof(ToBeDeletedCount));
    }

    private void OnDeletionMessage(object? sender, DeletionMessageEventArgs eventArgs)
    {
        var deletionMessage = eventArgs.Message;
        _lastDeletionMessage = deletionMessage.Text;
        var deletionMessageType = deletionMessage.Type;
        if (deletionMessageType is MessageType.Error or MessageType.Warning)
            Application.Current.Dispatcher.Invoke(() => Errors.Add(new ErrorMessage(deletionMessage.Path, deletionMessage.Text, deletionMessageType)));
        if (_textUpdateThrottle.IsUpdateDue())
            ProgressText = _lastDeletionMessage;
    }

    private void DeletionStateChanged(object? sender, DeletionStateEventArgs eventArgs)
    {
        var deletionState = eventArgs.State;
        var total = (double)deletionState.TotalFilesForDeletionCount;
        var current = (double)deletionState.CurrentFileForDeletionIndex;
        _progressPercentage = (int)(current * 10000 / total);
        var deletedSizeDelta = deletionState.DeletedSizeDelta;
        _toBeDeletedSize += deletedSizeDelta;
        _duplicatedTotalSize += deletedSizeDelta;
        _toBeDeletedCount += deletionState.DeletedCountDelta;

        if (_barUpdateThrottle.IsUpdateDue())
            OnPropertyChanged(nameof(ProgressPercentage));
        if (!_textUpdateThrottle.IsUpdateDue())
            return;
        OnPropertyChanged(nameof(ToBeDeletedSize));
        OnPropertyChanged(nameof(DuplicatedTotalSize));
        OnPropertyChanged(nameof(ToBeDeletedCount));
    }

    #endregion
}