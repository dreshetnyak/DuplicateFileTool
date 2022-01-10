using System.ComponentModel;
using System.Threading;

namespace DuplicateFileTool
{
    internal class EnabledElement : NotifyPropertyChanged
    {
        private bool _enabled;
        private readonly ReaderWriterLock _accessLock = new();
        private string Name { get; }
        private int DisableCount { get; set; }
        private bool Invert { get; }
        private EnabledElement Parent { get; }

        public bool Enabled { get => Get(); set => Set(value); }

        public EnabledElement(string elementName, bool enabled) : this(elementName, null, enabled)
        { }

        public EnabledElement(string elementName, EnabledElement parent = null, bool enabled = true, bool invert = false)
        {
            Name = elementName;
            Parent = parent;
            Invert = invert;
            _enabled = invert ? !enabled : enabled;
            if (!_enabled)
                DisableCount = 1;
            if (parent != null)
                parent.PropertyChanged += OnParentChanged;
        }

        private void OnParentChanged(object sender, PropertyChangedEventArgs eventArgs)
        {
            if (eventArgs.PropertyName == nameof(Enabled))
                OnPropertyChanged(nameof(Enabled));
        }

        private bool Get()
        {
            try
            {
                _accessLock.AcquireReaderLock(Timeout.Infinite);
                var enabled = _enabled && (Parent == null || Parent.Enabled);
                return Invert ? !enabled : enabled;
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
                if (Invert ? !value : value)
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
            OnPropertyChanged(nameof(Enabled));
        }

        private void SetDisabled()
        {
            DisableCount++;
            if (!_enabled)
                return;
            _enabled = false;
            OnPropertyChanged(nameof(Enabled));
        }
    }

    internal class UiSwitch : NotifyPropertyChanged
    {
        public EnabledElement Entry { get; }
        public EnabledElement EntryReadOnly { get; }
        public EnabledElement Search { get; }
        public EnabledElement CancelSearch { get; }
        public EnabledElement ErrorTabImage { get; }
        public EnabledElement ClearResults { get; }

        public UiSwitch()
        {
            Entry = new EnabledElement(nameof(Entry));
            EntryReadOnly = new EnabledElement(nameof(EntryReadOnly), Entry, false, true);
            Search = new EnabledElement(nameof(Search), Entry, false);
            CancelSearch = new EnabledElement(nameof(CancelSearch), false);
            ErrorTabImage = new EnabledElement(nameof(ErrorTabImage), false);
            ClearResults = new EnabledElement(nameof(ClearResults), Entry, false);
        }
    }
}
