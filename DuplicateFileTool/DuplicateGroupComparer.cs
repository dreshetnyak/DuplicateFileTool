using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using DuplicateFileTool.Annotations;
using DuplicateFileTool.Configuration;

namespace DuplicateFileTool
{
    internal class DuplicateGroupComparer : IComparer<DuplicateGroup>, INotifyPropertyChanged
    {
        public ResultsConfiguration Config { get; }
        private SortOrder _selectedSortOrder;
        private bool _isSortOrderDescending;

        public SortOrder SelectedSortOrder
        {
            get => _selectedSortOrder;
            set
            {
                if (_selectedSortOrder == value)
                    return;
                _selectedSortOrder = value;
                Config.SortOrder.Value = value;
                OnPropertyChanged();
            }
        }

        public bool IsSortOrderDescending
        {
            get => _isSortOrderDescending;
            set
            {
                if (_isSortOrderDescending == value)
                    return;
                _isSortOrderDescending = value;
                Config.SortDescending.Value = value;
                OnPropertyChanged();
            }
        }

        public DuplicateGroupComparer(ResultsConfiguration config)
        {
            Config = config;
            _selectedSortOrder = config.SortOrder.Value;
            _isSortOrderDescending = config.SortDescending.Value;
        }

        public int Compare(DuplicateGroup left, DuplicateGroup right)
        {
            if (left == null || right == null)
                return 0;

            return SelectedSortOrder switch
            {
                SortOrder.Size => IsSortOrderDescending ? CompareGroupSizes(right, left) : CompareGroupSizes(left, right),
                SortOrder.Name => IsSortOrderDescending ? CompareGroupNames(right, left) : CompareGroupNames(left, right),
                SortOrder.Path => IsSortOrderDescending ? CompareGroupPaths(right, left) : CompareGroupPaths(left, right),
                SortOrder.Number => IsSortOrderDescending ? CompareGroupNumbers(right, left) : CompareGroupNumbers(left, right),
                _ => 0
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int CompareGroupSizes(DuplicateGroup left, DuplicateGroup right)
        {
            var leftGroupSize = left.DuplicatedSize;
            var rightGroupSize = right.DuplicatedSize;
            return leftGroupSize != rightGroupSize
                ? leftGroupSize > rightGroupSize ? 1 : -1
                : 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int CompareGroupNames(DuplicateGroup left, DuplicateGroup right)
        {
            var leftGroupFile = left.DuplicateFiles.FirstOrDefault();
            var rightGroupFile = right.DuplicateFiles.FirstOrDefault();
            return leftGroupFile != null && rightGroupFile != null
                ? string.Compare(leftGroupFile.FileData.FileName, rightGroupFile.FileData.FileName, StringComparison.CurrentCulture)
                : 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int CompareGroupPaths(DuplicateGroup left, DuplicateGroup right)
        {
            var leftGroupFile = left.DuplicateFiles.FirstOrDefault();
            var rightGroupFile = right.DuplicateFiles.FirstOrDefault();
            return leftGroupFile != null && rightGroupFile != null
                ? string.Compare(leftGroupFile.FileFullName, rightGroupFile.FileFullName, StringComparison.CurrentCulture)
                : 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int CompareGroupNumbers(DuplicateGroup left, DuplicateGroup right)
        {
            var leftGroupNumber = left.GroupNumber;
            var rightGroupNumber = right.GroupNumber;
            return leftGroupNumber != rightGroupNumber
                ? leftGroupNumber > rightGroupNumber ? 1 : -1
                : 0;
        }

        #region INotifyPropertyChanged Implementation

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}
