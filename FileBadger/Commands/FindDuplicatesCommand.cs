using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Input;
using FileBadger.Annotations;

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
        public IReadOnlyCollection<SearchPath> SearchPaths { get; }
        public Func<IInclusionPredicate> GetInclusionPredicate { get; }
        public Func<FileComparerAttribute> GetSelectedComparer { get; }
        
        public FindDuplicatesCommand(
            [NotNull] DuplicatesEngine duplicatesEngine,
            [NotNull] IReadOnlyCollection<SearchPath> searchPaths,
            [NotNull] Func<IInclusionPredicate> getGetInclusionPredicate,
            [NotNull] Func<FileComparerAttribute> getSelectedComparer)
        {
            DuplicatesEngine = duplicatesEngine;
            SearchPaths = searchPaths;
            GetInclusionPredicate = getGetInclusionPredicate;
            GetSelectedComparer = getSelectedComparer;
        }

        public override void Execute(object parameter)
        {
            try
            {
                Enabled = false;

                var selectedComparer = GetSelectedComparer();

                //TODO

                var duplicates = DuplicatesEngine.FindDuplicates(SearchPaths, null, null, selectedComparer, CancellationToken.None);
                
            }
            finally
            { Enabled = true; }
        }
    }
}
