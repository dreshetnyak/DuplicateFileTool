using System.Linq;

namespace DuplicateFileTool
{
    internal class UiSwitch : NotifyPropertyChanged
    {
        private bool _isInterfaceEntryEnabled = true;
        private bool _isSearchPathsListReadOnly = true;
        private bool _isSearchExtensionsEnabled = true;
        private bool _isSearchFileSizeEntryEnabled;

        public bool IsAddPathEnabled
        {
            get => _isInterfaceEntryEnabled;
            set
            {
                if (_isInterfaceEntryEnabled == value)
                    return;
                _isInterfaceEntryEnabled = value;
                OnPropertyChanged();
            }
        }
        public bool IsSearchPathsListReadOnly
        {
            get => _isSearchPathsListReadOnly;
            set
            {
                if (_isSearchPathsListReadOnly == value)
                    return;
                _isSearchPathsListReadOnly = value; 
                OnPropertyChanged();
            }
        }
        public bool IsSearchExtensionsEnabled
        {
            get => _isSearchExtensionsEnabled;
            set
            {
                _isSearchExtensionsEnabled = value; 
                OnPropertyChanged();
            }
        }
        public bool IsSearchFileSizeEntryEnabled
        {
            get => _isSearchFileSizeEntryEnabled;
            set
            {
                _isSearchFileSizeEntryEnabled = value; 
                OnPropertyChanged();
            }
        }

        public void DisableEntry(params string[] exceptProperties)
        {
            SetProperties(exceptProperties, false);
        }

        public void EnableEntry(params string[] exceptProperties)
        {
            SetProperties(exceptProperties, true);
        }

        private void SetProperties(string[] exceptProperties, bool value)
        {
            foreach (var propertyInfo in this.GetPropertiesOfType<bool>())
            {
                var propertyName = propertyInfo.Name;
                if (exceptProperties.All(property => propertyName != property))
                    propertyInfo.SetValue(this, propertyName.EndsWith("Enabled") ? value : !value);
            }
        }
    }
}
