using System.Diagnostics;

namespace DuplicateFileTool.Commands;

internal sealed class OpenFileInExplorerCommand : CommandBase
{
    public override void Execute(object? parameter)
    {
        try
        {
            Enabled = false;
                
            string filePathName;
            switch (parameter)
            {
                case DuplicateFile duplicateFile:
                    filePathName = duplicateFile.FileFullName;
                    break;
                case FileTreeItem fileTreeItem:
                    filePathName = fileTreeItem.ItemPath;
                    break;
                default:
                    return;
            }

            if (FileSystem.PathExists(filePathName))
                Process.Start("explorer.exe", $"/select, \"{filePathName}\"");
        }
        finally
        {
            Enabled = true;
        }
    }
}