using System;
using System.Collections.Generic;
using System.Threading;
using DuplicateFileTool.Annotations;

namespace DuplicateFileTool.Commands
{
    internal class FindDuplicatesCommand : CommandBase
    {
        private DuplicatesEngine DuplicatesEngine { get; }
        private IReadOnlyCollection<SearchPath> SearchPaths { get; }
        private Func<IInclusionPredicate> GetInclusionPredicate { get; }
        private Func<IFileComparer> GetSelectedComparer { get; }
        private CancellationTokenSource Cts { get; set; }
        
        public FindDuplicatesCommand(
            [NotNull] DuplicatesEngine duplicatesEngine,
            [NotNull] IReadOnlyCollection<SearchPath> searchPaths,
            [NotNull] Func<IInclusionPredicate> getGetInclusionPredicate,
            [NotNull] Func<IFileComparer> getSelectedComparer)
        {
            DuplicatesEngine = duplicatesEngine;
            SearchPaths = searchPaths;
            GetInclusionPredicate = getGetInclusionPredicate;
            GetSelectedComparer = getSelectedComparer;
        }

        public override async void Execute(object parameter)
        {
            try
            {
                Enabled = false;

                var selectedComparer = GetSelectedComparer();
                using (Cts = new CancellationTokenSource()) 
                    await DuplicatesEngine.FindDuplicates(SearchPaths, GetInclusionPredicate(), selectedComparer.CandidatePredicate, selectedComparer.ComparableFileFactory, Cts.Token);
            }
            catch (OperationCanceledException) { /* ignore */ }
            finally
            {
                Cts = null;
                Enabled = true;
            }
        }

        public override void Cancel()
        {
            if (CanCancel)
                Cts?.Cancel();
        }
    }
}
