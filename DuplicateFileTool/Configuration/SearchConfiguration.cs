using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using DuplicateFileTool.Properties;

namespace DuplicateFileTool.Configuration
{
    [Localizable(true)]
    internal class SearchConfiguration : NotifyPropertyChanged, IChangeable
    {
        private bool _hasExtensions;

        public ConfigurationProperty<int> MaximumFilesOpenedAtOnce { get; } = new(
            Resources.Config_MaximumFilesOpenedAtOnce_Name,
            Resources.Config_MaximumFilesOpenedAtOnce_Description,
            256,
            new LongValidationRule(1, 512));

        public ConfigurationProperty<bool> ExcludeSystemFiles { get; } = new(
            Resources.Config_ExcludeSystemFiles_Name,
            Resources.Config_ExcludeSystemFiles_Description,
            true);
        
        public ConfigurationProperty<bool> ExcludeHiddenFiles { get; } = new(
            Resources.Config_ExcludeHiddenFiles_Name,
            Resources.Config_ExcludeHiddenFiles_Description,
            true);

        public ConfigurationProperty<bool> ExcludeOsFiles { get; } = new(
            Resources.Config_ExcludeOsFiles_Name,
            Resources.Config_ExcludeOsFiles_Description,
            true);

        public ConfigurationProperty<Guid> SelectedFileComparerGuid { get; } = new(
            Resources.Configur_SelectedFileComparerGuid_Name,
            Resources.Configur_SelectedFileComparerGuid_Description,
            // ReSharper disable once LocalizableElement
            Guid.Parse("56E94DDC-1021-49D5-8DB1-FF1C92710978"),
            isHidden: true);

        #region File size inclusion parameters

        public ConfigurationProperty<ByteSizeUnits> ByteSizeUnit { get; } = new(
            Resources.Config_ByteSizeUnit_Name,
            Resources.Config_ByteSizeUnit_Description, 
            ByteSizeUnits.Bytes);

        public ConfigurationProperty<long> MinFileSize { get; } = new(
            Resources.Config_MinFileSize_Name,
            Resources.Config_MinFileSize_Description,
            0,
            new LongValidationRule(0, long.MaxValue));

        public ConfigurationProperty<long> MaxFileSize { get; } = new(
            Resources.Config_MaxFileSize_Name,
            Resources.Config_MaxFileSize_Description,
            0,
            new LongValidationRule(0, long.MaxValue));

        #endregion

        #region  File extension inclusion parameters

        public ConfigurationProperty<InclusionType> ExtensionInclusionType { get; } = new(
            Resources.Config_ExtensionInclusionType_Name,
            Resources.Config_ExtensionInclusionType_Description,
            InclusionType.Include);

        public ObservableCollection<FileExtension> Extensions { get; set; } = new();
        public bool HasExtensions
        {
            get => _hasExtensions;
            set
            {
                if (_hasExtensions == value)
                    return;
                _hasExtensions = value;
                OnPropertyChanged();
            }
        }

        #endregion

        public bool HasChanged
        {
            get => ChangeTracker.HasChanged;
            set => ChangeTracker.HasChanged = value;
        }

        private PropertiesChangeTracker<SearchConfiguration> ChangeTracker { get; }

        public SearchConfiguration()
        {
            ChangeTracker = new PropertiesChangeTracker<SearchConfiguration>(this);
            ChangeTracker.PropertyChanged += (_, _) => OnPropertyChanged(nameof(HasChanged));
            MaximumFilesOpenedAtOnce.PropertyChanged += OnMaximumFilesOpenedAtOnceChanged;
            Extensions.CollectionChanged += (_, _) => HasExtensions = Extensions.Count != 0;
        }

        private void OnMaximumFilesOpenedAtOnceChanged(object sender, PropertyChangedEventArgs eventArgs)
        {
            FileReader.MaxFileHandlesCount = MaximumFilesOpenedAtOnce.Value;
        }
    }
}