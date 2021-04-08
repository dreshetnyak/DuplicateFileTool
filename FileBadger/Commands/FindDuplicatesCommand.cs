using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FileBadger.Annotations;

namespace FileBadger.Commands
{
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

        public override async void Execute(object parameter)
        {
            try
            {
                Enabled = false;

                var selectedComparer = GetSelectedComparer();
                var comparableFileFactory = Activator.CreateInstance(selectedComparer.ComparableFileFactoryType) as IComparableFileFactory;
                var candidatePredicate = Activator.CreateInstance(selectedComparer.CandidatePredicateType) as ICandidatePredicate;

                //TODO: Cancellation


                List<List<MatchResult>> duplicates = await DuplicatesEngine.FindDuplicates(SearchPaths, GetInclusionPredicate(), candidatePredicate, comparableFileFactory, CancellationToken.None);
                
            }
            finally
            { Enabled = true; }
        }
    }
}
