using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Media;

namespace FileBadger
{
    internal class FileTreeItem : NotifyPropertyChanged
    {
        public delegate void ItemSelectedEventHandler(object sender, EventArgs eventArgs);
        public static event ItemSelectedEventHandler ItemSelected;

        #region Backing Fields
        private bool _isExpanded;
        private ImageSource _itemIcon;
        private string _itemName = string.Empty;
        private bool _isSelected;

        #endregion

        private const FileTreeItem ChildPlaceholder = null;
        private bool ChildrenContainPlaceholder => Children != null && Children.Count == 1 && Children[0] == ChildPlaceholder;

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

        public string ItemPath => ItemFileData != null ? ItemFileData.FullName : string.Empty;

        public ImageSource ItemIcon
        {
            get => _itemIcon;
            private set
            {
                _itemIcon = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<FileTreeItem> Children { get; }

        public FileTreeItem()
        { }

        public FileTreeItem(FileData fileData, string itemName = null)
        {
            if (fileData == null)
                return;
            ItemFileData = fileData;

            UpdateIcon();
            ItemName = itemName ?? ItemFileData.FileName;

            Children = new ObservableCollection<FileTreeItem>();
            if (ItemFileData.Attributes.IsDirectory || ItemFileData.Attributes.IsDevice)
                Children.Add(ChildPlaceholder);
        }

        private void UpdateIcon()
        {
            ItemIcon = FileSystemIcon.GetImageSource(ItemFileData.FullName, IsExpanded ? Win32.ItemState.Open : Win32.ItemState.Close);
        }

        private void LoadChildren()
        {
            if (ChildrenContainPlaceholder)
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
            directoryContent.Sort((left, right) => left.Attributes.IsDirectory != right.Attributes.IsDirectory
                ? left.Attributes.IsDirectory ? -1 : 1
                : string.Compare(left.FileName, right.FileName, StringComparison.Ordinal));
        }

        public static IEnumerable<FileTreeItem> GetFileSystemItemsForDrives()
        {
            foreach (var drive in Drives.Get())
            {
                var fileData = new FileData(drive.Path, new Win32.WIN32_FIND_DATA
                {
                    dwFileAttributes = Win32.FileAttributes.Device,
                    ftCreationTime = new Win32.FILETIME(),
                    ftLastAccessTime = new Win32.FILETIME(),
                    ftLastWriteTime = new Win32.FILETIME(),
                    nFileSizeHigh = 0,
                    nFileSizeLow = 0,
                    dwReserved0 = 0,
                    dwReserved1 = 0,
                    cFileName = drive.Path,
                    cAlternate = drive.Path
                });

                yield return new FileTreeItem(fileData, drive.Name);
            }
        }

        public static FileTreeItem GetSelectedItem(IEnumerable<FileTreeItem> fileSystemItems)
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
}
