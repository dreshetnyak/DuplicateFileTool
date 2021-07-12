﻿using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using DuplicateFileTool.Properties;

namespace DuplicateFileTool.Configuration
{
    [Localizable(true)]
    internal class SearchConfiguration : NotifyPropertyChanged
    {
        public ConfigurationProperty<int> MaximumFilesOpenedAtOnce { get; } = new(
            Resources.Config_MaximumFilesOpenedAtOnce_Name,
            Resources.Config_MaximumFilesOpenedAtOnce_Description,
            256,
            new IntValidationRule(1, 512));

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
            Guid.Parse(@"56E94DDC-1021-49D5-8DB1-FF1C92710978"),
            isHidden: true);

        #region File size inclusion parameters

        public ConfigurationProperty<ByteSizeUnits> ByteSizeUnit { get; } = new(
            Resources.Config_ByteSizeUnit_Name,
            Resources.Config_ByteSizeUnit_Description, 
            ByteSizeUnits.Bytes);

        public ConfigurationProperty<int> MinFileSize { get; } = new(
            Resources.Config_MinFileSize_Name,
            Resources.Config_MinFileSize_Description,
            0,
            new IntValidationRule(0, int.MaxValue));

        public ConfigurationProperty<int> MaxFileSize { get; } = new(
            Resources.Config_MaxFileSize_Name,
            Resources.Config_MaxFileSize_Description,
            0,
            new IntValidationRule(0, int.MaxValue));

        #endregion

        #region  File extension inclusion parameters

        public ConfigurationProperty<InclusionType> ExtensionInclusionType { get; } = new(
            Resources.Config_ExtensionInclusionType_Name,
            Resources.Config_ExtensionInclusionType_Description,
            InclusionType.Include);

        public ObservableCollection<ObservableString> Extensions { get; set; } = new();

        #endregion

        public bool HasChanged => ChangeTracker.HasChanged;

        private PropertiesChangeTracker<SearchConfiguration> ChangeTracker { get; }

        public SearchConfiguration()
        {
            ChangeTracker = new PropertiesChangeTracker<SearchConfiguration>(this);
            ChangeTracker.PropertyChanged += (_, _) => OnPropertyChanged(nameof(HasChanged));
        }
    }
}