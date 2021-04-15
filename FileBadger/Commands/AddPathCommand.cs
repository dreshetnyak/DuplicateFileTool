using System;
using System.Linq;

namespace FileBadger.Commands
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
            if (AppViewModel.SelectedFileSystemItem == null)
                return;

            try
            {
                Enabled = false;
                
                var itemPath = AppViewModel.SelectedFileSystemItem.ItemPath;
                var pathIsNotYetAdded = AppViewModel.SearchPaths.All(existingPath => !existingPath.Path.Equals(itemPath, StringComparison.Ordinal));
                if (pathIsNotYetAdded)
                    AppViewModel.SearchPaths.Add(new SearchPath(AppViewModel.SelectedFileSystemItem.ItemPath, InclusionType.Include));
            }
            finally
            {
                Enabled = true;
            }

        }
    }
}
