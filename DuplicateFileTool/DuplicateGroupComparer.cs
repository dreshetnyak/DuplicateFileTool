using System.ComponentModel;
using System.Runtime.CompilerServices;
using DuplicateFileTool.Configuration;

namespace DuplicateFileTool;

[Localizable(true)]
internal sealed class DuplicateGroupComparer(ResultsConfiguration config) : IComparer<DuplicateGroup>, INotifyPropertyChanged
{
    public ResultsConfiguration Config { get; } = config;
    private SortOrder _selectedSortOrder = config.SortOrder.Value;
    private bool _isSortOrderDescending = config.SortDescending.Value;

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

    public int Compare(DuplicateGroup? left, DuplicateGroup? right)
    {
        if (left == null || right == null)
            return 0;

        return SelectedSortOrder switch
        {
#pragma warning disable S2234
            SortOrder.Size => IsSortOrderDescending ? CompareGroupSizes(right, left) : CompareGroupSizes(left, right),
            SortOrder.Name => IsSortOrderDescending ? CompareGroupNames(right, left) : CompareGroupNames(left, right),
            SortOrder.Path => IsSortOrderDescending ? CompareGroupPaths(right, left) : CompareGroupPaths(left, right),
            SortOrder.Number => IsSortOrderDescending ? CompareGroupNumbers(right, left) : CompareGroupNumbers(left, right),
#pragma warning restore S2234
            _ => 0
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CompareGroupSizes(DuplicateGroup left, DuplicateGroup right)
    {
        var leftGroupSize = left.DuplicatedSize;
        var rightGroupSize = right.DuplicatedSize;
        if (leftGroupSize == rightGroupSize)
            return 0;
        return leftGroupSize > rightGroupSize ? 1 : -1;
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
        if (leftGroupNumber == rightGroupNumber)
            return 0;
        return leftGroupNumber > rightGroupNumber ? 1 : -1;
    }

    #region INotifyPropertyChanged Implementation

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string propertyName = "") => 
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    #endregion
}