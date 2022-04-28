using System;
using System.ComponentModel;
using DuplicateFileTool.Properties;

namespace DuplicateFileTool.Configuration
{
    [Localizable(true)]
    internal class ProgramConfiguration : NotifyPropertyChanged, IChangeable, IDisposable
    {
        public ConfigurationProperty<string> SelectedCulture { get; } = new(
            Resources.Config_SelectedCulture_Name,
            Resources.Config_SelectedCulture_Description,
            "en"); //Neutral culture

        public bool HasChanged
        {
            get => ChangeTracker.HasChanged;
            set => ChangeTracker.HasChanged = value;
        }

        private PropertiesChangeTracker<ProgramConfiguration> ChangeTracker { get; }

        public ProgramConfiguration()
        {
            ChangeTracker = new PropertiesChangeTracker<ProgramConfiguration>(this);
            ChangeTracker.PropertyChanged += (_, _) => OnPropertyChanged(nameof(HasChanged));
        }

        public void Dispose()
        {
            ChangeTracker?.Dispose();
        }
    }
}
