using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace DuplicateFileTool;

internal sealed class ObservableCollectionProxy<T> : Collection<T>, INotifyCollectionChanged, INotifyPropertyChanged where T : class
{
    private const string COUNT_STRING = nameof(Count);
    private const string INDEXER_NAME = "Item[]";

    private int _itemsPerPage;
    private int _currentPage;
    private int _totalPages;
    private T? _selectedItem;
    private bool _deferPageReload;
    private IReadOnlyList<T>? _pendingRemovalAnchors;

    private ObservableCollection<T> SourceCollection { get; }
    private List<T> FilteredItems { get; }
    private IComparer<T> Comparer { get; }
    private IInclusionPredicate<T> InclusionPredicate { get; }

    public int ItemsPerPage
    {
        get => _itemsPerPage;
        set
        {
            _itemsPerPage = value; 
            OnPropertyChanged();
        }
    }
    public int CurrentPage
    {
        get => _currentPage;
        private set
        {
            _currentPage = value;
            OnPropertyChanged();
        }
    }
    public int TotalPages
    {
        get => _totalPages;
        private set
        {
            _totalPages = value;
            OnPropertyChanged();
        }
    }

    public T? SelectedItem
    {
        get => _selectedItem;
        set
        {
            _selectedItem = value;
            OnPropertyChanged();
        }
    }

    public bool SelectNewItem { get; set; } = true;
    public bool SortingEnabled { get; set; } = true;

    public bool HasPages => TotalPages != 0;
    public bool NextPageExists => CurrentPage < TotalPages;
    public bool PreviousPageExists => CurrentPage > 1;

    public event EventHandler? PageChanged;
    public event PropertyChangedEventHandler? PropertyChanged;
    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    public ObservableCollectionProxy(ObservableCollection<T> sourceCollection, IInclusionPredicate<T> inclusionPredicate, IComparer<T> comparer, int itemsPerPage)
    {
        SourceCollection = sourceCollection;
        InclusionPredicate = inclusionPredicate;
        Comparer = comparer;
        ItemsPerPage = itemsPerPage;
        FilteredItems = sourceCollection.AsParallel().Where(inclusionPredicate.IsIncluded).ToList();
        FilteredItems.Sort(comparer);

        TotalPages = GetTotalPages(FilteredItems.Count, itemsPerPage);
        foreach (var item in GetPageItems(1))
            Items.Add(item);

        SourceCollection.CollectionChanged += OnSourceCollectionChanged;
    }

    public void Filter()
    {
        FilteredItems.Clear();
        FilteredItems.AddRange(SourceCollection.AsParallel().Where(InclusionPredicate.IsIncluded));
        TotalPages = GetTotalPages(FilteredItems.Count, ItemsPerPage);
        if (TotalPages != 0)
            Sort();
        else
        {
            ResetTarget();
            OnCollectionReset();
        }
    }

    public void Sort()
    {
        if (TotalPages == 0)
            return;
        FilteredItems.Sort(Comparer);
        LoadPage(1);
    }

    public void LoadNextPage()
    {
        if (CurrentPage < TotalPages)
            LoadPage(CurrentPage + 1);
    }

    public void LoadPreviousPage()
    {
        if (CurrentPage > 1)
            LoadPage(CurrentPage - 1);
    }

    public void LoadFirstPage()
    {
        if (CurrentPage != 1 && TotalPages != 0)
            LoadPage(1);
    }

    public void LoadLastPage()
    {
        if (CurrentPage != TotalPages)
            LoadPage(TotalPages);
    }

    // A bulk source removal (a deletion run) removes many groups across pages. Begin/End wrap it so the page is chosen
    // once, when the run has fully finished, instead of reshuffling on every per-item removal. BeginBulkRemoval
    // snapshots the survivor-priority anchors (the filtered items from the current page's first slot to the end) and
    // tells OnSourceItemRemoved to skip the back-fill reload that would otherwise scramble the visible page.
    public void BeginBulkRemoval()
    {
        _pendingRemovalAnchors = CapturePageAnchors();
        _deferPageReload = true;
    }

    // Ends the bulk removal started by BeginBulkRemoval: re-enables page reloads and navigates to the page holding the
    // first anchor that survived the run; if none survived, the survivors all precede the old current page, so the last
    // page is shown. Safe to call even when BeginBulkRemoval captured nothing.
    public void EndBulkRemoval()
    {
        _deferPageReload = false;
        var anchors = _pendingRemovalAnchors;
        _pendingRemovalAnchors = null;
        if (anchors != null)
            RestorePage(anchors);
    }

    private IReadOnlyList<T> CapturePageAnchors()
    {
        var startIndex = ItemsPerPage * (CurrentPage - 1);
        if (startIndex < 0 || startIndex >= FilteredItems.Count)
            return [];
        return FilteredItems.GetRange(startIndex, FilteredItems.Count - startIndex);
    }

