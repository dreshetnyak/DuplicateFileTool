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
                if (parameter is not DuplicateFile duplicateFile)
                    return;

                var filePathName = duplicateFile.FileFullName;
                if (!FileSystem.PathExists(filePathName))
                    return;

                Process.Start("explorer.exe", $"/select, \"{filePathName}\"");
            }
            finally
            {
                Enabled = true;
            }
        }
    }
}
