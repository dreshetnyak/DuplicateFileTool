using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;

namespace DuplicateFileTool.Configuration
{
    internal class ApplicationConfig : NotifyPropertyChanged
    {
        private bool _hasUnsavedChanges;

        public string ApplicationName { get; } = ConfigurationManager.GetAppName();
        public Logger Log { get; }
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
        public List<FileComparerAttribute> FileComparers { get; }

        public ApplicationConfig()
        {
            Log = new Logger(Logger.Target.Debug);

            try { SearchConfig.LoadFromAppConfig(); }
            catch (Exception ex) { Log.Write("Error: Loading search configuration from app config failed with the exception: " + ex); throw; }
            SearchConfig.PropertyChanged += OnSearchConfigChanged;

            FileComparers = GetFileComparerAttributes().ToList();

            HasUnsavedChanges = false;
        }

        private void OnSearchConfigChanged(object sender, PropertyChangedEventArgs args)
        {
            if (args.PropertyName == nameof(SearchConfiguration.HasChanged))
                HasUnsavedChanges = SearchConfig.HasChanged;
        }

        private static IEnumerable<FileComparerAttribute> GetFileComparerAttributes()
        {
            foreach (var type in Assembly.GetExecutingAssembly().GetTypes().Where(type => type.Namespace?.EndsWith(nameof(Comparers)) ?? false))
            {
                //Check if implements ComparableFile
                if (!type.DerivedFrom(typeof(IComparableFile)))
                    continue;

                //Check if marked with FileComparer attribute
                var comparerAttribute = type.GetAttribute<FileComparerAttribute>();
                if (comparerAttribute == null)
                    continue;

                //Check if the config implements IComparerConfig
                if (comparerAttribute.ConfigurationType.GetInterfaces().All(inter => inter != typeof(IComparerConfig)))
                    continue;

                yield return comparerAttribute;
            }
        }
    }
}
