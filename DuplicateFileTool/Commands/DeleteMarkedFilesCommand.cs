using System;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using DuplicateFileTool.Configuration;

namespace DuplicateFileTool.Commands
{
    [Localizable(true)]
    internal sealed class DeleteMarkedFilesCommand : CommandBase
    {
        private DuplicatesEngine Duplicates { get; }
        private ResultsConfiguration ResultsConfig { get; }

        private static readonly object CtsLock = new();
        private CancellationTokenSource Cts { get; set; }

        public event EventHandler Started;
        public event EventHandler Finished;

        public DeleteMarkedFilesCommand(DuplicatesEngine duplicates, ResultsConfiguration resultsConfig)
        {
            Enabled = false;
            Duplicates = duplicates;
            ResultsConfig = resultsConfig;
        }

        public override async void Execute(object parameter)
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

                await Duplicates.RemoveDuplicates(Duplicates.DuplicateGroups, ResultsConfig.RemoveEmptyDirectories.Value, ResultsConfig.DeleteToRecycleBin.Value, ctx);
            }
            finally
            {
                Enabled = Duplicates.DuplicateGroups.Any(group => group.DuplicateFiles.Any(file => file.IsMarkedForDeletion));

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

        private void OnStarted()
        {
            Started?.Invoke(this, EventArgs.Empty);
        }

        private void OnFinished()
        {
            Finished?.Invoke(this, EventArgs.Empty);
        }
    }
}
