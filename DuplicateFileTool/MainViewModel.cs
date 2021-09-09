using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using DuplicateFileTool.Commands;
using DuplicateFileTool.Configuration;
using DuplicateFileTool.Properties;

namespace DuplicateFileTool
{
    public enum InclusionType { Include, Exclude }
    public enum ByteSizeUnits { Bytes, Kilobytes, Megabytes, Gigabytes }
    public enum SortOrder { None, Size, Name, Path, Number }

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
            }
        }

        public string Path
        {
            get => _path;
            set
            {
                _path = value;
                OnPropertyChanged();
            }
        }

        public SearchPath()
        { }

        public SearchPath(string path, InclusionType pathInclusionType)
        {
            _path = path;
            _pathInclusionType = pathInclusionType;
        }
    }

    [Localizable(true)]
    internal class MainViewModel : NotifyPropertyChanged
    {
        #region Backing Fields
        private IFileComparer _selectedFileComparer;
        private FileTreeItem _selectedFileTreeItem;
        private int _selectedTabIndex;
        private string _output;
        private bool _removeEmptyDirectories;
        private bool _deleteToRecycleBin;
        private double _taskbarProgress;
        private string _selectedDuplicatePath;
        private bool _isSortOrderDescending;
        private string _duplicatesSortingOrderToolTip;

        #endregion

        public ApplicationConfig Config { get; }

        public InclusionType[] PathComparisonTypes { get; }
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
        //public PagedObservableCollectionView<DuplicateGroup> DuplicateGroupsPageView { get; } //Stores the collection of duplicates groups that corresponds to the selected page
        public ObservableCollectionProxy<DuplicateGroup> DuplicateGroupsProxyView { get; }
        public bool IsSortOrderDescending
        {
            get => _isSortOrderDescending;
            set
            {
                if (_isSortOrderDescending == value)
                    return;
                _isSortOrderDescending = value;
                OnPropertyChanged();
            }
        }

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
        public string SelectedDuplicatePath
        {
            get => _selectedDuplicatePath;
            set
            {
                _selectedDuplicatePath = value; 
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
        public SortOrder[] SortOrderTypes { get; }
        public DuplicateGroupComparer DuplicateGroupComparer { get; }

        public string Output
        {
            get => _output;
            set
            {
                _output = value;
                OnPropertyChanged();
            }
        }
        public bool RemoveEmptyDirectories
        {
            get => _removeEmptyDirectories;
            set
            {
                _removeEmptyDirectories = value; 
                OnPropertyChanged();
            }
        }
        public bool DeleteToRecycleBin
        {
            get => _deleteToRecycleBin;
            set
            {
                _deleteToRecycleBin = value; 
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
        public ClearResultsCommand ClearResults { get; }

        #endregion

        public SearchConfiguration SearchConfig { get; }
        public UiSwitch Ui { get; } = new();

        public event TreeViewResetHandler TreeViewReset;

        public MainViewModel(Window mainWindow)
        {
            PropertyChanged += OnPropertyChanged;

            Config = new ApplicationConfig();
            SearchConfig = Config.SearchConfig;
            PathComparisonTypes = Enum.GetValues(typeof(InclusionType)).OfType<object>().Cast<InclusionType>().ToArray();
            SortOrderTypes = Enum.GetValues(typeof(SortOrder)).OfType<object>().Cast<SortOrder>().ToArray();

            FileSearchInclusionPredicate = new FileSearchInclusionPredicate(Config.SearchConfig);
            FileComparers = Config.FileComparers;
            Duplicates = new DuplicatesEngine();
            Duplicates.PropertyChanged += OnDuplicatesPropertyChanged;
            Duplicates.Errors.CollectionChanged += (_, _) => Ui.IsErrorTabImageEnabled = Duplicates.Errors.Count != 0;
            Duplicates.DuplicateGroups.CollectionChanged += OnDuplicateGroupsCollectionChanged;

            //TODO IsSortOrderDescending should be propagated to the configuration
            var resultsGroupInclusionPredicate = new ResultsGroupInclusionPredicate(); //TODO
            DuplicateGroupComparer = new DuplicateGroupComparer(SortOrder.Number); //TODO initial sort order should be propagated to config
            DuplicateGroupComparer.PropertyChanged += OnSortTypeChanged;
            DuplicateGroupsProxyView = new ObservableCollectionProxy<DuplicateGroup>(Duplicates.DuplicateGroups, resultsGroupInclusionPredicate, DuplicateGroupComparer, 25); //TODO items per page should ne in the config

            InitializeSelectedFileComparer();

            SearchPaths.CollectionChanged += OnSearchPathsCollectionChanged;

            FindDuplicates = new FindDuplicatesCommand(Duplicates, SearchPaths, () => FileSearchInclusionPredicate, () => SelectedFileComparer);
            FindDuplicates.FindDuplicatesStarting += OnFindDuplicatesStarting;
            FindDuplicates.FindDuplicatesFinished += OnFindDuplicatesFinished;
            FindDuplicates.CanExecuteChanged += OnFindDuplicatesCanExecuteChanged;

            CancelDuplicatesSearch = new RelayCommand(_ => FindDuplicates.Cancel());
            ToggleDeletionMark = new ToggleDeletionMarkCommand(sizeDelta => Duplicates.ToBeDeletedSize += sizeDelta, countDelta => Duplicates.ToBeDeletedCount += countDelta);
            AutoSelectByPath = new AutoSelectByPathCommand(Duplicates.DuplicateGroups, () => SelectedDuplicatePath, sizeDelta => Duplicates.ToBeDeletedSize += sizeDelta);
            ResetSelection = new ResetSelectionCommand(Duplicates.DuplicateGroups, sizeDelta => Duplicates.ToBeDeletedSize += sizeDelta, countDelta => Duplicates.ToBeDeletedCount += countDelta);
            DeleteMarkedFiles = new DeleteMarkedFilesCommand(Duplicates, () => RemoveEmptyDirectories, () => DeleteToRecycleBin);
            AddPath = new AddPathCommand(SearchPaths, () => SelectedFileTreeItem);
            OpenFileInExplorer = new OpenFileInExplorerCommand();
            ChangePage = new ChangePageCommand(DuplicateGroupsProxyView, () => { }); //TODO was OnPropertyChanged(nameof(DuplicateGroupsPageView.Collection))
            ToggleDuplicateSortingOrder = new RelayCommand(_ => IsSortOrderDescending = !IsSortOrderDescending);
            ClearResults = new ClearResultsCommand(() => Duplicates.Clear());

            var treeViewExtension = new TreeViewExtension(mainWindow);
            TreeViewReset += treeViewExtension.ViewModelOnTreeViewReset;
            
            DuplicateFile.ItemSelected += (sender, _) => { SelectedDuplicatePath = ((DuplicateFile)sender).FileFullName; };
            
            UpdateFileTree();
        }

        private void OnFindDuplicatesStarting(object sender, EventArgs e)
        {
            DuplicateGroupsProxyView.SortingEnabled = false;
            DuplicateGroupsProxyView.SelectNewItem = false;
        }

        private void OnFindDuplicatesFinished(object o, EventArgs eventArgs)
        {
            if (Duplicates.DuplicateGroups.Count != 0)
                SelectedTabIndex = 1;
            DuplicateGroupsProxyView.SortingEnabled = true;
            DuplicateGroupsProxyView.SelectNewItem = true;
        }

        private void OnFindDuplicatesCanExecuteChanged(object sender, EventArgs eventArgs)
        {
            Ui.IsCancelSearchEnabled = FindDuplicates.CanCancel;
            Ui.IsUiEntryEnabled = FindDuplicates.Enabled;
            Ui.IsSearchPathsListReadOnly = !FindDuplicates.Enabled;
            OnSelectedFileTreeItemChanged();
        }

        private void OnSearchPathsCollectionChanged(object sender, NotifyCollectionChangedEventArgs eventArgs)
        {
            Ui.IsSearchEnabled = SearchPaths.Count != 0 && FindDuplicates.Enabled;
        }

        private void OnDuplicateGroupsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            var isEnabled = Duplicates.DuplicateGroups.Count != 0;
            AutoSelectByPath.Enabled = isEnabled;
            ClearResults.Enabled = isEnabled;
        }

        private void OnDuplicatesPropertyChanged(object sender, PropertyChangedEventArgs eventArgs)
        {
            switch (eventArgs.PropertyName)
            {
                case nameof(Duplicates.ProgressPercentage):
                    TaskbarProgress = Duplicates.ProgressPercentage;
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
                    OnSelectedFileTreeItemChanged();
                    break;
                case nameof(IsSortOrderDescending):
                    DuplicatesSortingOrderToolTip = IsSortOrderDescending
                        ? Resources.Ui_Duplicates_Sorting_Order_Descending
                        : Resources.Ui_Duplicates_Sorting_Order_Ascending;
                    DuplicateGroupsProxyView.Sort();
                    break;
            }
        }
        private void OnSortTypeChanged(object sender, PropertyChangedEventArgs eventArgs)
        {
            if (eventArgs.PropertyName == nameof(DuplicateGroupComparer.SelectedSortOrder))
                DuplicateGroupsProxyView.Sort();
        }

        private void OnSelectedFileTreeItemChanged()
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
