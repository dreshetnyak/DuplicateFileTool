using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using DuplicateFileTool.Commands;
using DuplicateFileTool.Configuration;
using DuplicateFileTool.Properties;

namespace DuplicateFileTool
{
    public enum InclusionType { Include, Exclude }
    public enum ByteSizeUnits { Bytes, Kilobytes, Megabytes, Gigabytes }
    public enum SortOrder { Number, Size, Path, Name }

    internal class SearchPath : NotifyPropertyChanged
    {
        private InclusionType _pathInclusionType;
        private string _path;

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
    internal class MainViewModel : NotifyPropertyChanged, IDisposable
    {
        public TreeView ResultsTreeView { get; }

        #region Backing Fields
        private IFileComparer _selectedFileComparer;
        private FileTreeItem _selectedFileTreeItem;
        private int _selectedTabIndex;
        private string _progressText;
        private double _progressPercentage;
        private double _taskbarProgress;
        private string _duplicatesSortingOrderToolTip;

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
                if (Config?.SearchConfig != null)
                    Config.SearchConfig.SelectedFileComparerGuid.Value = value.Guid;
                OnPropertyChanged();
            }
        }
        public DuplicatesEngine Duplicates { get; }
        public ObservableCollectionProxy<DuplicateGroup> DuplicateGroupsProxyView { get; }

