using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using DuplicateFileTool.Annotations;

namespace DuplicateFileTool
{
    internal class EnabledElement : NotifyPropertyChanged
    {
        private bool _enabled;
        private readonly ReaderWriterLock _accessLock = new();
        private int DisableCount { get; set; }
        private bool Invert { get; }
        private EnabledElement Parent { get; }

        public bool Enabled { get => Get(); set => Set(value); }

        public EnabledElement(bool enabled) : this(null, enabled)
        { }

        public EnabledElement(EnabledElement parent = null, bool enabled = true, bool invert = false)
        {
            _enabled = enabled;
            Parent = parent;
            Invert = invert;
            if (!enabled)
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

        public UiSwitch()
        {
            Entry = new EnabledElement();
            EntryReadOnly = new EnabledElement(Entry, true, true);
            Search = new EnabledElement(Entry, false);
            CancelSearch = new EnabledElement(false);
            ErrorTabImage = new EnabledElement(false);
        }
    }
}
