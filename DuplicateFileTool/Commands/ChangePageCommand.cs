namespace DuplicateFileTool.Commands;

internal sealed class ChangePageCommand(ObservableCollectionProxy<DuplicateGroup> collection) : CommandBase
{
    private ObservableCollectionProxy<DuplicateGroup> Collection { get; } = collection;

    public override void Execute(object? parameter)
    {
        switch (parameter as string)
        {
            case "Next":
                Collection.LoadNextPage();
                break;
            case "Previous":
                Collection.LoadPreviousPage();
                break;
            case "First":
                Collection.LoadFirstPage();
                break;
            case "Last":
                Collection.LoadLastPage();
                break;
        }
    }
}