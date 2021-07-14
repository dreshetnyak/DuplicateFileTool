using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using Application = System.Windows.Application;

namespace DuplicateFileTool
{
    [Localizable(true)]
    internal class PagedObservableCollectionView<T> : NotifyPropertyChanged
    {
        private bool _isNextPageExists;
        private bool _isPreviousPageExists;
        private ObservableCollection<T> ParentCollection { get; }
        private int ParentIndex { get; set; }
        private int ItemsPerPage { get; }
        private int CurrentPage { get; set; }
        private int TotalPages { get; set; }

        public string PageInfo => $"Page {CurrentPage} / {TotalPages}";

        public bool IsNextPageExists
        {
            get => _isNextPageExists;
            set
            {
                if (_isNextPageExists == value)
                    return;
                _isNextPageExists = value;
                OnPropertyChanged();
            }
        }

        public bool IsPreviousPageExists
        {
            get => _isPreviousPageExists;
            set
            {
                if (_isPreviousPageExists == value)
                    return;
                _isPreviousPageExists = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<T> Collection { get; set; }
        public int Count => Collection?.Count ?? 0;

        public PagedObservableCollectionView(ObservableCollection<T> parentCollection, int itemsPerPage)
        {
            Collection = new ObservableCollection<T>();
            ItemsPerPage = itemsPerPage;
            ParentCollection = parentCollection;
            ParentCollection.CollectionChanged += OnParentCollectionChanged;
        }

        private void OnParentCollectionChanged(object sender, NotifyCollectionChangedEventArgs eventArgs)
        {
            switch (eventArgs.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    OnItemsAdded(eventArgs);
                    break;
                case NotifyCollectionChangedAction.Remove:
                    OnItemsRemoved(eventArgs);
                    break;
                case NotifyCollectionChangedAction.Reset:
                    OnCollectionReset();
                    break;
                case NotifyCollectionChangedAction.Replace:
                    Debug.WriteLine("NotifyCollectionChangedAction.Replace is not implemented, the view is incorrect.");
                    break;
                case NotifyCollectionChangedAction.Move:
                    Debug.WriteLine("NotifyCollectionChangedAction.Move is not implemented, the view is incorrect.");
                    break;
                default:
                    Debug.WriteLine("Unknown action that is not implemented, the view is incorrect.");
                    break;
            }
        }

        private void OnItemsRemoved(NotifyCollectionChangedEventArgs eventArgs)
        {
            try
            {
                var removalParentIndex = eventArgs.OldStartingIndex;
                if (IsIndexAfterCurrentPage(removalParentIndex))
                    return;

                if (IsIndexOnCurrentPage(removalParentIndex))
                    RemoveAt(Collection, removalParentIndex - ParentIndex);
                else if (Collection.Count != 0)
                    RemoveAt(Collection, 0);

                var parentCollectionCount = ParentCollection.Count;
                var itemsAfterIndex = parentCollectionCount - ParentIndex;

                if (itemsAfterIndex < ItemsPerPage)
                {
                    if (itemsAfterIndex == 0)
                        Previous();
                    return;
                }

                Add(Collection, ParentCollection[ParentIndex + ItemsPerPage - 1]); //-1 because the parent collection is already changed
            }
            finally
            {
                UpdatePageInfo();
            }
        }

        private void OnItemsAdded(NotifyCollectionChangedEventArgs eventArgs)
        {
            try
            {
                var additionParentIndex = eventArgs.NewStartingIndex;
                if (IsIndexAfterCurrentPage(additionParentIndex))
                    return;

                if (IsIndexBeforeCurrentPage(additionParentIndex))
                {
                    Debug.WriteLine("Error. Element added before current page, this is not fully supported the entire view will be reloaded.");
                    LoadCurrentPage();
                    return;
                }

                //Addition is on the current page
                var startIndexInCollection = additionParentIndex - ParentIndex;
                if (startIndexInCollection >= Collection.Count) //If after the last collection parentIndex
                    AddItems(eventArgs.NewItems, startIndexInCollection);
                else
                    InsertItems(eventArgs.NewItems, startIndexInCollection);
            }
            finally
            {
                UpdatePageInfo();
            }
        }

        private void AddItems(IList newItems, int startIndexInCollection)
        {
            var newItemsCount = newItems.Count;
            var itemsCollectionCanReceive = ItemsPerPage - startIndexInCollection;

            for (var additionIndex = 0; additionIndex < newItemsCount && additionIndex < itemsCollectionCanReceive; additionIndex++)
                Add(Collection, (T)newItems[additionIndex]);
        }

        private void InsertItems(IList newItems, int startIndexInCollection)
        {
            var newItemsCount = newItems.Count;
            var itemsCollectionCanReceive = ItemsPerPage - startIndexInCollection;

            for (var additionIndex = 0; additionIndex < newItemsCount && additionIndex < itemsCollectionCanReceive; additionIndex++)
            {
                Collection.Insert(startIndexInCollection + additionIndex, (T)newItems[additionIndex]);
                if (Collection.Count > ItemsPerPage) //If after insertion we have exceeded the size remove the last item
                    Collection.RemoveAt(ItemsPerPage);
            }
        }

        private void OnCollectionReset()
        {
            Clear(Collection);
            ParentIndex = 0;
            UpdatePageInfo();
        }

        public void Next()
        {
            if (!IsNextPageExists)
                return;

            ParentIndex += ItemsPerPage;
            LoadCurrentPage();
            UpdatePageInfo();
        }

        public void Previous()
        {
            if (!IsPreviousPageExists)
                return;

            ParentIndex -= ItemsPerPage;
            LoadCurrentPage();
            UpdatePageInfo();
        }

        private static void Add(ObservableCollection<T> collection, T item)
        {
            Application.Current.Dispatcher?.Invoke(delegate { collection.Add(item); });
        }

        private static void RemoveAt(ObservableCollection<T> collection, int index)
        {
            Application.Current.Dispatcher?.Invoke(delegate { collection.RemoveAt(index); });
        }

        private static void Clear(ObservableCollection<T> collection)
        {
            Application.Current.Dispatcher?.Invoke(collection.Clear);
        }

        private void LoadCurrentPage()
        {
            Collection.Clear();

            for (var collectionIndex = 0; collectionIndex < ItemsPerPage; collectionIndex++)
            {
                var parentIndex = ParentIndex + collectionIndex;
                if (parentIndex >= ParentCollection.Count)
                    return;

                Add(Collection, ParentCollection[parentIndex]);
            }
        }

        private void UpdatePageInfo()
        {
            var newTotalPages = GetTotalPageCount();
            var newCurrentPage = GetCurrentPageNumber();
            var pageInfoUnchanged = newCurrentPage == CurrentPage && newTotalPages == TotalPages;
            if (pageInfoUnchanged)
                return;

            IsPreviousPageExists = GetIsPreviousPageExists();
            IsNextPageExists = GetIsNextPageExists();

            CurrentPage = newCurrentPage;
            TotalPages = newTotalPages;
            OnPropertyChanged(nameof(PageInfo));
        }

        private bool IsIndexAfterCurrentPage(int parentIndex)
        {
            return parentIndex >= ParentIndex + ItemsPerPage;
        }

        private bool IsIndexOnCurrentPage(int parentIndex)
        {
            return parentIndex >= ParentIndex && parentIndex < ParentIndex + ItemsPerPage;
        }

        private bool IsIndexBeforeCurrentPage(int parentIndex)
        {
            return parentIndex < ParentIndex;
        }

        private bool GetIsPreviousPageExists()
        {
            return ParentIndex != 0;
        }

        private bool GetIsNextPageExists()
        {
            return ParentCollection.Count - ParentIndex > ItemsPerPage;
        }

        private int GetCurrentPageNumber()
        {
            var parentItemNumber = ParentIndex + 1;
            var currentPage = parentItemNumber / ItemsPerPage;
            if (parentItemNumber % ItemsPerPage != 0)
                currentPage++;
            return currentPage;
        }

        private int GetTotalPageCount()
        {
            var parentCollectionCount = ParentCollection.Count;
            var pagesCount = parentCollectionCount / ItemsPerPage;
            if (parentCollectionCount % ItemsPerPage != 0 || parentCollectionCount == 0)
                pagesCount++;

            return pagesCount;
        }
    }
}
