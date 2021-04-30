using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace FileBadger
{
    internal sealed class FileSystemErrorEventArgs : EventArgs
    {
        public string Path { get; }
        public string Message { get; }
        public Exception Exception { get; }
        public FileSystemErrorEventArgs(string path, string message, Exception exception = null) { Path = path; Message = message; Exception = exception; }
    }
    internal delegate void FileSystemErrorEventHandler(object sender, FileSystemErrorEventArgs eventArgs);

    [Localizable(true)]
    internal sealed class FileSystemException : Exception
    {
        public string FileFullName { get; }

        public FileSystemException(string fileFullName, string message) : base(message)
        {
            FileFullName = fileFullName;
        }
    }

    internal class FileHandle : IDisposable
    {
        private SafeFileHandle Handle { get; set; }

        public string FileFullName { get; }
        public DateTime LastAccessTime { get; private set; }

        public bool IsOpen => Handle != null;

        public FileHandle(string fileFullName)
        {
            FileFullName = fileFullName;
        }

        public FileHandle(SafeFileHandle fileHandle, string fileFullName)
        {
            FileFullName = fileFullName;
            Handle = fileHandle;
        }

        public static implicit operator SafeFileHandle(FileHandle fileHandle) => fileHandle.Get();

        public SafeFileHandle Get()
        {
            LastAccessTime = DateTime.UtcNow;

            if (Handle != null)
                return Handle;

            if (!Win32.PathFileExists(FileFullName))
                throw new FileSystemException(FileFullName, Properties.Resources.Error_File_not_found);

            Handle = FileSystem.OpenRead(FileFullName);

            return Handle;
        }

        public void Dispose()
        {
            LastAccessTime = default;

            if (Handle == null)
                return;

            Handle.Dispose();
            Handle = null;
        }
    }

    internal static class FileSystem
    {
        #region Disable File System Redirect
        //File system redirection is present in Wow64, here we disable it to get the straight forward access to the files and avoid errors.
        private class RedirectDisable : IDisposable
        {
            private IntPtr Wow64Value { get; set; }

            public RedirectDisable()
            {
                var wow64Value = IntPtr.Zero;
                Wow64Value = Win32.Wow64DisableWow64FsRedirection(ref wow64Value)
                    ? wow64Value
                    : IntPtr.Zero;
            }

            public void Dispose()
            {
                if (Wow64Value.IsInvalidHandle())
                    return;
                Win32.Wow64RevertWow64FsRedirection(Wow64Value);
                Wow64Value = IntPtr.Zero;
            }
        }

        // ReSharper disable once UnusedMember.Local
        private static readonly RedirectDisable Redirect = new RedirectDisable();

        #endregion

        /// <summary>
        /// FileHandle is a wrapper around SafeFileHandle it opens it when needed, closes it, contains the information indicating
        /// if the file is open or not and the last access to the stored handle.
        /// </summary>

        public static FileHandle OpenRead(string filePath)
        {
            return OpenFile(filePath, Win32.FileAccess.GenericRead, Win32.FileShare.Read, Win32.CreationDisposition.OpenExisting);
        }

        public static FileHandle OpenFile(string filePath, Win32.FileAccess fileAccess, Win32.FileShare fileShare, Win32.CreationDisposition disposition)
        {
            var fileHandle = Win32.CreateFile(
                !filePath.StartsWith(@"\\?\") ? @"\\?\" + filePath : filePath, //If the path does not start from this special sequence then we add it. The sequence allow to exceed MAX_PATH
                fileAccess,
                fileShare,
                null,
                disposition,
                0,
                IntPtr.Zero);

            if (fileHandle.IsInvalid)
                throw new FileSystemException(filePath, new Win32Exception(Marshal.GetLastWin32Error()).Message);

            return new FileHandle(fileHandle, filePath);
        }

        public static unsafe int ReadFile(FileHandle fileHandle, byte[] bufferToReceiveData)
        {
            int bytesRead;
            bool readSuccess;

            fixed (byte* pointerToBufferForReading = bufferToReceiveData)
                readSuccess = Win32.ReadFile(fileHandle, pointerToBufferForReading, bufferToReceiveData.Length, out bytesRead, IntPtr.Zero) != 0;

            if (!readSuccess)
                throw new FileSystemException(fileHandle.FileFullName, new Win32Exception(Marshal.GetLastWin32Error()).Message);

            return bytesRead;
        }

        public static unsafe bool SetFilePointer(FileHandle fileHandle, long offset)
        {
            var lowOffsetDword = (int)offset;
            var highOffsetDword = (int)(offset >> 32);

            lowOffsetDword = Win32.SetFilePointer(fileHandle, lowOffsetDword, &highOffsetDword, (int)SeekOrigin.Begin);

            if (lowOffsetDword != (int)Win32.INVALID_HANDLE_VALUE)
                return true;

            var errorCode = Marshal.GetLastWin32Error();
            if (errorCode != Win32.NO_ERROR)
                throw new FileSystemException(fileHandle.FileFullName, new Win32Exception(errorCode).Message);
            
            return true;
        }

        public static unsafe long GetFilePointer(FileHandle fileHandle)
        {
            int lowOffsetDword = 0, highOffsetDword = 0;

            lowOffsetDword = Win32.SetFilePointer(fileHandle, lowOffsetDword, &highOffsetDword, (int)SeekOrigin.Current);
            if (lowOffsetDword != (int) Win32.INVALID_HANDLE_VALUE)
                return Data.JoinToLong(highOffsetDword, lowOffsetDword);
            
            var errorCode = Marshal.GetLastWin32Error();
            if (errorCode != Win32.NO_ERROR)
                throw new FileSystemException(fileHandle.FileFullName, new Win32Exception(errorCode).Message);

            return Data.JoinToLong(highOffsetDword, lowOffsetDword);
        }

        public static FileData GetFileSystemItemData(string path)
        {
            var adaptedPath = !path.StartsWith(@"\\?\") ? @"\\?\" + path : path;

            var findHandle = Win32.FindFirstFile(adaptedPath, out var foundFileInfo);
            if (findHandle.IsInvalidHandle())
                throw new FileSystemException(path, new Win32Exception(Marshal.GetLastWin32Error()).Message);

            var closeSuccess = Win32.FindClose(findHandle);
            if (!closeSuccess)
                throw new FileSystemException(path, new Win32Exception(Marshal.GetLastWin32Error()).Message);

            return new FileData(path, foundFileInfo);
        }

        public static void DeleteFile(string fileFullName)
        {
            if (!Win32.DeleteFile(fileFullName))
                throw new FileSystemException(fileFullName, new Win32Exception(Marshal.GetLastWin32Error()).Message);
        }

        public struct DriveInfo
        {
            public string DriveLetter { get; set; }
            public uint PhysicalDriveNumber { get; set; }   //Equals to uint.MaxValue if the value is undetermined
        }

        public static IEnumerable<DriveInfo> GetDrivesInfo()
        {
            var volumeDiskExtents = new Win32.VOLUME_DISK_EXTENTS();
            var outBufferSize = (uint)Marshal.SizeOf(volumeDiskExtents);
            var outBuffer = IntPtr.Zero;

            try
            {
                outBuffer = Marshal.AllocHGlobal((int)outBufferSize);
                foreach (var logicalDrive in Environment.GetLogicalDrives()) // C:\, D:\, etc.
                {
                    using (var driveHandle = Win32.CreateFile($"\\\\.\\{logicalDrive[0]}:", Win32.FileAccess.None, Win32.FileShare.Read | Win32.FileShare.Write, null, Win32.CreationDisposition.OpenExisting, 0, IntPtr.Zero))
                    {
                        if (driveHandle.IsInvalid)
                        {
                            yield return new DriveInfo { DriveLetter = logicalDrive, PhysicalDriveNumber = uint.MaxValue };
                            continue;
                        }

                        if (!Win32.DeviceIoControl(driveHandle, Win32.IOCTL_VOLUME_GET_VOLUME_DISK_EXTENTS, IntPtr.Zero, 0, outBuffer, outBufferSize, out _, IntPtr.Zero))
                        {
                            yield return new DriveInfo { DriveLetter = logicalDrive, PhysicalDriveNumber = uint.MaxValue };
                            continue;
                        }

                        Marshal.PtrToStructure(outBuffer, volumeDiskExtents);
                        yield return new DriveInfo { DriveLetter = logicalDrive, PhysicalDriveNumber = volumeDiskExtents.Extents.DiskNumber };
                    }
                }
            }
            finally
            {
                Marshal.FreeHGlobal(outBuffer);
            }
        }
    }
}
