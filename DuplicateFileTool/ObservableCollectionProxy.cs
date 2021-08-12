﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using DuplicateFileTool.Annotations;

namespace DuplicateFileTool
{
    internal sealed class ObservableCollectionProxy<T> : Collection<T>, INotifyCollectionChanged, INotifyPropertyChanged
    {
        private const string CountString = nameof(Count);
        private const string IndexerName = "Item[]";

        private int _itemsPerPage;
        private int _currentPage;
        private int _totalPages;
        private T _selectedItem;

        private ObservableCollection<T> SourceCollection { get; }
        private List<T> FilteredItems { get; }
        private IComparer<T> Comparer { get; }
        private Func<T, bool> ItemFilter { get; }

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

        public T SelectedItem
        {
            get => _selectedItem;
            set
            {
                _selectedItem = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public event NotifyCollectionChangedEventHandler CollectionChanged;

        public ObservableCollectionProxy([NotNull] ObservableCollection<T> sourceCollection, [NotNull] Func<T, bool> itemFilter, [NotNull] IComparer<T> comparer, int itemsPerPage)
        {
            SourceCollection = sourceCollection;
            ItemFilter = itemFilter;
            Comparer = comparer;
            ItemsPerPage = itemsPerPage;
            FilteredItems = sourceCollection.AsParallel().Where(itemFilter).ToList();
            FilteredItems.Sort(comparer);

            TotalPages = GetTotalPages(FilteredItems.Count, itemsPerPage);
            foreach (var item in GetPageItems(0))
                Items.Add(item);
            
            SourceCollection.CollectionChanged += OnSourceCollectionChanged;
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

        private IEnumerable<T> GetPageItems(int page)
        {
            var itemsPerPage = ItemsPerPage;
            var startIndex = itemsPerPage * page;
            var endIndex = startIndex + itemsPerPage;
            var filteredItemsCount = FilteredItems.Count;

            for (var index = startIndex; index < endIndex && index < filteredItemsCount; index++)
                yield return FilteredItems[index];
        }

        private void LoadPage(int page)
        {
            List<T> oldList;
            if (Items.Count != 0)
            {
                oldList = new List<T>(Items);
                Items.Clear();
            }
            else
                oldList = null;

            foreach (var item in GetPageItems(page))
                Items.Add(item);

            CurrentPage = page;

            OnPropertyChanged(CountString);
            OnPropertyChanged(IndexerName);
            if (oldList != null)
                OnCollectionChanged(NotifyCollectionChangedAction.Replace, (IList)Items, oldList, 0);
            else
                OnCollectionChanged(NotifyCollectionChangedAction.Add, (IList)Items, 0);
        }

        #region Collection<T> Overrides

        protected override void ClearItems()
        {
            SourceCollection.Clear();
            ResetTarget();

            OnPropertyChanged(CountString);
            OnPropertyChanged(IndexerName);
            OnCollectionReset();
        }

        protected override void InsertItem(int index, T item)
        {
            SourceCollection.Add(item);
            if (!ItemFilter(item))
                return;

            var itemIndex = InsertSorted(FilteredItems, Comparer, item);
            var itemPage = GetItemPage(ItemsPerPage, itemIndex);
            LoadPage(itemPage);
            SelectedItem = item;

            OnPropertyChanged(CountString);
            OnPropertyChanged(IndexerName);
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
            SelectedItem = index < itemsCount
                ? Items[index]
                : itemsCount != 0
                    ? Items[itemsCount - 1]
                    : default;

            OnPropertyChanged(CountString);
            OnPropertyChanged(IndexerName);
            OnCollectionChanged(NotifyCollectionChangedAction.Remove, removedItem, index);
        }

        protected override void SetItem(int index, T item)
        {
            var oldItem = this[index];

            SourceCollection[SourceCollection.IndexOf(oldItem)] = item;
            FilteredItems[FilteredItems.IndexOf(oldItem)] = item;
            Items[index] = item;
            SelectedItem = item;

            OnPropertyChanged(IndexerName);
            OnCollectionChanged(NotifyCollectionChangedAction.Replace, oldItem, item, index);
        }

        #endregion

        public void Move(int oldIndex, int newIndex)
        {
            throw new NotImplementedException();

            //var obj = this[oldIndex];

            //base.RemoveItem(oldIndex);
            //base.InsertItem(newIndex, obj);

            //OnPropertyChanged(IndexerName);
            //OnCollectionChanged(NotifyCollectionChangedAction.Move, obj, newIndex, oldIndex);
        }

        private void ResetTarget()
        {
            Items.Clear();
            FilteredItems.Clear();

            SelectedItem = default;

            CurrentPage = 0;
            TotalPages = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetItemPage(int itemsPerPage, int itemIndex)
        {
            var itemPage = itemIndex / itemsPerPage;
            return itemIndex % itemsPerPage != 0 ? itemPage + 1 : itemPage;
        }

        private static int InsertSorted(IList<T> filteredItems, IComparer<T> comparer, T item)
        {
            var filteredItemsCount = filteredItems.Count;
            if (comparer == null)
                return filteredItemsCount;

            var index = 0;
            for (; index < filteredItemsCount; index++)
            {
                if (comparer.Compare(filteredItems[index], item) < 0)
                    continue;
                break;
            }

            if (index < filteredItemsCount)
                filteredItems.Insert(index, item);
            else
                filteredItems.Add(item);

            return index;
        }

        private void OnSourceCollectionChanged(object sender, NotifyCollectionChangedEventArgs eventArgs)
        {
            switch (eventArgs.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    OnItemAdded(eventArgs.NewStartingIndex);
                    break;
                case NotifyCollectionChangedAction.Remove:
                    OnItemRemoved(eventArgs.OldItems);
                    break;
                case NotifyCollectionChangedAction.Replace:
                    OnItemReplaced(eventArgs.NewStartingIndex);
                    break;
                case NotifyCollectionChangedAction.Move:
                    OnItemMoved(eventArgs.OldStartingIndex, eventArgs.NewStartingIndex);
                    break;
                case NotifyCollectionChangedAction.Reset:
                    ResetTarget();
                    break;
            }
        }

        private void OnItemAdded(int newItemIndex)
        {
            var newItem = SourceCollection[newItemIndex];
            if (!ItemFilter(newItem))
                return;
            var filteredIndex = InsertSorted(FilteredItems, Comparer, newItem);
            var itemPage = GetItemPage(ItemsPerPage, filteredIndex);

            TotalPages = GetTotalPages(FilteredItems.Count, ItemsPerPage);

            SelectedItem = newItem;

            LoadPage(itemPage);
        }

        private void OnItemRemoved(IEnumerable removedSourceItems)
        {
            foreach (T removedSourceItem in removedSourceItems)
            {
                if (!ItemFilter(removedSourceItem))
                    continue;

                var filteredItemIndex = FilteredItems.IndexOf(removedSourceItem);
                if (filteredItemIndex == -1)
                    continue;
                
                FilteredItems.RemoveAt(filteredItemIndex);
                TotalPages = GetTotalPages(FilteredItems.Count, ItemsPerPage);

                var itemIndex = Items.IndexOf(removedSourceItem);
                if (itemIndex != -1)
                    Items.RemoveAt(itemIndex);

                var itemPage = GetItemPage(ItemsPerPage, filteredItemIndex);
                if (itemPage > CurrentPage)
                    continue;

                //TODO

                //if (SelectedItem == removedSourceItem)


                //SelectedItem = index < itemsCount
                //    ? Items[index]
                //    : itemsCount != 0
                //        ? Items[itemsCount - 1]
                //        : default;

                LoadPage(CurrentPage);
            }
        }

        private void OnItemReplaced(int replacedItemIndex)
        {
            throw new NotImplementedException();
        }

        private void OnItemMoved(int oldStartingIndex, int newStartingIndex)
        {
            throw new NotImplementedException();
        }

        #region Event Invokators
        [NotifyPropertyChangedInvocator]
        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void OnCollectionChanged(NotifyCollectionChangedAction action, object item, int index) => CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(action, item, index));
        private void OnCollectionChanged(NotifyCollectionChangedAction action, object oldItem, object newItem, int index) => CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(action, newItem, oldItem, index));
        private void OnCollectionReset() => CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));

        #endregion
    }
}