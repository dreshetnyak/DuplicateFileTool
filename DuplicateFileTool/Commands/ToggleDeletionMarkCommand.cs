using System;

namespace DuplicateFileTool.Commands
{
    internal class UpdateToDeleteEventArgs : EventArgs
    {
        public long Count { get; }
        public long Size { get; set; }

        public UpdateToDeleteEventArgs(long count, long size)
        {
            Count = count;
            Size = size;
        }
    }

    internal delegate void UpdateToDeleteEventHandler(object sender, UpdateToDeleteEventArgs eventArgs);
    
    internal class ToggleDeletionMarkCommand : CommandBase
    {
        public event UpdateToDeleteEventHandler DeletionMarkToggle;

        public override void Execute(object parameter)
        {
            try
            {
                Enabled = false;
                if (parameter is not DuplicateFile duplicateFile)
                    return;

                var isMarkedForDeletion = duplicateFile.IsMarkedForDeletion = !duplicateFile.IsMarkedForDeletion;
                var size = isMarkedForDeletion ? duplicateFile.FileData.Size : -duplicateFile.FileData.Size;
                var count = isMarkedForDeletion ? 1 : -1;
                OnDeletionMarkToggle(count, size);
            }
            finally
            {
                Enabled = true;
            }
        }

        protected virtual void OnDeletionMarkToggle(long count, long size)
        {
            DeletionMarkToggle?.Invoke(this, new UpdateToDeleteEventArgs(count, size));
        }
    }
}
