using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using FileBadger.Comparers;

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

    internal class FileInclusionConfig : NotifyPropertyChanged
    {
        #region File size inclusion parameters
        public enum SizeUnits { Bytes, Kilobytes, Megabytes, Gigabytes }
        public SizeUnits SizeUnit { get; set; }
        public int MinFileSize { get; set; }
        public int MaxFileSize { get; set; }
        #endregion

        #region  File extension inclusion parameters
        public InclusionType ExtensionInclusionType { get; set; }
        public ObservableCollection<string> Extensions { get; } = new ObservableCollection<string>();
        #endregion
    }

    internal class MainViewModel : NotifyPropertyChanged
    {
        public Configuration.ConfigurationManager Configuration { get; } = new Configuration.ConfigurationManager();
        private DuplicatesEngine Duplicates { get; } = new DuplicatesEngine();
        public ObservableCollection<SearchPath> SearchPaths { get; } = new ObservableCollection<SearchPath>();
        public FileInclusionConfig FileInclusion { get; } = new FileInclusionConfig();
        private List<ComparableFileFactory> FileComparers { get; }

        public MainViewModel()
        {
            FileComparers = new List<ComparableFileFactory>
            {
                new ComparableFileHash.Factory(new ComparableFileHashConfig())
                // Add more file comparers here
            };

            //Duplicates.FindDuplicates();

            //IReadOnlyCollection<string> searchPaths,

            //TODO
            //Func<FileData, bool> inclusionPredicate, 
            //Constraints: Min file size, Max file size, Include/Exclude extensions, Modified/Created dates From-To

            //Func<FileData, FileData, bool> duplicateCandidatePredicate,


            //ComparableFileFactory comparableFileFactory,
            //CancellationToken cancellationToken
        }
    }
}
