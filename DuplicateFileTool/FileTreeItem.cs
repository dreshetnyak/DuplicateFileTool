using System.Collections.ObjectModel;
using System.Windows.Media;

namespace DuplicateFileTool;

internal sealed class FileTreeItem : NotifyPropertyChanged
{
    public static event EventHandler? ItemSelected;

    #region Backing Fields
    private bool _isExpanded;
    private ImageSource? _itemIcon;
    private string _itemName = "";
    private bool _isSelected;

    #endregion

    private static readonly FileTreeItem ChildPlaceholder = new();
    private bool ChildrenContainsOnlyPlaceholder => Children.Count == 1 && ReferenceEquals(Children[0], ChildPlaceholder);

    private FileData ItemFileData { get; }

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value)
                return;

            if (value)
                LoadChildren();
            else
                RemoveChildren();

            _isExpanded = value;
            UpdateIcon();
            OnPropertyChanged();
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            _isSelected = value;
            ItemSelected?.Invoke(this, EventArgs.Empty);
            OnPropertyChanged();
        }
    }

    public string ItemName
    {
        get => _itemName;
        set
        {
            _itemName = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ItemPath));
        }
    }

    public string ItemPath => ItemFileData.FullName;

    public ImageSource? ItemIcon
    {
        get => _itemIcon;
        private set
        {
            _itemIcon = value;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<FileTreeItem> Children { get; } = [];

    private FileTreeItem()
    {
        ItemFileData = FileData.Empty;
    }

    public FileTreeItem(FileData fileData, string? itemName = null)
    {
        ItemFileData = fileData;

        UpdateIcon();
        ItemName = itemName ?? fileData.FileName;

        if (fileData.Attributes.IsDirectory || fileData.Attributes.IsDevice)
            Children.Add(ChildPlaceholder);
    }

    private void UpdateIcon() => 
        ItemIcon = FileSystemIcon.GetImageSource(ItemFileData.FullName, IsExpanded ? Win32.ItemState.Open : Win32.ItemState.Close);

    private void LoadChildren()
    {
        if (ChildrenContainsOnlyPlaceholder)
            Children.Clear();

        try
        {
            var directoryContent = new List<FileData>(new DirectoryEnumeration(ItemFileData.FullName));
            SortFileData(directoryContent);
            foreach (var fileData in directoryContent)
                Children.Add(new FileTreeItem(fileData));
        }
        catch (Exception)
        {
            Children.Clear();
        }
    }

    private void RemoveChildren()
    {
        Children.Clear();
        Children.Add(ChildPlaceholder);
    }

    private static void SortFileData(List<FileData> directoryContent)
    {
        directoryContent.Sort((left, right) =>
        {
            if (left.Attributes.IsDirectory == right.Attributes.IsDirectory)
                return string.Compare(left.FileName, right.FileName, StringComparison.OrdinalIgnoreCase);
            return left.Attributes.IsDirectory ? -1 : 1;
        });
    }

    public static IEnumerable<FileTreeItem> GetFileSystemItemsForDrives()
    {
        foreach (var drive in Drives.Get())
        {
            var fileData = new FileData(drive.Name, new Win32.WIN32_FIND_DATA
            {
                dwFileAttributes = Win32.FileAttributes.Device,
                ftCreationTime = new Win32.FILETIME(),
                ftLastAccessTime = new Win32.FILETIME(),
                ftLastWriteTime = new Win32.FILETIME(),
                nFileSizeHigh = 0,
                nFileSizeLow = 0,
                dwReserved0 = 0,
                dwReserved1 = 0,
                cFileName = drive.Name,
                cAlternate = drive.Name
            });

            yield return new FileTreeItem(fileData, drive.Description);
        }
    }

    public static FileTreeItem? GetSelectedItem(IEnumerable<FileTreeItem> fileSystemItems)
    {
        foreach (var fileSystemItem in fileSystemItems)
        {
            if (fileSystemItem.IsSelected)
                return fileSystemItem;

            if (!fileSystemItem.IsExpanded)
                continue;

            var selectedChild = GetSelectedItem(fileSystemItem.Children);
            if (selectedChild != null)
                return selectedChild;
        }

        return null;
    }
}