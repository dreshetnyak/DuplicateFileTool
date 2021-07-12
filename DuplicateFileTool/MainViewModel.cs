using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
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

    internal class MainViewModel : NotifyPropertyChanged
    {
        private IFileComparer _selectedFileComparer;
        private FileTreeItem _selectedFileTreeItem;
        private long _toBeDeletedSize;
        private string _output;
        private int _progressPercentage;
        private string _progressText;

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

        public ObservableCollection<SearchPath> SearchPaths { get; } = new();
        public ObservableCollection<FileTreeItem> FileTree { get; }
        public FileTreeItem SelectedFileTreeItem
        {
            get => _selectedFileTreeItem;
            set
            {
                _selectedFileTreeItem = value;
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
        public int ProgressPercentage
        {
            get => _progressPercentage;
            set
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

        #region Commands
        public FindDuplicatesCommand FindDuplicates { get; }
        public RelayCommand CancelDuplicatesSearch { get; }
        public AddPathCommand AddPath { get; }
        public ToggleDeletionMarkCommand ToggleDeletionMark { get; }
        public AutoSelectByPathCommand AutoSelectByPath { get; }
        public ResetSelectionCommand ResetSelection { get; }
        public DeleteMarkedFilesCommand DeleteMarkedFiles { get; }

        #endregion

        public SearchConfiguration SearchConfig { get; }

        public MainViewModel()
        {
            PropertyChanged += OnPropertyChanged;

            Config = new ApplicationConfig();
            SearchConfig = Config.SearchConfig;
            PathComparisonTypes = Enum.GetValues(typeof(InclusionType)).OfType<object>().Cast<InclusionType>().ToArray();
            
            InclusionPredicate = new InclusionPredicate(Config.SearchConfig);
            FileComparers = Config.FileComparers;
            Duplicates = new DuplicatesEngine();
            InitializeSelectedFileComparer(); 

            //TODO Need to display this 
            //Duplicates.FileSystemErrors
            
            FindDuplicates = new FindDuplicatesCommand(Duplicates, SearchPaths, () => InclusionPredicate, () => SelectedFileComparer);
            CancelDuplicatesSearch = new RelayCommand(_ => FindDuplicates.Cancel());

            ToggleDeletionMark = new ToggleDeletionMarkCommand(sizeDelta => ToBeDeletedSize += sizeDelta);
            AutoSelectByPath = new AutoSelectByPathCommand(Duplicates.DuplicateGroups, sizeDelta => ToBeDeletedSize += sizeDelta);
            ResetSelection = new ResetSelectionCommand(Duplicates.DuplicateGroups, sizeDelta => ToBeDeletedSize += sizeDelta);
            DeleteMarkedFiles = new DeleteMarkedFilesCommand(Duplicates.DuplicateGroups, sizeDelta => ToBeDeletedSize += sizeDelta, message => { Output += message; });
            AddPath = new AddPathCommand(SearchPaths, () => SelectedFileTreeItem);

            FileTree = new ObservableCollection<FileTreeItem>();
            UpdateFileTree();
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
            Debug.Assert(Config != null, "Initializing the selected file comparer while the Config object is null");
            Debug.Assert(Config.SearchConfig != null, "Initializing the selected file comparer while the Config.SearchConfig object is null");
            Debug.Assert(FileComparers != null, "Initializing the selected file comparer while the Config.FileComparers list is null");

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
