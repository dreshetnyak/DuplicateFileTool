using System;
using System.Windows.Input;

namespace FileBadger.Commands
{
    internal interface ICancellable
    {
        bool CanCancel { get; }

        void Cancel();
    }

    internal abstract class CommandBase : NotifyPropertyChanged, ICommand, ICancellable
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

        public virtual void Cancel() { }

        #endregion

        protected CommandBase(bool enabled = true)
        {
            _enabled = Enabled;
        }

        public abstract void Execute(object parameter);
    }
}
