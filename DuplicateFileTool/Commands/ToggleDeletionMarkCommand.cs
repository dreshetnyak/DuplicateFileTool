using System;

namespace DuplicateFileTool.Commands
{
    internal class ToggleDeletionMarkCommand : CommandBase
    {
        private Action<long> UpdateToDeleteSize { get; }

        public ToggleDeletionMarkCommand(Action<long> updateToDeleteSize)
        {
            UpdateToDeleteSize = updateToDeleteSize;
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
                UpdateToDeleteSize?.Invoke(sizeDelta);
            }
            finally
            {
                Enabled = true;
            }
        }
    }
}
