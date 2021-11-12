using System.Diagnostics;

namespace DuplicateFileTool.Commands
{
    internal class OpenFileInExplorerCommand : CommandBase
    {
        public override void Execute(object parameter)
        {
            try
            {
                Enabled = false;
                
                string filePathName;
                if (parameter is DuplicateFile duplicateFile)
                    filePathName = duplicateFile.FileFullName;
                else if (parameter is FileTreeItem fileTreeItem)
                    filePathName = fileTreeItem.ItemPath;
                else
                    return;

                if (FileSystem.PathExists(filePathName))
                    Process.Start("explorer.exe", $"/select, \"{filePathName}\"");
            }
            finally
            {
                Enabled = true;
            }
        }
    }
}
