using System.ComponentModel;
using DuplicateFileTool.Properties;

namespace DuplicateFileTool.Configuration
{

    [Localizable(true)]
    internal class ResultsConfiguration : NotifyPropertyChanged, IChangeable
    {
        public ConfigurationProperty<bool> SortDescending { get; } = new(
            Resources.Config_Results_Sort_Descending_Name,
            Resources.Config_Results_Sort_Descending_Description,
            false);
        
        public ConfigurationProperty<SortOrder> SortOrder { get; } = new(
            Resources.Config_Results_Sort_Order_Name,
            Resources.Config_Results_Sort_Order_Description,
            DuplicateFileTool.SortOrder.Number);
        
        public ConfigurationProperty<int> ItemsPerPage { get; } = new(
            Resources.Config_Results_Items_Per_Page_Name,
            Resources.Config_Results_Items_Per_Page_Description,
            25,
            new LongValidationRule(10, 100));

        public ConfigurationProperty<bool> RemoveEmptyDirectories { get; } = new(
            Resources.Config_Results_Remove_Empty_Directories_Name,
            Resources.Config_Results_Remove_Empty_Directories_Description,
            true);

        public ConfigurationProperty<bool> DeleteToRecycleBin { get; } = new(
            Resources.Config_Results_Delete_To_Recycle_Bin_Name,
            Resources.Config_Results_Delete_To_Recycle_Bin_Description,
            false);

        public bool HasChanged
        {
            get => ChangeTracker.HasChanged;
            set => ChangeTracker.HasChanged = value;
        }

        private PropertiesChangeTracker<ResultsConfiguration> ChangeTracker { get; }

        public ResultsConfiguration()
        {
            ChangeTracker = new PropertiesChangeTracker<ResultsConfiguration>(this);
            ChangeTracker.PropertyChanged += (_, _) => OnPropertyChanged(nameof(HasChanged));
        }
    }
}
