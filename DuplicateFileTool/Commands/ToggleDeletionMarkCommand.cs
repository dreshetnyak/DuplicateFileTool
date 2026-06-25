namespace DuplicateFileTool.Commands;

internal sealed class ToggleDeletionMarkCommand : CommandBase
{
    public override void Execute(object? parameter)
    {
        try
        {
            Enabled = false;
            if (parameter is not DuplicateFile duplicateFile)
                return;

            // Toggling IsMarkedForDeletion mutates the engine's DeletionSelection set, which drives the
            // to-be-deleted totals via DuplicatesEngine.OnDeletionSelectionChanged. No delta event is fired here.
            duplicateFile.IsMarkedForDeletion = !duplicateFile.IsMarkedForDeletion;
        }
        finally
        {
            Enabled = true;
        }
    }
}