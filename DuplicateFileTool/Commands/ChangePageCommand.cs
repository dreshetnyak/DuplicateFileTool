using System;

namespace DuplicateFileTool.Commands
{
    internal class ChangePageCommand : CommandBase
    {
        private ObservableCollectionProxy<DuplicateGroup> Collection { get; }
        private Action RefreshCollectionView { get; }

        public ChangePageCommand(ObservableCollectionProxy<DuplicateGroup> collection, Action refreshCollectionView)
        {
            Collection = collection;
            RefreshCollectionView = refreshCollectionView;
        }

        public override void Execute(object parameter)
        {
            var command = parameter as string;
            switch (command)
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

            RefreshCollectionView(); //Otherwise scrollbar does not return to the beginning of the tree
        }
    }
}
