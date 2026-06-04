namespace DuplicateFileTool.Commands;

internal sealed class UpdateToDeleteEventArgs(long count, long size) : EventArgs
{
    public long Count { get; } = count;
    public long Size { get; set; } = size;
}

internal delegate void UpdateToDeleteEventHandler(object? sender, UpdateToDeleteEventArgs eventArgs);
    
internal sealed class ToggleDeletionMarkCommand : CommandBase
{
    public event UpdateToDeleteEventHandler? DeletionMarkToggle;

    public override void Execute(object? parameter)
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

    private void OnDeletionMarkToggle(long count, long size) => 
        DeletionMarkToggle?.Invoke(this, new UpdateToDeleteEventArgs(count, size));
}