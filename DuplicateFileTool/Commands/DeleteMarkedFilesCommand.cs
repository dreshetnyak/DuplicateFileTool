using System;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using DuplicateFileTool.Configuration;

namespace DuplicateFileTool.Commands
{
    [Localizable(true)]
    internal class DeleteMarkedFilesCommand : CommandBase
    {
        private DuplicatesEngine Duplicates { get; }
        private ResultsConfiguration ResultsConfig { get; }

        private static readonly object CtsLock = new();
        private CancellationTokenSource Cts { get; set; }
        
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
            }
        }

        public override void Cancel()
        {
            try { lock (CtsLock) Cts?.Cancel(); }
            catch { /* ignore */ }
        }
    }
}
