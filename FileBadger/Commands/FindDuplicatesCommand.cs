using System;
using System.Windows.Input;

namespace FileBadger.Commands
{
    internal abstract class CommandBase : ICommand
    {
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

        protected CommandBase(bool enabled = true)
        {
            _enabled = Enabled;
        }

        public abstract void Execute(object parameter);

        protected virtual void OnCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    internal class FindDuplicatesCommand : CommandBase
    {
        private DuplicatesEngine Duplicates { get; } = new DuplicatesEngine();
        
        public FindDuplicatesCommand( )
        {
            
        }

        public override void Execute(object parameter)
        {
            throw new NotImplementedException();
        }
    }
}
