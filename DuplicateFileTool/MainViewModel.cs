using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using DuplicateFileTool.Commands;
using DuplicateFileTool.Configuration;
using DuplicateFileTool.Properties;
using TreeView = System.Windows.Controls.TreeView;

namespace DuplicateFileTool;

public enum InclusionType { Include, Exclude }
public enum ByteSizeUnits { Bytes, Kilobytes, Megabytes, Gigabytes }
public enum SortOrder { Number, Size, Path, Name }

internal sealed class SearchPath : NotifyPropertyChanged
{
    private InclusionType _pathInclusionType;
    private string _path = "";
    private bool _isActive = true;

    public bool IsActive
    {
        get => _isActive;
        set
        {
            _isActive = value;
            OnPropertyChanged();
        }
    }

    public InclusionType PathInclusionType
    {
        get => _pathInclusionType;
        set
        {
            _pathInclusionType = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PathInclusionTypeName));
        }
    }

    public string PathInclusionTypeName => PathInclusionType switch
    {
        InclusionType.Include => Resources.Ui_Search_Path_Include,
        InclusionType.Exclude => Resources.Ui_Search_Path_Exclude,
        _ => ""
    };

    public string Path
    {
        get => _path;
        set
        {
            _path = value;
            OnPropertyChanged();
        }
    }

    // ReSharper disable once UnusedMember.Global
    public SearchPath()
    { }

    public SearchPath(string path, InclusionType pathInclusionType)
    {
        _path = path;
        _pathInclusionType = pathInclusionType;
    }
}

[Localizable(true)]
internal sealed class MainViewModel : NotifyPropertyChanged, IResultsFilter, IDisposable
{
    public TreeView ResultsTreeView { get; }

    #region Backing Fields
    private IFileComparer _selectedFileComparer;
    private FileTreeItem? _selectedFileTreeItem;
    private int _selectedTabIndex;
    private string _progressText = "";
    private double _progressPercentage;
    private double _taskbarProgress;
    private readonly UpdateThrottle _textUpdateThrottle = new(250);
    private readonly UpdateThrottle _barUpdateThrottle = new(100);
    private string _duplicatesSortingOrderToolTip = "";
    private string _resultsFilterKeywords = "";
    private bool _isFilterFilePath = true;
    private bool _isFilterFileName = true;
    private bool _isFilterFileExtension = true;
    private bool _isFilterCaseSensitive;
    private bool _isIncludeFilter = true;
    private bool _isExcludeFilter;

    #endregion

    public Configuration.Configuration Config { get; }

    public IInclusionPredicate<FileData> FileSearchInclusionPredicate { get; }
    public IReadOnlyCollection<IFileComparer> FileComparers { get; }
    public IFileComparer SelectedFileComparer
    {
        get => _selectedFileComparer;
        set
        {
            if (ReferenceEquals(_selectedFileComparer, value))
                return;
            _selectedFileComparer = value;
            Config.SearchConfig.SelectedFileComparerGuid.Value = value.Guid;
            OnPropertyChanged();
        }
    }
    public DuplicatesEngine Duplicates { get; }
    public ObservableCollectionProxy<DuplicateGroup> DuplicateGroupsProxyView { get; }

