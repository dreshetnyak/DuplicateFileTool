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

namespace DuplicateFileTool
{
    public enum InclusionType { Include, Exclude }
    public enum ByteSizeUnits { Bytes, Kilobytes, Megabytes, Gigabytes }
    
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
        private long _toBeDeletedSize;
        private string _output;
        private bool _removeEmptyDirectories;
        private bool _deleteToRecycleBin;
        private double _taskbarProgress;

        #endregion

        public ApplicationConfig Config { get; }

        public InclusionType[] PathComparisonTypes { get; }
        public IInclusionPredicate InclusionPredicate { get; }
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
        public PagedObservableCollectionView<DuplicateGroup> DuplicateFilesPageView { get; } //Stores the collection of duplicates groups that corresponds to the selected page
        
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

        public long ToBeDeletedSize
        {
            get => _toBeDeletedSize;
            set
            {
                _toBeDeletedSize = value;
                OnPropertyChanged();
            }
        }
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
            
            InclusionPredicate = new InclusionPredicate(Config.SearchConfig);
            FileComparers = Config.FileComparers;
            Duplicates = new DuplicatesEngine();
            Duplicates.PropertyChanged += OnDuplicatesPropertyChanged;
            
            DuplicateFilesPageView = new PagedObservableCollectionView<DuplicateGroup>(Duplicates.DuplicateGroups, 100);
            DuplicateFilesPageView.Collection.CollectionChanged += OnDuplicatesCollectionChanged;

            InitializeSelectedFileComparer();

            SearchPaths.CollectionChanged += OnSearchPathsCollectionChanged;

            FindDuplicates = new FindDuplicatesCommand(Duplicates, SearchPaths, () => InclusionPredicate, () => SelectedFileComparer);
            FindDuplicates.FindDuplicatesFinished += OnFindDuplicatesFinished;
            FindDuplicates.CanExecuteChanged += OnFindDuplicatesCanExecuteChanged;

            CancelDuplicatesSearch = new RelayCommand(_ => FindDuplicates.Cancel());
            ToggleDeletionMark = new ToggleDeletionMarkCommand(sizeDelta => ToBeDeletedSize += sizeDelta);
            AutoSelectByPath = new AutoSelectByPathCommand(Duplicates.DuplicateGroups, sizeDelta => ToBeDeletedSize += sizeDelta);
            ResetSelection = new ResetSelectionCommand(Duplicates.DuplicateGroups, sizeDelta => ToBeDeletedSize += sizeDelta);
            DeleteMarkedFiles = new DeleteMarkedFilesCommand(Duplicates, () => RemoveEmptyDirectories, () => DeleteToRecycleBin);
            AddPath = new AddPathCommand(SearchPaths, () => SelectedFileTreeItem);
            OpenFileInExplorer = new OpenFileInExplorerCommand();

            var treeViewExtension = new TreeViewExtension(mainWindow);
            TreeViewReset += treeViewExtension.ViewModelOnTreeViewReset;

            UpdateFileTree();
        }

        private void OnFindDuplicatesFinished(object o, EventArgs eventArgs)
        {
            SelectedTabIndex = 1;
        }

        private void OnFindDuplicatesCanExecuteChanged(object sender, EventArgs eventArgs)
        {
            Ui.IsCancelSearchEnabled = FindDuplicates.CanCancel;
        }

        private void OnSearchPathsCollectionChanged(object sender, NotifyCollectionChangedEventArgs eventArgs)
        {
            Ui.IsSearchEnabled = SearchPaths.Count != 0 && FindDuplicates.Enabled;
        }

        private void OnDuplicatesCollectionChanged(object _, NotifyCollectionChangedEventArgs args)
        {
            if (args.Action == NotifyCollectionChangedAction.Reset)
                TreeViewReset?.Invoke(this, new TreeViewResetEventArgs(nameof(DuplicateFilesPageView)));
        }

        private void OnDuplicatesPropertyChanged(object sender, PropertyChangedEventArgs eventArgs)
        {
            if (eventArgs.PropertyName == nameof(Duplicates.ProgressPercentage))
                TaskbarProgress = Duplicates.ProgressPercentage;
        }

        private void OnPropertyChanged(object sender, PropertyChangedEventArgs eventArgs)
        {
            if (eventArgs.PropertyName == nameof(SelectedFileTreeItem))
                OnSelectedFileTreeItemChanged();
        }
        
        private void OnSelectedFileTreeItemChanged()
        {
            if (AddPath == null)
                return;

            var currentAddPathState = AddPath.Enabled;
            var itemPath = SelectedFileTreeItem.ItemPath;
            var newAddPathState = AddPath.CanAddPath(itemPath);
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
