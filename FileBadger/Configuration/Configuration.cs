using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace FileBadger.Configuration
{
    internal class SearchConfiguration : TrackedChangeNotifier<ConfigurationPropertyAttribute>
    {
        #region Backing Fields
        private int _maximumFilesOpenedAtOnce;
        private bool _excludeSystemFiles;
        private bool _excludeHiddenFiles;
        private bool _excludeOsFiles;

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
    }

    internal class Configuration : NotifyPropertyChanged
    {
        public string ApplicationName { get; } = ConfigurationManager.GetAppName();

        private bool _hasUnsavedChanges;
        public bool HasUnsavedChanges
        {
            get => _hasUnsavedChanges;
            private set
            {
                _hasUnsavedChanges = value;
                OnPropertyChanged();
            }
        }

        public SearchConfiguration SearchConfig { get; } = new SearchConfiguration();

        public Configuration()
        {
            //This property is specific to ComparableFileHash
            //HashChunkSize = AppConfig.Get("ComparableFileHash.HashChunkSize", 65535);

            SearchConfig.LoadFromAppConfig();
            SearchConfig.PropertyChanged += OnSearchConfigChanged;



            HasUnsavedChanges = false;
        }

        private void OnSearchConfigChanged(object sender, PropertyChangedEventArgs args)
        {
            if (args.PropertyName == nameof(SearchConfiguration.HasChanged))
                HasUnsavedChanges = SearchConfig.HasChanged;
        }

        



        private static List<string> GetComparers(string nameSpace)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var assemblyTypes = assembly.GetTypes();
            var comparersNamespaceTypes = assemblyTypes.Where(type => type.Namespace == nameof(Comparers)); //TODO test

            foreach (var type in comparersNamespaceTypes)
            {
                var attributes type.GetCustomAttributes(true);
            }


            //if (comparersNamespace == default)
            //    return null;

            //Get all with the attribute FileComparer
            
        }


    }
}
