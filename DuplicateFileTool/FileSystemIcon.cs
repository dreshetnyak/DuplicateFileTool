using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DuplicateFileTool
{
    internal static class FileSystemIcon
    {
        public static Icon GetIcon(string fileSystemItemPath, Win32.ItemState itemState = Win32.ItemState.Undefined, Win32.IconSize iconSize = Win32.IconSize.Small)
        {
            try
            {
                var attributes = GetFileInfoRequestAttributes(fileSystemItemPath);
                var flags = GetFileInfoRequestFlags(itemState, iconSize, attributes);
                var fileInfo = new Win32.ShellFileInfo();
                var size = (uint)Marshal.SizeOf(fileInfo);

                var result = Win32.SHGetFileInfo(fileSystemItemPath, (uint)attributes, out fileInfo, size, flags);
                return result.IsValidHandle()
                    ? GetIconFromHandle(fileInfo.hIcon)
                    : EmptyIcon();
            }
            catch
            {
                return EmptyIcon();
            }
        }

        public static ImageSource GetImageSource(string fileSystemItemPath, Win32.ItemState itemState = Win32.ItemState.Undefined, Win32.IconSize iconSize = Win32.IconSize.Small)
        {
            using var fileIcon = GetIcon(fileSystemItemPath, itemState, iconSize);
            return IconToImageSource(fileIcon) ?? IconToImageSource(EmptyIcon());
        }

        private static ImageSource IconToImageSource(Icon fileIcon)
        {
            using var fileIconBitmap = fileIcon.ToBitmap();
            var fileIconBitmapHandle = IntPtr.Zero;
            try
            {
                fileIconBitmapHandle = fileIconBitmap.GetHbitmap();
                return Imaging.CreateBitmapSourceFromHBitmap(fileIconBitmapHandle, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            }
            catch
            {
                return null;
            }
            finally
            {
                if (fileIconBitmapHandle != IntPtr.Zero)
                    Win32.DeleteObject(fileIconBitmapHandle);
            }
        }

        private static uint GetFileInfoRequestFlags(Win32.ItemState itemState, Win32.IconSize iconSize, Win32.FileAttribute attributes)
        {
            var flags = (uint)(Win32.ShellAttribute.Icon | Win32.ShellAttribute.UseFileAttributes);
            if (attributes == Win32.FileAttribute.Directory && itemState == Win32.ItemState.Open)
                flags |= (uint)Win32.ShellAttribute.OpenIcon;
            flags |= iconSize == Win32.IconSize.Small ? (uint)Win32.ShellAttribute.SmallIcon : (uint)Win32.ShellAttribute.LargeIcon;
            return flags;
        }

        private static Win32.FileAttribute GetFileInfoRequestAttributes(string fileSystemItemPath)
        {
            if (IsPathToDrive(fileSystemItemPath))
                return Win32.FileAttribute.Directory;

            var itemData = FileSystem.GetFileSystemItemData(fileSystemItemPath);
            if (itemData == null)
                throw new ApplicationException($"Unable to retrieve the file system item '{fileSystemItemPath}' attributes");

            return itemData.Attributes.IsDirectory ? Win32.FileAttribute.Directory : Win32.FileAttribute.File;
        }

        private static bool IsPathToDrive(string path)
        {
            var directoryInfo = new DirectoryInfo(path);
            return directoryInfo.Parent == null && directoryInfo.FullName == directoryInfo.Root.FullName;
        }

        private static Icon GetIconFromHandle(IntPtr iconHandle)
        {
            try
            {
                return (Icon)Icon.FromHandle(iconHandle).Clone(); //TODO: Why we are cloning?
            }
            catch
            {
                return EmptyIcon();
            }
            finally
            {
                Win32.DestroyIcon(iconHandle);
            }
        }

        private static Icon EmptyIcon()
        {
            return Icon.FromHandle(new Bitmap(1, 1).GetHicon());
        }
    }
}
