using System;
using System.ComponentModel;
using System.Linq;
using System.Threading;

namespace DuplicateFileTool.Commands
{
    [Localizable(true)]
    internal class DeleteMarkedFilesCommand : CommandBase
    {
        private DuplicatesEngine Duplicates { get; }
        private Func<bool> RemoveEmptyDirs { get; }
        private Func<bool> DeleteToRecycleBin { get; }

        private static readonly object CtsLock = new();
        private CancellationTokenSource Cts { get; set; }
        
        public DeleteMarkedFilesCommand(DuplicatesEngine duplicates, Func<bool> removeEmptyDirs, Func<bool> deleteToRecycleBin)
        {
            Enabled = false;
            Duplicates = duplicates;
            RemoveEmptyDirs = removeEmptyDirs;
            DeleteToRecycleBin = deleteToRecycleBin;
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

                await Duplicates.RemoveDuplicates(Duplicates.DuplicateGroups, RemoveEmptyDirs(), DeleteToRecycleBin(), ctx);
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
