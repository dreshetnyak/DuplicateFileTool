using System;
using System.Linq;

namespace DuplicateFileTool.Commands
{
    internal class AddPathCommand : CommandBase
    {
        private MainViewModel AppViewModel { get; }

        public AddPathCommand(MainViewModel appViewModel)
        {
            AppViewModel = appViewModel;
        }

        public override void Execute(object parameter)
        {
            if (AppViewModel.SelectedFileTreeItem == null)
                return;

            try
            {
                Enabled = false;
                
                var itemPath = AppViewModel.SelectedFileTreeItem.ItemPath;
                var pathIsNotYetAdded = AppViewModel.SearchPaths.All(existingPath => !existingPath.Path.Equals(itemPath, StringComparison.Ordinal));
                if (pathIsNotYetAdded)
                    AppViewModel.SearchPaths.Add(new SearchPath(AppViewModel.SelectedFileTreeItem.ItemPath, InclusionType.Include));
            }
            finally
            {
                Enabled = true;
            }
        }
    }
}
