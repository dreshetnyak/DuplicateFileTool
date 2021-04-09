using System;

namespace FileBadger.Commands
{
    internal class ToggleDeletionMarkCommand : CommandBase
    {
        private Action<long> UpdateSizeToBeDeleted { get; }

        public ToggleDeletionMarkCommand(Action<long> updateSizeToBeDeleted)
        {
            UpdateSizeToBeDeleted = updateSizeToBeDeleted;
        }

        public override void Execute(object parameter)
        {
            try
            {
                Enabled = false;
                var duplicateFile = parameter as DuplicateFile;
                if (duplicateFile == null)
                    return;

                duplicateFile.IsMarkedForDeletion = !duplicateFile.IsMarkedForDeletion;
                var sizeDelta = duplicateFile.IsMarkedForDeletion ? duplicateFile.FileData.Size : -duplicateFile.FileData.Size;
                UpdateSizeToBeDeleted?.Invoke(sizeDelta);
            }
            finally
            {
                Enabled = true;
            }
        }
    }
}
