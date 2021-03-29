using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using FileBadger.Commands;

namespace FileBadger
{
    public enum InclusionType { Include, Exclude }

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
    }

    internal class MainViewModel : NotifyPropertyChanged
    {
        private FileComparerAttribute _selectedFileComparer;

        public Configuration.ApplicationConfig Config { get; }
        public ObservableCollection<SearchPath> SearchPaths { get; } = new ObservableCollection<SearchPath>();
        public IReadOnlyCollection<FileComparerAttribute> FileComparers { get; }
        public FileComparerAttribute SelectedFileComparer
        {
            get => _selectedFileComparer;
            set
            {
                _selectedFileComparer = value;
                UpdateSelectedFileComparerGuid(value);
                OnPropertyChanged();
            }
        }
        public FindDuplicatesCommand FindDuplicates { get; }
        private DuplicatesEngine Duplicates { get; }

        public MainViewModel()
        {
            Config = new Configuration.ApplicationConfig();
            Duplicates = new DuplicatesEngine();
            FileComparers = Config.FileComparers;
            InitializeSelectedFileComparer();

            FindDuplicates = new FindDuplicatesCommand(Duplicates, Config.SearchConfig, () => SearchPaths, () => SelectedFileComparer);
        }

        private void InitializeSelectedFileComparer()
        {
            Debug.Assert(Config != null, "Initializing the selected file comparer while the Config object is null");
            Debug.Assert(Config.SearchConfig != null, "Initializing the selected file comparer while the Config.SearchConfig object is null");
            Debug.Assert(FileComparers != null, "Initializing the selected file comparer while the Config.FileComparers list is null");

            var searchConfig = Config?.SearchConfig; 
            if (searchConfig == null)
                return;

            FileComparerAttribute selectedComparer;
            SelectedFileComparer = !string.IsNullOrEmpty(searchConfig.SelectedFileComparerGuid)
                ? FileComparers.Count != 0 && (selectedComparer = FileComparers.FirstOrDefault(comparer => comparer.Guid == searchConfig.SelectedFileComparerGuid)) != null ? selectedComparer : null
                : FileComparers.FirstOrDefault();
        }
        
        private void UpdateSelectedFileComparerGuid(FileComparerAttribute value)
        {
            if (Config?.SearchConfig == null)
                return;
            if (value != null)
                Config.SearchConfig.SelectedFileComparerGuid = value.Guid;
            else if (Config.SearchConfig.SelectedFileComparerGuid != null)
                Config.SearchConfig.SelectedFileComparerGuid = null;
        }
    }
}
