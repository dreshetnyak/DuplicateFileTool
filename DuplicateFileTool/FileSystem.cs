﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace DuplicateFileTool
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

            if (!FileSystem.PathExists(FileFullName))
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
                MakeLongPath(filePath), //If the path does not start from this special sequence then we add it. The sequence allow to exceed MAX_PATH
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

            if (new FileAttributes((uint)foundFileInfo.dwFileAttributes).IsArchive)
                path = path.SubstringBeforeLast('\\');

            return new FileData(path, foundFileInfo);
        }

        public static void DeleteFile(string fileFullName)
        {
            if (!Win32.DeleteFile(fileFullName))
                throw new FileSystemException(fileFullName, new Win32Exception(Marshal.GetLastWin32Error()).Message);
        }

        public static void DeleteFile(FileData fileData)
        {
            if (fileData.Attributes.IsReadonly)
                RemoveFileReadonlyAttribute(fileData);

            if (Win32.DeleteFile(fileData.FullName))
                return;

            throw new FileSystemException(fileData.FullName, new Win32Exception(Marshal.GetLastWin32Error()).Message);
        }

        public static bool RemoveFileReadonlyAttribute(FileData fileData)
        {
            var fileAttributes = GetFileAttributes(fileData.FullName);
            return fileAttributes != null && Win32.SetFileAttributes(fileData.FullName, fileAttributes);
        }

        public static FileAttributes GetFileAttributes(string fileFullName)
        {
            var fileAttributesInt = Win32.GetFileAttributes(fileFullName);
            return fileAttributesInt != Win32.INVALID_FILE_ATTRIBUTES
                ? new FileAttributes(fileAttributesInt) { IsReadonly = false }
                : null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool PathExists(string path)
        {
            return Win32.GetFileAttributes(path) != Win32.INVALID_FILE_ATTRIBUTES;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool DirectoryExists(string path)
        {
            var attributes = Win32.GetFileAttributes(path);
            return attributes != Win32.INVALID_FILE_ATTRIBUTES && new FileAttributes(attributes).IsDirectory;
        }

        public static bool IsDirectoryTreeEmpty(string path)
        {
            if (!DirectoryExists(path))
                return true;

            try
            {
                foreach (var dirItem in new DirectoryEnumeration(path))
                {
                    if (dirItem.Attributes.IsDirectory && IsDirectoryTreeEmpty(dirItem.FullName))
                        continue; //If it is a directory and it is empty

                    //If it is a file
                    return false;
                }
            }
            catch (Exception)
            {
                return false;
            }

            return true;
        }

        //TODO
        public static bool DeleteDirectoryTree(string path, Action<string> log)
        {
            if (!DirectoryExists(path))
                return true;

            try
            {
                foreach (var dirItem in new DirectoryEnumeration(path))
                {
                    if (dirItem.Attributes.IsDirectory && DeleteDirectoryTree(dirItem.FullName, log))
                        continue; //If it is a directory and we successfully deleted it

                    //If it is a file or deletion failed
                    return false;
                }
            }
            catch (Exception)
            {
                return false;
            }

            //The dir content is empty, now we can delete it
            log?.Invoke(path);
            return Win32.RemoveDirectory(MakeLongPath(path));
        }

        public static void DeleteDirectoryTreeWithParents(string path, Action<string> log)
        {
            var deletionFailed = !DeleteDirectoryTree(path, log);
            if (deletionFailed)
                return;

            var dirPath = new DirectoryInfo(path);
            while (dirPath.Parent != null) //While root is not reached
            {
                dirPath = dirPath.Parent;
                if (!DeleteDirectoryTree(dirPath.FullName, log))
                    return;
            }
        }

        #region Drives Information

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

        #endregion

        public static string MakeLongPath(string path)
        {
            return !path.StartsWith(@"\\?\") ? @"\\?\" + path : path;
        }
    }
}
