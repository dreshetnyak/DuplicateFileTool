using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace DuplicateFileTool.Configuration
{
    internal class ApplicationConfig : NotifyPropertyChanged, IChangeable, IDisposable
    {
        private bool _hasChanged;
        private bool _hasUnsavedChanges;

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

        public SearchConfiguration SearchConfig { get; } = new();
        public ResultsConfiguration ResultsConfig { get; } = new();
        public ExtensionsConfiguration ExtensionsConfig { get; } = new();
        public IReadOnlyCollection<IFileComparer> FileComparers { get; }
        public InclusionType[] PathComparisonTypes { get; }
        public SortOrder[] SortOrderTypes { get; }

        public ApplicationConfig()
        {
            Log = new Logger(Logger.Target.Debug);

            //TODO need to be update with the names that is taken from the resources
            PathComparisonTypes = Enum.GetValues(typeof(InclusionType)).OfType<object>().Cast<InclusionType>().ToArray();
            SortOrderTypes = Enum.GetValues(typeof(SortOrder)).OfType<object>().Cast<SortOrder>().ToArray();

            try { this.LoadFromAppConfig(); }
            catch (Exception ex) { Log.Write("Error: Loading application configuration from app config failed with the exception: " + ex); throw; }
            PropertyChanged += OnConfigurationChanged;

            try { SearchConfig.LoadFromAppConfig(); }
            catch (Exception ex) { Log.Write("Error: Loading search configuration from app config failed with the exception: " + ex); throw; }
            SearchConfig.PropertyChanged += OnConfigurationChanged;

            try { ResultsConfig.LoadFromAppConfig(); }
            catch (Exception ex) { Log.Write("Error: Loading results configuration from app config failed with the exception: " + ex); throw; }
            ResultsConfig.PropertyChanged += OnConfigurationChanged;

            try { ExtensionsConfig.LoadFromAppConfig(); }
            catch (Exception ex) { Log.Write("Error: Loading extensions configuration from app config failed with the exception: " + ex); throw; }
            ResultsConfig.PropertyChanged += OnConfigurationChanged;

            FileComparers = GetFileComparers().ToArray();
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
