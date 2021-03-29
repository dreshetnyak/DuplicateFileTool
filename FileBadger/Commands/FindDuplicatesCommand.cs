using System;
using System.Collections.Generic;
using System.Windows.Input;
using FileBadger.Annotations;
using FileBadger.Configuration;

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
        public DuplicatesEngine DuplicatesEngine { get; }
        public SearchConfiguration SearchConfig { get; }
        public Func<IEnumerable<SearchPath>> GetSearchPaths { get; }
        public Func<FileComparerAttribute> GetSelectedComparer { get; }
        
        public FindDuplicatesCommand([NotNull] DuplicatesEngine duplicatesEngine, [NotNull] SearchConfiguration searchConfig, [NotNull] Func<IEnumerable<SearchPath>> getSearchPaths, [NotNull] Func<FileComparerAttribute> getSelectedComparer)
        {
            DuplicatesEngine = duplicatesEngine;
            SearchConfig = searchConfig;
            GetSearchPaths = getSearchPaths;
            GetSelectedComparer = getSelectedComparer;
        }

        public override void Execute(object parameter)
        {
            try
            {
                Enabled = false;


                Duplicates.




            }
            finally
            { Enabled = true; }
        }
    }
}
