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

        public bool HasChanged => ChangeTracker.HasChanged;

        private PropertiesChangeTracker<ResultsConfiguration> ChangeTracker { get; }

        public ResultsConfiguration()
        {
            ChangeTracker = new PropertiesChangeTracker<ResultsConfiguration>(this);
            ChangeTracker.PropertyChanged += (_, _) => OnPropertyChanged(nameof(HasChanged));
        }
    }
}
