using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using DuplicateFileTool.Annotations;

namespace DuplicateFileTool
{
    internal class DuplicateGroupComparer : IComparer<DuplicateGroup>, INotifyPropertyChanged
    {
        private SortOrder _selectedSortOrder;

        public SortOrder SelectedSortOrder
        {
            get => _selectedSortOrder;
            set
            {
                if (_selectedSortOrder == value)
                    return;
                _selectedSortOrder = value;
                OnPropertyChanged();
            }
        }

        public DuplicateGroupComparer(SortOrder sortOrder)
        {
            _selectedSortOrder = sortOrder;
        }

        public int Compare(DuplicateGroup left, DuplicateGroup right)
        {
            if (left == null || right == null)
                return 0;

            return SelectedSortOrder switch
            {
                SortOrder.Size => CompareGroupSizes(left, right),
                SortOrder.Name => CompareGroupNames(left, right),
                SortOrder.Path => CompareGroupPaths(left, right),
                SortOrder.Number => CompareGroupNumbers(left, right),
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
