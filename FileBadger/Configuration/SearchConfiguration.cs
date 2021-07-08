using System.Collections.ObjectModel;
using System.ComponentModel;

namespace DuplicateFileTool.Configuration
{
    [Localizable(true)]
    internal class SearchConfiguration : TrackedChangeNotifier<ConfigurationPropertyAttribute>
    {
        #region Backing Fields
        private int _maximumFilesOpenedAtOnce;
        private bool _excludeSystemFiles;
        private bool _excludeHiddenFiles;
        private bool _excludeOsFiles;
        private string _selectedFileComparerGuid;
        private ByteSizeUnits _byteSizeUnit;
        private int _minFileSize;
        private int _maxFileSize;
        private InclusionType _extensionInclusionType;

        #endregion

        [DefaultValue(256)]
        [IntRangeValidationRule(1, 512)]
        [ConfigurationProperty("Maximum files opened at once", "Specifies the maximum count of files that the program will keep open after reaching which the program will start closing the files opened previously. The low value will negatively impact performance, the high value will cause the program to consume to many resources.")]
        public int MaximumFilesOpenedAtOnce
        {
            get => _maximumFilesOpenedAtOnce;
            set
            {
                _maximumFilesOpenedAtOnce = value;
                OnPropertyChanged();
            }
        }

        [DefaultValue(true)]
        [ConfigurationProperty("Exclude system files and directories", "If enabled the program will skip the files and directories that has the system attribute set.")]
        public bool ExcludeSystemFiles
        {
            get => _excludeSystemFiles;
            set
            {
                _excludeSystemFiles = value;
                OnPropertyChanged();
            }
        }

        [DefaultValue(true)]
        [ConfigurationProperty("Exclude hidden files and directories", "If enabled the program will skip the hidden files and directories.")]
        public bool ExcludeHiddenFiles
        {
            get => _excludeHiddenFiles;
            set
            {
                _excludeHiddenFiles = value;
                OnPropertyChanged();
            }
        }

        [DefaultValue(true)]
        [ConfigurationProperty("Exclude OS files and directories", "If enabled the program will skip the OS files and directories, such as windows directory and others known directories that belong to the operating system.")]
        public bool ExcludeOsFiles
        {
            get => _excludeOsFiles;
            set
            {
                _excludeOsFiles = value;
                OnPropertyChanged();
            }
        }

        [DefaultValue("56E94DDC-1021-49D5-8DB1-FF1C92710978")]
        [ConfigurationProperty("Selected file comparer", "The file comparer that should be used to compare files during the duplicates search.")]
        public string SelectedFileComparerGuid
        {
            get => _selectedFileComparerGuid;
            set
            {
                _selectedFileComparerGuid = value;
                OnPropertyChanged();
            }
        }
        
        #region File size inclusion parameters
        public ByteSizeUnits ByteSizeUnit
        {
            get => _byteSizeUnit;
            set
            {
                _byteSizeUnit = value; 
                OnPropertyChanged();
            }
        }

        public int MinFileSize
        {
            get => _minFileSize;
            set
            {
                _minFileSize = value; 
                OnPropertyChanged();
            }
        }

        public int MaxFileSize
        {
            get => _maxFileSize;
            set
            {
                _maxFileSize = value; 
                OnPropertyChanged();
            }
        }

        #endregion

        #region  File extension inclusion parameters
        public InclusionType ExtensionInclusionType
        {
            get => _extensionInclusionType;
            set
            {
                _extensionInclusionType = value; 
                OnPropertyChanged();
            }
        }

        public ObservableCollection<ObservableString> Extensions { get; set; } = new();

        #endregion
    }
}