    private void RestorePage(IReadOnlyList<T> anchors)
    {
        if (TotalPages == 0)
            return; //Nothing left to show; the source-reset path already cleared the view.

        foreach (var anchor in anchors)
        {
            var index = FilteredItems.IndexOf(anchor);
            if (index == -1)
                continue; //This anchor was removed by the run; try the next one in priority order.
            LoadPage(GetItemPage(ItemsPerPage, index));
            return;
        }

        LoadPage(TotalPages); //No anchor survived: every survivor precedes the old current page.
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetTotalPages(int filteredCount, int itemsPerPage)
    {
        if (filteredCount == 0)
            return 0;
        var pagesCount = filteredCount / itemsPerPage;
        if (filteredCount % itemsPerPage != 0)
            pagesCount++;
        return pagesCount;
    }

    private T[] GetPageItems(int page)
    {
        if (page == 0)
            return [];

        var itemsPerPage = ItemsPerPage;
        var startIndex = itemsPerPage * (page - 1);
        var endIndex = startIndex + itemsPerPage;
        var filteredItemsCount = FilteredItems.Count;
            
        var itemsCount = (endIndex < filteredItemsCount ? endIndex : filteredItemsCount) - startIndex;
        var items = new T[itemsCount];
            
        for (var index = 0; index < itemsCount; index++)
            items[index] = FilteredItems[startIndex + index];

        return items;
    }

    private void LoadPage(int page)
    {
        var pageChanged = CurrentPage != page;
        CurrentPage = page;
        var pageItems = GetPageItems(page);

        var existingItemsCount = Items.Count;
        var pageItemsLength = pageItems.Length;
        for (var itemIndex = 0; itemIndex < pageItemsLength; itemIndex++)
        {
            // ReSharper disable once RedundantAssignment
            T? existingItem = null;
            var newItem = pageItems[itemIndex];
            switch (itemIndex < existingItemsCount) //If item at the index exists
            {
                case true when ReferenceEquals(newItem, existingItem = Items[itemIndex]):
                    continue;
                case true:
                    Items[itemIndex] = newItem;
                    OnCollectionChanged(NotifyCollectionChangedAction.Replace, existingItem, newItem, itemIndex);
                    pageChanged = true;
                    break;
                default:
                    Items.Add(newItem);
                    OnCollectionChanged(NotifyCollectionChangedAction.Add, newItem, itemIndex);
                    pageChanged = true;
                    break;
            }
        }

        if (pageItemsLength < existingItemsCount)
        {
            for (var indexToRemove = existingItemsCount - 1; indexToRemove >= pageItemsLength; indexToRemove--)
            {
                var removedItem = Items[indexToRemove];
                Items.RemoveAt(indexToRemove);
                OnPropertyChanged(COUNT_STRING);
                OnPropertyChanged(INDEXER_NAME);
                OnCollectionChanged(NotifyCollectionChangedAction.Remove, removedItem, indexToRemove);
                pageChanged = true;
            }
        }

        OnPropertyChanged(nameof(HasPages));
        OnPropertyChanged(nameof(PreviousPageExists));
        OnPropertyChanged(nameof(NextPageExists));
        if (pageChanged)
            OnPageChanged();
    }

    #region Collection<T> Overrides

    protected override void ClearItems()
    {
        SourceCollection.Clear();
        ResetTarget();

        OnPropertyChanged(COUNT_STRING);
        OnPropertyChanged(INDEXER_NAME);
        OnCollectionReset();
    }

    protected override void InsertItem(int index, T item)
    {
        SourceCollection.Add(item);
        if (!InclusionPredicate.IsIncluded(item))
            return;

        var itemIndex = InsertSorted(item);
        var itemPage = GetItemPage(ItemsPerPage, itemIndex);
        LoadPage(itemPage);
        SelectedItem = item;

        OnPropertyChanged(COUNT_STRING);
        OnPropertyChanged(INDEXER_NAME);
        OnCollectionChanged(NotifyCollectionChangedAction.Add, item, index);
    }

    protected override void RemoveItem(int index)
    {
        var removedItem = this[index];

        SourceCollection.Remove(removedItem);
        FilteredItems.Remove(removedItem);
        Items.RemoveAt(index);
        LoadPage(CurrentPage);
        var itemsCount = Items.Count;
        if (index < itemsCount)
            SelectedItem = Items[index];
        else
            SelectedItem = itemsCount != 0
                ? Items[itemsCount - 1]
                : default;

        OnPropertyChanged(COUNT_STRING);
        OnPropertyChanged(INDEXER_NAME);
        OnCollectionChanged(NotifyCollectionChangedAction.Remove, removedItem, index);
    }

    protected override void SetItem(int index, T item)
    {
        var oldItem = this[index];

        SourceCollection[SourceCollection.IndexOf(oldItem)] = item;
        FilteredItems[FilteredItems.IndexOf(oldItem)] = item;
        Items[index] = item;
        SelectedItem = item;

        OnPropertyChanged(INDEXER_NAME);
        OnCollectionChanged(NotifyCollectionChangedAction.Replace, oldItem, item, index);
    }

    #endregion

    private void ResetTarget()
    {
        Items.Clear();
        FilteredItems.Clear();

        SelectedItem = default;

        CurrentPage = 0;
        TotalPages = 0;

        OnPropertyChanged(nameof(HasPages));
        OnPropertyChanged(nameof(PreviousPageExists));
        OnPropertyChanged(nameof(NextPageExists));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetItemPage(int itemsPerPage, int itemIndex)
    {
        itemIndex++; //Item number is used to determine the page number
        var itemPage = itemIndex / itemsPerPage;
        return itemIndex % itemsPerPage != 0 ? itemPage + 1 : itemPage;
    }

    private int InsertSorted(T item)
    {
        var filteredItemsCount = FilteredItems.Count;

        int index;
        if (SortingEnabled)
        {
            for (index = 0; index < filteredItemsCount; index++)
            {
                if (Comparer.Compare(FilteredItems[index], item) >= 0)
                    break;
            }
        }
        else
            index = filteredItemsCount;

        if (index < filteredItemsCount)
            FilteredItems.Insert(index, item);
        else
            FilteredItems.Add(item);

        return index;
    }

    private void OnSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs eventArgs)
    {
        switch (eventArgs.Action)
        {
            case NotifyCollectionChangedAction.Add:
                OnSourceItemAdded(eventArgs.NewStartingIndex);
                break;
            case NotifyCollectionChangedAction.Remove:
                if (eventArgs.OldItems != null)
                    OnSourceItemRemoved(eventArgs.OldItems);
                break;
            case NotifyCollectionChangedAction.Reset:
                OnSourceCollectionReset();
                break;
            default:
                Debug.Fail($"Source collection action '{eventArgs.Action}' is not supported");
                break;
        }
    }

    private void OnSourceItemAdded(int newItemIndex)
    {
        var newItem = SourceCollection[newItemIndex];
        if (!InclusionPredicate.IsIncluded(newItem))
            return;
        var filteredIndex = InsertSorted(newItem);
        var itemPage = GetItemPage(ItemsPerPage, filteredIndex);

        TotalPages = GetTotalPages(FilteredItems.Count, ItemsPerPage);

        if (SelectNewItem)
            SelectedItem = newItem;
        else
            itemPage = GetSelectedItemPage();

        LoadPage(itemPage);
    }

    private int GetSelectedItemPage()
    {
        if (SelectedItem is null)
            return 1;
        var selectedItemIndex = FilteredItems.IndexOf(SelectedItem);
        return selectedItemIndex != -1
            ? GetItemPage(ItemsPerPage, selectedItemIndex)
            : 1;
    }

    private void OnSourceItemRemoved(IEnumerable removedSourceItems)
    {
        foreach (T removedSourceItem in removedSourceItems)
        {
            if (!InclusionPredicate.IsIncluded(removedSourceItem))
                continue;

            var filteredItemIndex = FilteredItems.IndexOf(removedSourceItem);
            if (filteredItemIndex == -1)
                continue;
                
            FilteredItems.RemoveAt(filteredItemIndex);
            TotalPages = GetTotalPages(FilteredItems.Count, ItemsPerPage);

            bool selectedItemWasRemoved;
            var itemIndex = Items.IndexOf(removedSourceItem);
            if (itemIndex != -1)
            {
                var itemToRemove = Items[itemIndex];
                selectedItemWasRemoved = SelectedItem != null && itemToRemove == SelectedItem;
                Items.RemoveAt(itemIndex);
                OnCollectionChanged(NotifyCollectionChangedAction.Remove, itemToRemove, itemIndex);
            }
            else
                selectedItemWasRemoved = false;

            // During a bulk removal (deletion run) keep FilteredItems/TotalPages and the visible list trimmed for live
            // feedback, but skip the back-fill reload that would reshuffle the page; EndBulkRemoval picks the page once.
            var itemPage = GetItemPage(ItemsPerPage, filteredItemIndex);
            if (!_deferPageReload && itemPage <= CurrentPage)
                LoadPage(CurrentPage > TotalPages ? TotalPages : CurrentPage);

            var itemsCount = Items.Count;
            if (!selectedItemWasRemoved || itemsCount == 0)
                continue;

            SelectedItem = Items[itemIndex < itemsCount ? itemIndex : itemsCount - 1];
        }
    }

    private void OnSourceCollectionReset()
    {
        ResetTarget();

        OnPropertyChanged(COUNT_STRING);
        OnPropertyChanged(INDEXER_NAME);
        OnCollectionReset();
    }

    #region Event Invokators
    private void OnPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void OnCollectionChanged(NotifyCollectionChangedAction action, object item, int index) => CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(action, item, index));
    private void OnCollectionChanged(NotifyCollectionChangedAction action, object oldItem, object newItem, int index) => CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(action, newItem, oldItem, index));
    private void OnCollectionReset() => CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    private void OnPageChanged() => PageChanged?.Invoke(this, EventArgs.Empty);

    #endregion
}