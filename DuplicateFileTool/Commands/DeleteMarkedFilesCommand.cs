using System.ComponentModel;
using DuplicateFileTool.Configuration;
using DuplicateFileTool.Windows;
using Application = System.Windows.Application;

namespace DuplicateFileTool.Commands;

[Localizable(true)]
internal sealed class DeleteMarkedFilesCommand : CommandBase
{
    private DuplicatesEngine Duplicates { get; }
    private ResultsConfiguration ResultsConfig { get; }

    private static readonly object CtsLock = new();
    private CancellationTokenSource? Cts { get; set; }

    public event EventHandler? Started;
    public event EventHandler? Finished;

    public DeleteMarkedFilesCommand(DuplicatesEngine duplicates, ResultsConfiguration resultsConfig)
    {
        Enabled = false;
        Duplicates = duplicates;
        ResultsConfig = resultsConfig;
    }

    public override async void Execute(object? parameter)
    {
        try
        {
            OnStarted();
            Enabled = false;
            CancellationToken ctx;
            lock (CtsLock)
            {
                Cts = new CancellationTokenSource();
                ctx = Cts.Token;
            }

            await Duplicates.RemoveDuplicates(Duplicates.DuplicateGroups, ResultsConfig.RemoveEmptyDirectories.Value, ResultsConfig.DeleteToRecycleBin.Value, PromptRecycleFailure, ctx);
        }
        finally
        {
            // Enable iff anything is still marked. Derive from the unified set's count (covers non-duplicate and
            // folder selections too), not a duplicate-group walk which would miss a non-duplicate-only selection.
            Enabled = Duplicates.ToBeDeletedCount != 0;

            try
            {
                lock (CtsLock)
                {
                    Cts?.Dispose();
                    Cts = null;
                }
            }
            catch { /* ignore */ }
            OnFinished();
        }
    }

    public override void Cancel()
    {
        try { lock (CtsLock) Cts?.Cancel(); }
        catch { /* ignore */ }
    }

    //Called on the deletion worker thread; the worker stays blocked until the user makes a choice
    private static RecycleFailureResponse PromptRecycleFailure(string filePath, string reason) =>
        Application.Current.Dispatcher.Invoke(() =>
        {
            var viewModel = new RecycleFailurePromptViewModel(filePath, reason);
            new RecycleFailurePrompt(viewModel).ShowDialog();
            return new RecycleFailureResponse(viewModel.Decision, viewModel.ApplyToAll);
        });

    private void OnStarted() => 
        Started?.Invoke(this, EventArgs.Empty);

    private void OnFinished() => 
        Finished?.Invoke(this, EventArgs.Empty);
}