    public ObservableCollection<SearchPath> SearchPaths { get; } = [];
    public ObservableCollection<FileTreeItem> FileTree { get; } = [];
    public FileTreeItem? SelectedFileTreeItem
    {
        get => _selectedFileTreeItem;
        set
        {
            _selectedFileTreeItem = value;
            OnPropertyChanged();
        }
    }
    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set
        {
            _selectedTabIndex = value; 
            OnPropertyChanged();
        }
    }
    public string DuplicatesSortingOrderToolTip
    {
        get => _duplicatesSortingOrderToolTip;
        set
        {
            _duplicatesSortingOrderToolTip = value; 
            OnPropertyChanged();
        }
    }
    public DuplicateGroupComparer DuplicateGroupComparer { get; }

    #region IResultsFilter Implementation
    public bool IsFilterFilePath
    {
        get => _isFilterFilePath;
        set
        {
            _isFilterFilePath = value; 
            OnPropertyChanged();
        }
    }
    public bool IsFilterFileName
    {
        get => _isFilterFileName;
        set
        {
            _isFilterFileName = value;
            OnPropertyChanged();
        }
    }
    public bool IsFilterFileExtension
    {
        get => _isFilterFileExtension;
        set
        {
            _isFilterFileExtension = value;
            OnPropertyChanged();
        }
    }
    public bool IsFilterCaseSensitive
    {
        get => _isFilterCaseSensitive;
        set
        {
            _isFilterCaseSensitive = value; 
            OnPropertyChanged();
        }
    }
    public bool IsIncludeFilter
    {
        get => _isIncludeFilter;
        set
        {
            _isIncludeFilter = value;
            OnPropertyChanged();
        }
    }
    public bool IsExcludeFilter
    {
        get => _isExcludeFilter;
        set
        {
            _isExcludeFilter = value;
            OnPropertyChanged();
        }
    }
    public string ResultsFilterKeywords
    {
        get => _resultsFilterKeywords;
        set
        {
            _resultsFilterKeywords = value; 
            OnPropertyChanged();
        }
    }
    #endregion

    [Localizable(false)]
    public string ProgressText
    {
        get => _progressText;
        set
        {
            _progressText = value.Replace("\n", "").Replace("\r", "");
            OnPropertyChanged();
        }
    }
    public double ProgressPercentage
    {
        get => _progressPercentage;
        set
        {
            _progressPercentage = value;
            OnPropertyChanged();
        }
    }
    public double TaskbarProgress
    {
        get => _taskbarProgress;
        set
        {
            _taskbarProgress = value / 10000;
            OnPropertyChanged();
        }
    }

    #region Commands
    public FindDuplicatesCommand FindDuplicates { get; }
    public RelayCommand CancelDuplicatesSearch { get; }
    public AddPathCommand AddPath { get; }
    public ToggleDeletionMarkCommand ToggleDeletionMark { get; }
    public AutoSelectByPathCommand AutoSelectByPath { get; }
    public ResetSelectionCommand ResetSelection { get; }
    public DeleteMarkedFilesCommand DeleteMarkedFiles { get; }
    public OpenFileInExplorerCommand OpenFileInExplorer { get; }
    public ChangePageCommand ChangePage { get; }
    public RelayCommand ToggleDuplicateSortingOrder { get; }
    public RelayCommand ClearResultsFilter { get; }
    public RelayCommand ClearResults { get; }
    public RelayCommand ClearErrors { get; }
    public RelayCommand ClearSearchPaths { get; }
    public RelayCommand ToggleSearchPathActive { get; }
    public RelayCommand RefreshFileTree { get; }
    public RelayCommand ResetSettings { get; }
    public AddOrRemoveExtensionsCommand AddOrRemoveExtensions { get; }
    public RelayCommand ClearExtensions { get; }
    public RelayCommand Navigate { get; }

    #endregion

    public SearchConfiguration SearchConfig { get; }
    public UiSwitch Ui { get; } = new();

    public MainViewModel(TreeView resultsTreeView) //TODO get rid of resultsTreeView
    {
        ResultsTreeView = resultsTreeView;

        Config = new Configuration.Configuration();

        SearchConfig = Config.SearchConfig;

        FileSearchInclusionPredicate = new FileSearchInclusionPredicate(Config.SearchConfig);
        FileComparers = Config.FileComparers;
        Duplicates = new DuplicatesEngine();
        Duplicates.PropertyChanged += OnDuplicatesPropertyChanged;
        Duplicates.Errors.CollectionChanged += (_, _) => Ui.ErrorTabImageEnabled = Duplicates.Errors.Count != 0;
        Duplicates.DuplicateGroups.CollectionChanged += OnDuplicateGroupsCollectionChanged;

        var resultsGroupInclusionPredicate = new ResultsGroupInclusionPredicate(this);
        DuplicateGroupComparer = new DuplicateGroupComparer(Config.ResultsConfig);
        DuplicateGroupComparer.PropertyChanged += OnSortTypeChanged;
        DuplicateGroupsProxyView = new ObservableCollectionProxy<DuplicateGroup>(Duplicates.DuplicateGroups, resultsGroupInclusionPredicate, DuplicateGroupComparer, Config.ResultsConfig.ItemsPerPage.Value);
        DuplicateGroupsProxyView.PageChanged += OnResultsPageChanged;
        DuplicateGroupsProxyView.CollectionChanged += OnResultsCollectionProxyChanged;

        _selectedFileComparer = GetInitialSelectedFileComparer(Config, FileComparers) ?? FileComparers.First();
        Config.SearchConfig.SelectedFileComparerGuid.Value = _selectedFileComparer.Guid;

        SearchPaths.CollectionChanged += OnSearchPathsCollectionChanged;

        FindDuplicates = new FindDuplicatesCommand(Duplicates, SearchPaths, () => FileSearchInclusionPredicate, () => SelectedFileComparer);
        FindDuplicates.FindDuplicatesStarting += OnFindDuplicatesStarting;
        FindDuplicates.FindDuplicatesFinished += OnFindDuplicatesFinished;
        FindDuplicates.CanExecuteChanged += OnFindDuplicatesCanExecuteChanged;
        FindDuplicates.CanCancelChanged += OnFindDuplicatesCanCancelChanged;

        CancelDuplicatesSearch = new RelayCommand(_ => FindDuplicates.Cancel());

        ToggleDeletionMark = new ToggleDeletionMarkCommand();

        AutoSelectByPath = new AutoSelectByPathCommand(Duplicates.DuplicateGroups);
        AutoSelectByPath.Starting += OnAutoSelectByPathStarting;
        AutoSelectByPath.Finished += OnAutoSelectByPathFinished;
        AutoSelectByPath.Progress += OnAutoSelectByPathProgress;
        DuplicateFile.ItemSelected += OnDuplicateFileSelected;
        DuplicateGroup.ItemSelected += OnDuplicateGroupSelected;
        FileTreeItem.ItemSelected += OnFileTreeItemSelected;

        ResetSelection = new ResetSelectionCommand(Duplicates.DeletionSelection);

        DeleteMarkedFiles = new DeleteMarkedFilesCommand(Duplicates, Config.ResultsConfig);
        DeleteMarkedFiles.Started += (_, _) => { Ui.Entry.Enabled = false; DuplicateGroupsProxyView.BeginBulkRemoval(); };
        DeleteMarkedFiles.Finished += OnDeleteMarkedFilesFinished;

        AddPath = new AddPathCommand(SearchPaths, () => SelectedFileTreeItem);
        OpenFileInExplorer = new OpenFileInExplorerCommand();
        ChangePage = new ChangePageCommand(DuplicateGroupsProxyView);
        ToggleDuplicateSortingOrder = new RelayCommand(_ => DuplicateGroupComparer.IsSortOrderDescending = !DuplicateGroupComparer.IsSortOrderDescending);
        ClearResultsFilter = new RelayCommand(_ => ResultsFilterKeywords = "");
        ClearResults = new RelayCommand(_ => Duplicates.Clear());
        ClearErrors = new RelayCommand(_ => Duplicates.Errors.Clear());
        ClearSearchPaths = new RelayCommand(_ => SearchPaths.Clear());
        ToggleSearchPathActive = new RelayCommand(param => { if (param is SearchPath searchPath) searchPath.IsActive = !searchPath.IsActive; });
        RefreshFileTree = new RelayCommand(_ => RefreshExpandedFileTreeItems());
        ResetSettings = new RelayCommand(_ => ResetConfigurationToDefaults());

        ClearExtensions = new RelayCommand(_ => SearchConfig.Extensions.Clear());
        AddOrRemoveExtensions = new AddOrRemoveExtensionsCommand(SearchConfig.Extensions, Config.ExtensionsConfig);
        Navigate = new RelayCommand(parameter => Process.Start(new ProcessStartInfo(parameter as string ?? "") { UseShellExecute = true }));

        PropertyChanged += OnPropertyChanged;

        SetSortingOrderToolTip();
        UpdateFileTree();
    }

    public void Dispose() => 
        Config.Dispose();

    private void OnFindDuplicatesStarting(object? sender, EventArgs e)
    {
        DuplicateGroupsProxyView.SortingEnabled = false;
        DuplicateGroupsProxyView.SelectNewItem = false;
    }

    private void OnFindDuplicatesFinished(object? o, EventArgs eventArgs)
    {
        DuplicateGroupsProxyView.Sort();
        if (Duplicates.DuplicateGroups.Count != 0)
            SelectedTabIndex = 1;
        DuplicateGroupsProxyView.SortingEnabled = true;
        DuplicateGroupsProxyView.SelectNewItem = true;
    }

    private void OnFindDuplicatesCanExecuteChanged(object? sender, EventArgs eventArgs)
    {
        var findDuplicatesEnabled = FindDuplicates.Enabled;
        Ui.Entry.Enabled = findDuplicatesEnabled;
        Ui.EntryReadOnly.Enabled = !findDuplicatesEnabled;
        UpdateSearchEnabled();
        OnUpdateAddPathEnabled();
    }

    private void OnFindDuplicatesCanCancelChanged(object? sender, EventArgs eventArgs) =>
        Ui.CancelSearch.Enabled = FindDuplicates.CanCancel;

    private void OnSearchPathsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs eventArgs)
    {
        if (eventArgs.OldItems != null)
            foreach (SearchPath searchPath in eventArgs.OldItems)
                searchPath.PropertyChanged -= OnSearchPathPropertyChanged;
        if (eventArgs.NewItems != null)
            foreach (SearchPath searchPath in eventArgs.NewItems)
                searchPath.PropertyChanged += OnSearchPathPropertyChanged;

        UpdateSearchEnabled();
        OnUpdateAddPathEnabled();
    }

    private void OnSearchPathPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName == nameof(SearchPath.IsActive))
            UpdateSearchEnabled();
    }

    // The search has nothing to scan unless at least one path is present and enabled (toggled on).
    private void UpdateSearchEnabled() =>
        Ui.Search.Enabled = FindDuplicates.Enabled && SearchPaths.Any(searchPath => searchPath.IsActive);

    private void OnDuplicateGroupsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => 
        ClearResults.Enabled = Duplicates.DuplicateGroups.Count != 0;

    private void OnDuplicatesPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        switch (eventArgs.PropertyName)
        {
            case nameof(Duplicates.ProgressPercentage):
                TaskbarProgress = Duplicates.ProgressPercentage;
                ProgressPercentage = Duplicates.ProgressPercentage;
                break;
            case nameof(Duplicates.ProgressText):
                ProgressText = Duplicates.ProgressText;
                break;
            case nameof(Duplicates.ToBeDeletedCount):
                var hasFilesToBeDeleted = Duplicates.ToBeDeletedCount != 0;
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    ResetSelection.Enabled = hasFilesToBeDeleted;
                    DeleteMarkedFiles.Enabled = hasFilesToBeDeleted;
                });
                break;
        }
    }

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        switch (eventArgs.PropertyName)
        {
            case nameof(SelectedFileTreeItem):
                OnUpdateAddPathEnabled();
                break;
            case nameof(IsFilterFilePath):
            case nameof(IsFilterFileName):
            case nameof(IsFilterFileExtension):
            case nameof(IsFilterCaseSensitive):
            case nameof(IsIncludeFilter):
            case nameof(IsExcludeFilter):
            case nameof(ResultsFilterKeywords):
                OnResultsFilterChanged();
                break;
        }
    }

    private void OnSortTypeChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        switch (eventArgs.PropertyName)
        {
            case nameof(DuplicateGroupComparer.SelectedSortOrder):
                DuplicateGroupsProxyView.Sort();
                break;
            case nameof(DuplicateGroupComparer.IsSortOrderDescending):
                SetSortingOrderToolTip();
                DuplicateGroupsProxyView.Sort();
                break;
        }
    }

    private void SetSortingOrderToolTip()
    {
        DuplicatesSortingOrderToolTip = DuplicateGroupComparer.IsSortOrderDescending
            ? Resources.Ui_Duplicates_Sorting_Order_Descending
            : Resources.Ui_Duplicates_Sorting_Order_Ascending;
    }

    private void OnUpdateAddPathEnabled()
    {
        if (SelectedFileTreeItem == null)
            return;
        var currentAddPathState = AddPath.Enabled;
        var itemPath = SelectedFileTreeItem.ItemPath;
        var newAddPathState = AddPath.CanAddPath(itemPath) && FindDuplicates.Enabled;
        if (currentAddPathState == newAddPathState)
            return;

        AddPath.Enabled = newAddPathState;
    }

    private void UpdateFileTree()
    {
        if (FileTree.Count != 0)
            FileTree.Clear();
        var fileTreeContent = FileTreeItem.GetFileSystemItemsForDrives();
        foreach (var fileSystemItem in fileTreeContent)
            FileTree.Add(fileSystemItem);
    }

    public void RefreshExpandedFileTreeItems()
    {
        foreach (var fileTreeItem in FileTree)
            fileTreeItem.Refresh();
    }

    // A deletion run mutates files/groups on disk and in the model. Refresh the search-page tree, then force the
    // folder-comparison control to rebuild so deleted files disappear from its columns. The control rebuilds when
    // CurrentComparisonGroup changes (its setter guards ReferenceEquals), so re-drive the property: if the current
    // group was emptied/removed by the run, fall back to null (the placeholder, OQ-6); otherwise toggle through null
    // to force a change notification and re-enumerate the surviving group's folders from disk. DeleteMarkedFiles.Finished
    // resumes on the UI thread (its async-void Execute captures the UI context, like the RefreshExpandedFileTreeItems
    // call already relied on), so this runs on the UI thread.
    private void OnDeleteMarkedFilesFinished(object? sender, EventArgs eventArgs)
    {
        Ui.Entry.Enabled = true;
        // The run removed groups across pages with the page reload deferred; now that it has fully finished, snap the
        // results list to the page holding the first group from the old current page that survived (or the last page).
        DuplicateGroupsProxyView.EndBulkRemoval();
        RefreshExpandedFileTreeItems();

        var group = Duplicates.CurrentComparisonGroup;
        if (group != null && !Duplicates.DuplicateGroups.Contains(group))
            group = null;                            // the group was emptied/removed by the deletion → placeholder
        Duplicates.CurrentComparisonGroup = null;    // force a change notification
        Duplicates.CurrentComparisonGroup = group;   // rebuild (re-enumerates folders from disk), or placeholder if null
    }

    private void OnFileTreeItemSelected(object? sender, EventArgs eventArgs) =>
        SelectedFileTreeItem = sender as FileTreeItem;

    private void OnDuplicateFileSelected(object? sender, EventArgs eventArgs)
    {
        if (sender is DuplicateFile { IsSelected: true } duplicateFile)
        {
            AutoSelectByPath.Path = duplicateFile.FileFullName;
            // Drive the folder-comparison control to the selected file's group. Only set on selection, never clear on
            // deselect: single-select fires the old row's deselect before the new row's select, so clearing here would
            // briefly flip CurrentComparisonGroup to null and make the control rebuild twice (flicker). Selecting
            // another file in the same group is a no-op (the setter guards ReferenceEquals), so no rebuild.
            Duplicates.CurrentComparisonGroup = Duplicates.GetGroupForPath(duplicateFile.FileFullName);
            // Persist the selected file's path so each folder column can light up its outer background when the file
            // lives in that folder. Set-on-select only (same flicker rationale as the group above).
            Duplicates.SelectedDuplicateFilePath = duplicateFile.FileFullName;
        }
        else
            AutoSelectByPath.Path = "";
    }

    private void OnDuplicateGroupSelected(object? sender, EventArgs eventArgs)
    {
        if (sender is DuplicateGroup { IsSelected: true } group)
            Duplicates.CurrentComparisonGroup = group;
    }

    private void OnAutoSelectByPathStarting(object? sender, EventArgs eventArgs) =>
        Ui.Entry.Enabled = false;

    private void OnAutoSelectByPathFinished(object? sender, AutoSelectStartingEventArgs eventEventArgs)
    {
        TaskbarProgress = ProgressPercentage = 0;
        ProgressText = string.Format(Resources.Ui_AutoSelectByPath_Finished, eventEventArgs.SelectedCount);
        Ui.Entry.Enabled = true;
    }

    private void OnAutoSelectByPathProgress(object? sender, AutoSelectProgressEventArgs eventArgs)
    {
        if (_barUpdateThrottle.IsUpdateDue())
        {
            var totalFilesCount = eventArgs.TotalFilesCount;
            TaskbarProgress = ProgressPercentage = totalFilesCount != 0 ? (double)eventArgs.CurrentFileIndex * 10000 / eventArgs.TotalFilesCount : 0;
        }
        if (_textUpdateThrottle.IsUpdateDue())
            ProgressText = string.Format(Resources.Ui_AutoSelectByPath_Progress, eventArgs.SelectedCount);
    }

    private void OnResultsFilterChanged()
    {
        if (DuplicateGroupsProxyView.SortingEnabled)
            DuplicateGroupsProxyView.Filter();
    }
        
    private void OnResultsPageChanged(object? sender, EventArgs _) => 
        ResultsTreeView.ResetView();

    private void OnResultsCollectionProxyChanged(object? o, NotifyCollectionChangedEventArgs eventArgs)
    {
        if (Duplicates.DuplicateGroups.Count != 0)
            Ui.ClearResults.Enabled = true;
        else if (Ui.ClearResults.Enabled)
            Ui.ClearResults.Enabled = false;
    }

    private void ResetConfigurationToDefaults()
    {
        var owner = System.Windows.Application.Current?.MainWindow;
        var confirmation = owner != null
            ? System.Windows.MessageBox.Show(owner, Resources.Ui_Settings_Reset_Confirm_Text, Resources.Ui_Settings_Reset_Confirm_Caption, System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning)
            : System.Windows.MessageBox.Show(Resources.Ui_Settings_Reset_Confirm_Text, Resources.Ui_Settings_Reset_Confirm_Caption, System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);
        if (confirmation != System.Windows.MessageBoxResult.Yes)
            return;

        Config.ResetToDefaults();
        SelectedFileComparer = FileComparers.FirstOrDefault(comparer => comparer.Guid == Config.SearchConfig.SelectedFileComparerGuid.Value) ?? FileComparers.First();
    }

    private static IFileComparer? GetInitialSelectedFileComparer(Configuration.Configuration config, IReadOnlyCollection<IFileComparer> fileComparers)
    {            
        // ReSharper disable LocalizableElement
        Debug.Assert(config.SearchConfig != null, "Initializing the selected file comparer while the Config.SearchConfig object is null");
        // ReSharper restore LocalizableElement

        if (fileComparers.Count == 0)
            return null;

        var searchConfig = config.SearchConfig;
        return fileComparers.FirstOrDefault(comparer => comparer.Guid == searchConfig.SelectedFileComparerGuid.Value);
    }
}