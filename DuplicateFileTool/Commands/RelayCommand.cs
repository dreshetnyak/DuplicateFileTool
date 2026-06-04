namespace DuplicateFileTool.Commands;

internal sealed class RelayCommand(Action<object?> command, bool enabled = true) : CommandBase(enabled)
{
    public Action<object?> Command { get; } = command;

    public override void Execute(object? parameter)
    {
        try
        {
            Enabled = false;
            Command(parameter);
        }
        finally
        {
            Enabled = true;
        }
    }
}