using System;

namespace DuplicateFileTool.Commands
{
    internal class ToggleDeletionMarkCommand : CommandBase
    {
        private Action<long> UpdateToDeleteSize { get; }
        public Action<long> UpdateToDeleteCount { get; }

        public ToggleDeletionMarkCommand(Action<long> updateToDeleteSize, Action<long> updateToDeleteCount)
        {
            UpdateToDeleteSize = updateToDeleteSize;
            UpdateToDeleteCount = updateToDeleteCount;
        }

        public override void Execute(object parameter)
        {
            try
            {
                Enabled = false;
                if (parameter is not DuplicateFile duplicateFile)
                    return;

                var isMarkedForDeletion = duplicateFile.IsMarkedForDeletion = !duplicateFile.IsMarkedForDeletion;
                UpdateToDeleteSize?.Invoke(isMarkedForDeletion ? duplicateFile.FileData.Size : -duplicateFile.FileData.Size);
                UpdateToDeleteCount?.Invoke(isMarkedForDeletion ? 1 : -1);
            }
            finally
            {
                Enabled = true;
            }
        }
    }
}
