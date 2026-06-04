namespace DuplicateFileTool.Commands;

internal sealed class AddPathCommand : CommandBase
{
    private ICollection<SearchPath> SearchPaths { get; }
    private Func<FileTreeItem?> GetSelectedFileTreeItem { get; }

    public AddPathCommand(ICollection<SearchPath> searchPaths, Func<FileTreeItem?> getSelectedFileTreeItem)
    {
        Enabled = false;
        SearchPaths = searchPaths;
        GetSelectedFileTreeItem = getSelectedFileTreeItem;
    }

    public override void Execute(object? parameter)
    {
        var selectedFileTreeItem = GetSelectedFileTreeItem();
        if (selectedFileTreeItem == null)
            return;
        var itemPath = selectedFileTreeItem.ItemPath;
        if (!CanAddPath(itemPath))
            return;
        SearchPaths.Add(new SearchPath(itemPath, InclusionType.Include));
        Enabled = false;
    }

    public bool CanAddPath(string path)
    {
        if (string.IsNullOrEmpty(path)) 
            return false;
        
        foreach (var existingPath in SearchPaths)
        {
            if (existingPath.Path.Equals(path, StringComparison.OrdinalIgnoreCase)) 
                return false;
        }

        return FileSystem.PathExists(path);
    }
}