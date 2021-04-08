using System;
using System.Threading;
using System.Windows.Input;

namespace FileBadger.Commands
{
    internal interface ICancellable
    {
        bool CanCancel { get; }

        void Cancel(object parameter);
    }

    internal abstract class CommandBase : NotifyPropertyChanged, ICommand, ICancellable, IDisposable
    {
        #region Can Execute Implementation

        private bool _enabled;
        public bool Enabled
        {
            get => _enabled;
            set
            {
                if (_enabled == value)
                    return;
                _enabled = value;
                OnCanExecuteChanged();
            }
        }

        public bool CanExecute(object parameter) { return Enabled; }
        public event EventHandler CanExecuteChanged;

        protected virtual void OnCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }

        #endregion

        #region Can Cancel Implementation

        private AutoResetEvent _cancelEvent;
        private AutoResetEvent CancelEvent => _cancelEvent ??= new AutoResetEvent(false);

        private bool _canCancel;
        public bool CanCancel
        {
            get => _canCancel;
            set
            {
                if (_canCancel == value)
                    return;
                _canCancel = value;
                OnPropertyChanged();
            }
        }

        public void Cancel(object parameter)
        {
            if (CanCancel)
                CancelEvent.Set();
        }

        #endregion

        protected CommandBase(bool enabled = true)
        {
            _enabled = Enabled;
        }
        public void Dispose()
        {
            _cancelEvent?.Dispose();
        }

        public abstract void Execute(object parameter);
    }
}
