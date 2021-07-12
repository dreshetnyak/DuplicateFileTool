using System;
using System.Collections.Generic;
using System.Linq;

namespace DuplicateFileTool.Commands
{
    internal class AddPathCommand : CommandBase
    {
        private ICollection<SearchPath> SearchPaths { get; }
        private Func<FileTreeItem> GetSelectedFileTreeItem { get; }

        public AddPathCommand(ICollection<SearchPath> searchPaths, Func<FileTreeItem> getSelectedFileTreeItem)
        {
            Enabled = false;
            SearchPaths = searchPaths;
            GetSelectedFileTreeItem = getSelectedFileTreeItem;
        }

        public override void Execute(object parameter)
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
            return !string.IsNullOrEmpty(path) &&
                SearchPaths.All(existingPath => !existingPath.Path.Equals(path, StringComparison.OrdinalIgnoreCase)) && 
                FileSystem.PathExists(path);
        }
    }
}