        public ObservableCollection<SearchPath> SearchPaths { get; } = new();
        public ObservableCollection<FileTreeItem> FileTree { get; } = new();
        public FileTreeItem SelectedFileTreeItem
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
        [Localizable(false)]
        public string ProgressText
        {
            get => _progressText;
            set
            {
                _progressText = value?.Replace("\n", "").Replace("\r", "");
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
        public RelayCommand ClearResults { get; }
        public RelayCommand ClearErrors { get; }
        public RelayCommand ClearSearchPaths { get; }
        public AddOrRemoveExtensionsCommand AddOrRemoveExtensions { get; }
        public RelayCommand ClearExtensions { get; }
        public RelayCommand Navigate { get; }

        #endregion

        public SearchConfiguration SearchConfig { get; }
        public UiSwitch Ui { get; } = new();

        public MainViewModel(TreeView resultsTreeView) //TODO get rid of resultsTreeView
        {
            ResultsTreeView = resultsTreeView;
            PropertyChanged += OnPropertyChanged;

            Config = new Configuration.Configuration();

            SearchConfig = Config.SearchConfig;

            FileSearchInclusionPredicate = new FileSearchInclusionPredicate(Config.SearchConfig);
            FileComparers = Config.FileComparers;
            Duplicates = new DuplicatesEngine();
            Duplicates.PropertyChanged += OnDuplicatesPropertyChanged;
            Duplicates.Errors.CollectionChanged += (_, _) => Ui.ErrorTabImageEnabled = Duplicates.Errors.Count != 0;
            Duplicates.DuplicateGroups.CollectionChanged += OnDuplicateGroupsCollectionChanged;

            var resultsGroupInclusionPredicate = new ResultsGroupInclusionPredicate(); //TODO need to implement
            DuplicateGroupComparer = new DuplicateGroupComparer(Config.ResultsConfig);
            DuplicateGroupComparer.PropertyChanged += OnSortTypeChanged;
            DuplicateGroupsProxyView = new ObservableCollectionProxy<DuplicateGroup>(Duplicates.DuplicateGroups, resultsGroupInclusionPredicate, DuplicateGroupComparer, Config.ResultsConfig.ItemsPerPage.Value);
            DuplicateGroupsProxyView.PageChanged += OnResultsPageChanged;
            DuplicateGroupsProxyView.CollectionChanged += OnResultsCollectionProxyChanged;

            InitializeSelectedFileComparer();

            SearchPaths.CollectionChanged += OnSearchPathsCollectionChanged;

            FindDuplicates = new FindDuplicatesCommand(Duplicates, SearchPaths, () => FileSearchInclusionPredicate, () => SelectedFileComparer);
            FindDuplicates.FindDuplicatesStarting += OnFindDuplicatesStarting;
            FindDuplicates.FindDuplicatesFinished += OnFindDuplicatesFinished;
            FindDuplicates.CanExecuteChanged += OnFindDuplicatesCanExecuteChanged;
            FindDuplicates.CanCancelChanged += OnFindDuplicatesCanCancelChanged;

            CancelDuplicatesSearch = new RelayCommand(_ => FindDuplicates.Cancel());

            ToggleDeletionMark = new ToggleDeletionMarkCommand();
            ToggleDeletionMark.DeletionMarkToggle += OnUpdateToDelete;
            
            AutoSelectByPath = new AutoSelectByPathCommand(Duplicates.DuplicateGroups);
            AutoSelectByPath.FilesAutoMarkedForDeletion += OnUpdateToDelete;
            AutoSelectByPath.Starting += OnAutoSelectByPathStarting;
            AutoSelectByPath.Finished += OnAutoSelectByPathFinished;
            AutoSelectByPath.Progress += OnAutoSelectByPathProgress;
            DuplicateFile.ItemSelected += OnDuplicateFileSelected;

            ResetSelection = new ResetSelectionCommand(Duplicates.DuplicateGroups);
            ResetSelection.UpdateToDeleteSize += OnUpdateToDelete;

            DeleteMarkedFiles = new DeleteMarkedFilesCommand(Duplicates, Config.ResultsConfig);
            DeleteMarkedFiles.Started += (_, _) => Ui.Entry.Enabled = false;
            DeleteMarkedFiles.Finished += (_, _) => Ui.Entry.Enabled = true;

            AddPath = new AddPathCommand(SearchPaths, () => SelectedFileTreeItem);
            OpenFileInExplorer = new OpenFileInExplorerCommand();
            ChangePage = new ChangePageCommand(DuplicateGroupsProxyView);
            ToggleDuplicateSortingOrder = new RelayCommand(_ => DuplicateGroupComparer.IsSortOrderDescending = !DuplicateGroupComparer.IsSortOrderDescending);
            ClearResults = new RelayCommand(_ => Duplicates.Clear());
            ClearErrors = new RelayCommand(_ => Duplicates.Errors.Clear());
            ClearSearchPaths = new RelayCommand(_ => SearchPaths.Clear());

            ClearExtensions = new RelayCommand(_ => SearchConfig.Extensions.Clear());
            AddOrRemoveExtensions = new AddOrRemoveExtensionsCommand(SearchConfig.Extensions, Config.ExtensionsConfig);
            Navigate = new RelayCommand(parameter => Process.Start(new ProcessStartInfo((string)parameter)));

            SetSortingOrderToolTip();
            UpdateFileTree();
        }

        public void Dispose()
        {
            Config?.Dispose();
        }

        private void OnFindDuplicatesStarting(object sender, EventArgs e)
        {
            DuplicateGroupsProxyView.SortingEnabled = false;
            DuplicateGroupsProxyView.SelectNewItem = false;
        }

        private void OnFindDuplicatesFinished(object o, EventArgs eventArgs)
        {
            DuplicateGroupsProxyView.Sort();
            if (Duplicates.DuplicateGroups.Count != 0)
                SelectedTabIndex = 1;
            DuplicateGroupsProxyView.SortingEnabled = true;
            DuplicateGroupsProxyView.SelectNewItem = true;
        }

        private void OnFindDuplicatesCanExecuteChanged(object sender, EventArgs eventArgs)
        {
            var findDuplicatesEnabled = FindDuplicates.Enabled;
            Ui.Entry.Enabled = findDuplicatesEnabled;
            Ui.EntryReadOnly.Enabled = !findDuplicatesEnabled;
            Ui.Search.Enabled = SearchPaths.Count != 0 && findDuplicatesEnabled;
            OnUpdateAddPathEnabled();
        }

        private void OnFindDuplicatesCanCancelChanged(object sender, EventArgs eventArgs)
        {
            Ui.CancelSearch.Enabled = FindDuplicates.CanCancel;
        }

        private void OnSearchPathsCollectionChanged(object sender, NotifyCollectionChangedEventArgs eventArgs)
        {
            Ui.Search.Enabled = SearchPaths.Count != 0 && FindDuplicates.Enabled;
            OnUpdateAddPathEnabled();
        }

        private void OnDuplicateGroupsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            ClearResults.Enabled = Duplicates.DuplicateGroups.Count != 0;
        }

        private void OnDuplicatesPropertyChanged(object sender, PropertyChangedEventArgs eventArgs)
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
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        ResetSelection.Enabled = hasFilesToBeDeleted;
                        DeleteMarkedFiles.Enabled = hasFilesToBeDeleted;
                    });
                    break;
            }
        }

        private void OnPropertyChanged(object sender, PropertyChangedEventArgs eventArgs)
        {
            switch (eventArgs.PropertyName)
            {
                case nameof(SelectedFileTreeItem):
                    OnUpdateAddPathEnabled();
                    break;
            }
        }

        private void OnSortTypeChanged(object sender, PropertyChangedEventArgs eventArgs)
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
            if (AddPath == null)
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
            FileTreeItem.ItemSelected += (sender, _) => { SelectedFileTreeItem = (FileTreeItem)sender; };
        }

        private void OnDuplicateFileSelected(object sender, EventArgs eventArgs)
        {
            var duplicateFile = (DuplicateFile)sender;
            AutoSelectByPath.Path = duplicateFile.IsSelected
                ? duplicateFile.FileFullName
                : null;
        }

        private void OnUpdateToDelete(object sender, UpdateToDeleteEventArgs eventArgs)
        {
            Duplicates.ToBeDeletedCount += eventArgs.Count;
            Duplicates.ToBeDeletedSize += eventArgs.Size;
        }

        private void OnAutoSelectByPathStarting(object sender, EventArgs eventArgs)
        {
            Ui.Entry.Enabled = false;
        }

        private void OnAutoSelectByPathFinished(object sender, AutoSelectStartingArgs eventArgs)
        {
            TaskbarProgress = ProgressPercentage = 0;
            ProgressText = string.Format(Resources.Ui_AutoSelectByPath_Finished, eventArgs.SelectedCount);
            Ui.Entry.Enabled = true;
        }

        private void OnAutoSelectByPathProgress(object sender, AutoSelectProgressEventArgs eventArgs)
        {
            var totalFilesCount = eventArgs.TotalFilesCount;
            TaskbarProgress = ProgressPercentage = totalFilesCount != 0 ? (double)eventArgs.CurrentFileIndex * 10000 / eventArgs.TotalFilesCount : 0;
            ProgressText = string.Format(Resources.Ui_AutoSelectByPath_Progress, eventArgs.SelectedCount);
        }

        private void OnResultsPageChanged(object sender, EventArgs _)
        {
            ResultsTreeView.ResetView();
        }

        private void OnResultsCollectionProxyChanged(object o, NotifyCollectionChangedEventArgs eventArgs)
        {
            if (DuplicateGroupsProxyView.Count != 0)
                Ui.ClearResults.Enabled = true;
            else if (Ui.ClearResults.Enabled)
                Ui.ClearResults.Enabled = false;
        }

        private void InitializeSelectedFileComparer()
        {            
            // ReSharper disable LocalizableElement
            Debug.Assert(Config != null, "Initializing the selected file comparer while the Config object is null");
            Debug.Assert(Config.SearchConfig != null, "Initializing the selected file comparer while the Config.SearchConfig object is null");
            Debug.Assert(FileComparers != null, "Initializing the selected file comparer while the Config.FileComparers list is null");
            // ReSharper restore LocalizableElement

            var searchConfig = Config?.SearchConfig;
            if (searchConfig == null)
                return;

            IFileComparer selectedComparer;
            SelectedFileComparer = FileComparers.Count != 0 && (selectedComparer = FileComparers.FirstOrDefault(comparer => comparer.Guid == searchConfig.SelectedFileComparerGuid.Value)) != null
                ? selectedComparer
                : null;
        }
    }
}
