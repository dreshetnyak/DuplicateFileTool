using System.Linq;

namespace DuplicateFileTool
{
    internal class UiSwitch : NotifyPropertyChanged
    {
        private bool _isInterfaceEntryEnabled = true;
        private bool _isSearchPathsListReadOnly;
        private bool _isUiEntryEnabled = true;
        private bool _isCancelSearchEnabled;
        private bool _isSearchEnabled;
        private bool _isErrorTabImageEnabled;
        private bool _isClearPathsListEnabled;

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
        public bool IsUiEntryEnabled
        {
            get => _isUiEntryEnabled;
            set
            {
                _isUiEntryEnabled = value; 
                OnPropertyChanged();
            }
        }
        public bool IsCancelSearchEnabled
        {
            get => _isCancelSearchEnabled;
            set
            {
                _isCancelSearchEnabled = value; 
                OnPropertyChanged();
            }
        }
        public bool IsSearchEnabled
        {
            get => _isSearchEnabled;
            set
            {
                _isSearchEnabled = value;
                OnPropertyChanged();
            }
        }
        public bool IsClearPathsListEnabled
        {
            get => _isClearPathsListEnabled;
            set
            {
                _isClearPathsListEnabled = value; 
                OnPropertyChanged();
            }
        }
        public bool IsErrorTabImageEnabled
        {
            get => _isErrorTabImageEnabled;
            set
            {
                if (_isErrorTabImageEnabled == value)
                    return;
                _isErrorTabImageEnabled = value;
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
