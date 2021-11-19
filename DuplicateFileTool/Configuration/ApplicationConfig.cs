﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using DuplicateFileTool.Properties;

namespace DuplicateFileTool.Configuration
{
    internal class SortOrderData
    {
        public SortOrder Order { get; }
        public string Name { get; }

        public SortOrderData(SortOrder order, string name)
        {
            Order = order;
            Name = name;
        }
    }

    internal class InclusionTypeData
    {
        public InclusionType Type { get; }
        public string Name { get; }

        public InclusionTypeData(InclusionType type, string name)
        {
            Type = type;
            Name = name;
        }
    }

    internal class ApplicationConfig : NotifyPropertyChanged, IChangeable, IDisposable
    {
        private bool _hasChanged;
        private bool _hasUnsavedChanges;
        private static readonly InclusionTypeData[] PathComparisonTypesData = 
        {
            new(InclusionType.Include, Resources.Ui_Search_Path_Include),
            new(InclusionType.Exclude, Resources.Ui_Search_Path_Exclude)
        };
        private static readonly SortOrderData[] SortOrderTypesData =
        {
            new(SortOrder.Number, Resources.Ui_Results_Sorting_By_Number),
            new(SortOrder.Size, Resources.Ui_Results_Sorting_By_Size),
            new(SortOrder.Path, Resources.Ui_Results_Sorting_By_Path),
            new(SortOrder.Name, Resources.Ui_Results_Sorting_By_Name)
        };

        public string ApplicationName { get; } = ConfigurationManager.GetAppName();
        public Logger Log { get; }
        public bool HasChanged
        {
            get => _hasChanged;
            // ReSharper disable once UnusedMember.Local
            private set
            {
                _hasChanged = value;
                OnPropertyChanged();
            }
        }
        public bool HasUnsavedChanges
        {
            get => _hasUnsavedChanges;
            set
            {
                if (_hasUnsavedChanges == value)
                    return;
                _hasUnsavedChanges = value;
                OnPropertyChanged();
            }
        }

        public SearchConfiguration SearchConfig { get; }
        public ObservableCollection<object> SearchConfigParams { get; }
        public ExtensionsConfiguration ExtensionsConfig { get; }
        public ObservableCollection<object> ExtensionsConfigParams { get; }
        public ResultsConfiguration ResultsConfig { get; }
        public ObservableCollection<object> ResultsConfigParams { get; }

        public IReadOnlyCollection<IFileComparer> FileComparers { get; }

        public InclusionTypeData[] PathComparisonTypes => PathComparisonTypesData;
        public SortOrderData[] SortOrderTypes => SortOrderTypesData;

        public ApplicationConfig()
        {
            Log = new Logger(Logger.Target.Debug);

            SearchConfig = new SearchConfiguration();
            SearchConfigParams = new ObservableCollection<object>(SearchConfig
                .GetGenericPropertiesObjects(typeof(IConfigurationProperty<>))
                .Where(IsParameterIncluded));
            
            ExtensionsConfig = new ExtensionsConfiguration();
            ExtensionsConfigParams = new ObservableCollection<object>(ExtensionsConfig
                .GetGenericPropertiesObjects(typeof(IConfigurationProperty<>))
                .Where(IsParameterIncluded));
          
            ResultsConfig = new ResultsConfiguration();
            ResultsConfigParams = new ObservableCollection<object>(ResultsConfig
                .GetGenericPropertiesObjects(typeof(IConfigurationProperty<>))
                .Where(IsParameterIncluded));

            try { this.LoadFromAppConfig(); }
            catch (Exception ex) { Log.Write("Error: Loading application configuration from app config failed with the exception: " + ex); throw; }
            HasChanged = false;
            PropertyChanged += OnConfigurationChanged;

            try { SearchConfig.LoadFromAppConfig(); }
            catch (Exception ex) { Log.Write("Error: Loading search configuration from app config failed with the exception: " + ex); throw; }
            SearchConfig.HasChanged = false;
            SearchConfig.PropertyChanged += OnConfigurationChanged;

            try { ResultsConfig.LoadFromAppConfig(); }
            catch (Exception ex) { Log.Write("Error: Loading results configuration from app config failed with the exception: " + ex); throw; }
            ResultsConfig.HasChanged = false;
            ResultsConfig.PropertyChanged += OnConfigurationChanged;

            try { ExtensionsConfig.LoadFromAppConfig(); }
            catch (Exception ex) { Log.Write("Error: Loading extensions configuration from app config failed with the exception: " + ex); throw; }
            ExtensionsConfig.HasChanged = false;
            ExtensionsConfig.PropertyChanged += OnConfigurationChanged;

            FileComparers = GetFileComparers().ToArray();
        }

        private static bool IsParameterIncluded(object parameter)
        {
            var itemType = parameter.GetType();
            if (!itemType.ImplementsInterfaceGeneric(typeof(IConfigurationProperty<>)))
                return false;

            var isHiddenProperty = itemType.GetProperty(nameof(IConfigurationProperty<int>.IsHidden));
            return isHiddenProperty != null && isHiddenProperty.GetValue(parameter) is false;
        }

        public void Dispose()
        {
            if (HasUnsavedChanges)
                SaveChanges();

            Log?.Dispose();
        }

        private void SaveChanges()
        {
            if (HasChanged)
                this.SaveToAppConfig();
            if (SearchConfig.HasChanged)
                SearchConfig.SaveToAppConfig();
            if (ResultsConfig.HasChanged)
                ResultsConfig.SaveToAppConfig();
            if (ExtensionsConfig.HasChanged)
                ExtensionsConfig.SaveToAppConfig();
        }

        private void OnConfigurationChanged(object sender, PropertyChangedEventArgs _)
        {
            if (!HasUnsavedChanges && sender is IChangeable { HasChanged: true })
                HasUnsavedChanges = true;
        }

        private static IEnumerable<IFileComparer> GetFileComparers()
        {
            foreach (var type in Assembly.GetExecutingAssembly().GetTypes().Where(type => type.Namespace?.EndsWith(nameof(Comparers)) ?? false))
            {
                if (type.GetTypeInfo().ImplementedInterfaces.All(implementedInterfaceType => implementedInterfaceType != typeof(IFileComparer)))
                    continue; //If does not implement IFileComparer
                if (type.GetConstructor(Type.EmptyTypes) == null) 
                    continue; // If there is no parameterless constructor

                IFileComparer fileComparer;
                try { fileComparer = (IFileComparer)Activator.CreateInstance(type); }
                catch (Exception ex) { Debug.Fail(ex.ToString()); continue; }

                yield return fileComparer;
            }
        }
    }
}
