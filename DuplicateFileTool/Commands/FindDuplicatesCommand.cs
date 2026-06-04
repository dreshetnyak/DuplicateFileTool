namespace DuplicateFileTool.Commands;

internal sealed class FindDuplicatesCommand(
    DuplicatesEngine duplicatesEngine,
    IReadOnlyCollection<SearchPath> searchPaths,
    Func<IInclusionPredicate<FileData>> getGetInclusionPredicate,
    Func<IFileComparer> getSelectedComparer)
    : CommandBase
{
    private DuplicatesEngine DuplicatesEngine { get; } = duplicatesEngine;
    private IReadOnlyCollection<SearchPath> SearchPaths { get; } = searchPaths;
    private Func<IInclusionPredicate<FileData>> GetInclusionPredicate { get; } = getGetInclusionPredicate;
    private Func<IFileComparer> GetSelectedComparer { get; } = getSelectedComparer;
    private CancellationTokenSource? Cts { get; set; }

    public event EventHandler? FindDuplicatesStarting;
    public event EventHandler? FindDuplicatesFinished;

    public override async void Execute(object? parameter)
    {
        try
        {
            CanCancel = true;
            Enabled = false;
            OnFindDuplicatesStarting();

            var selectedComparer = GetSelectedComparer();
            using (Cts = new CancellationTokenSource())
                await DuplicatesEngine.FindDuplicates(SearchPaths, GetInclusionPredicate(), selectedComparer.CandidatePredicate, selectedComparer.ComparableFileFactory, Cts.Token);
        }
        catch (OperationCanceledException) { /* ignore */ }
        finally
        {
            CanCancel = false;
            Cts?.Dispose();
            Cts = null;
            Enabled = true;
            OnFindDuplicatesFinished();
        }
    }

    public override void Cancel()
    {
        if (CanCancel)
            Cts?.Cancel();
    }

    private void OnFindDuplicatesStarting() => 
        FindDuplicatesStarting?.Invoke(this, EventArgs.Empty);

    private void OnFindDuplicatesFinished() => 
        FindDuplicatesFinished?.Invoke(this, EventArgs.Empty);
}