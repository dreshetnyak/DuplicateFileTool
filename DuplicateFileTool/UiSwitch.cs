using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace DuplicateFileTool
{
    //TODO Redoing the whole concept of how it is done

    internal class EnabledElement : NotifyPropertyChanged
    {
        private bool _enabled;
        private readonly ReaderWriterLock _accessLock = new();
        private int DisableCount { get; set; }
        private EnabledElement Parent { get; }
        private List<EnabledElement> Children { get; set; }

        public bool Enabled { get => Get(); set => Set(value); }

        public EnabledElement(bool enabled, EnabledElement parent = null)
        {
            _enabled = enabled;
            Parent = parent;
            parent?.AddChild(this);
        }

        private void AddChild(EnabledElement child)
        {
            Children ??= new List<EnabledElement>();
            Children.Add(child);
        }

        //Own state
        //Desired state
        //Parent state

        private bool Get()
        {
            try
            {
                _accessLock.AcquireReaderLock(Timeout.Infinite);
                return _enabled;
            }
            finally
            {
                _accessLock.ReleaseReaderLock();
            }
        }

        private void Set(bool value)
        {
            try
            {
                _accessLock.AcquireWriterLock(Timeout.Infinite);
                if (value)
                    SetEnabled();
                else
                    SetDisabled();
            }
            finally
            {
                _accessLock.ReleaseWriterLock();
            }
        }

        private void SetEnabled()
        {
            if (DisableCount > 0)
                DisableCount--;
            if (DisableCount != 0 || _enabled)
                return;
            _enabled = true;
            OnPropertyChanged();
            if (Children == null)
                return;
            foreach (var child in Children)
                child.SetEnabled();
        }

        private void SetDisabled()
        {
            DisableCount++;
            if (!_enabled)
                return;
            _enabled = false;
            OnPropertyChanged();
            if (Children == null)
                return;
            foreach (var child in Children)
                child.SetDisabled();
        }
    }

    internal class UiSwitch : NotifyPropertyChanged
    {
        #region Backing Fields
        private bool _isUiEntryEnabled = true;
        private bool _isSearchPathsListReadOnly;
        private bool _isCancelSearchEnabled;
        private bool _isSearchEnabled;
        private bool _isErrorTabImageEnabled;
        private bool _isClearPathsListEnabled;
        #endregion

        private readonly object _disableCountLock = new();
        private int DisableCount { get; set; }
        
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

        public void DisableUiEntry(params string[] exceptProperties)
        {
            SetProperties(exceptProperties, false);
            lock (_disableCountLock)
                DisableCount++;
        }

        public void EnableUiEntry(params string[] exceptProperties)
        {
            lock (_disableCountLock)
            {
                if (DisableCount > 0)
                    DisableCount --;
                if (DisableCount > 0)
                    return;
            }

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
