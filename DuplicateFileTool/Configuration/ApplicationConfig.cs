using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
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

        public SearchConfiguration SearchConfig { get; } = new();
        public IReadOnlyCollection<IFileComparer> FileComparers { get; }

        public ApplicationConfig()
        {
            Log = new Logger(Logger.Target.Debug);

            try { SearchConfig.LoadFromAppConfig(); }
            catch (Exception ex) { Log.Write("Error: Loading search configuration from app config failed with the exception: " + ex); throw; }
            SearchConfig.PropertyChanged += OnSearchConfigChanged;

            FileComparers = GetFileComparers().ToArray();

            HasUnsavedChanges = false;
        }

        private void OnSearchConfigChanged(object sender, PropertyChangedEventArgs args)
        {
            if (args.PropertyName == nameof(SearchConfiguration.HasChanged))
                HasUnsavedChanges = SearchConfig.HasChanged;
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
