namespace DuplicateFileTool.Commands;

internal sealed class ResetSelectionCommand : CommandBase
{
    private DeletionSelection DeletionSelection { get; }

    public ResetSelectionCommand(DeletionSelection deletionSelection)
    {
        Enabled = false;
        DeletionSelection = deletionSelection;
    }

    public override void Execute(object? parameter)
    {
        // Clear the entire unified set (duplicate + non-duplicate + folder marks) in one shot. This fires a single
        // Reset change event, which the engine handles by zeroing the to-be-deleted totals while every row refreshes.
        // Walking DuplicateGroups would only reach duplicate marks and miss non-duplicate and folder marks.
        DeletionSelection.Clear();
    }
}
