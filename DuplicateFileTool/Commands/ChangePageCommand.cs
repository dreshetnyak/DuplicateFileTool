using System;

namespace DuplicateFileTool.Commands
{
    internal class ChangePageCommand : CommandBase
    {
        private PagedObservableCollectionView<DuplicateGroup> Collection { get; }
        private Action RefreshCollectionView { get; }

        public ChangePageCommand(PagedObservableCollectionView<DuplicateGroup> collection, Action refreshCollectionView)
        {
            Collection = collection;
            RefreshCollectionView = refreshCollectionView;
        }

        public override void Execute(object parameter)
        {
            string command = parameter as string;
            switch (command)
            {
                case "Next":
                    Collection.Next();
                    break;
                case "Previous":
                    Collection.Previous();
                    break;
            }

            RefreshCollectionView(); //Otherwise scrollbar does not return to the beginning of the tree
        }
    }
}
