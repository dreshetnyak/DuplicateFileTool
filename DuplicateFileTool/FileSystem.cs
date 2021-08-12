using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DuplicateFileTool.Annotations;
using Microsoft.Win32.SafeHandles;

namespace DuplicateFileTool
{
    #region File System Error Types

    internal sealed class FileSystemError
    {
        public string Path { get; }
        public string Message { get; }
        public Exception Exception { get; }
        public FileSystemError(string path, string message, Exception exception = null) { Path = path; Message = message; Exception = exception; }
    }

    internal sealed class FileSystemErrorEventArgs : EventArgs
    {
        public FileSystemError FileSystemError { get; }
        public FileSystemErrorEventArgs(string path, string message, Exception exception = null) { FileSystemError = new FileSystemError(path, message, exception); }
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

    #endregion

    /// <summary>
    /// FileHandle is a wrapper around SafeFileHandle it opens it when needed, closes it, contains the information indicating
    /// if the file is open or not and the last access to the stored handle.
    /// </summary>
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

        //TODO implement deleteToRecycleBin
        public static void DeleteFile(string fileFullName, bool deleteToRecycleBin = false)
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
            try { return !new DirectoryEnumeration(path).Any(dirItem => !dirItem.Attributes.IsDirectory || !IsDirectoryTreeEmpty(dirItem.FullName)); }
            catch (Exception) { return false; }
        }

        public static bool DeleteEmptySubDirectories([NotNull] string path, [NotNull] Action<string> directoryToRemoveName, [NotNull] Action<string, string> deletionError)
        {
            if (!DirectoryExists(path))
                return true;

            try
            {
                if (new DirectoryEnumeration(path).Any(dirItem => !dirItem.Attributes.IsDirectory || !DeleteEmptySubDirectories(dirItem.FullName, directoryToRemoveName, deletionError)))
                    return false; //Found a file or deletion failure
            }
            catch (Exception)
            {
                return false;
            }

            //The dir content is empty, now we can delete it
            directoryToRemoveName(path);
            var deletionSuccess = Win32.RemoveDirectory(MakeLongPath(path));
            if (deletionSuccess)
                return true;

            deletionError(path, new Win32Exception(Marshal.GetLastWin32Error()).Message);
            return false;
        }

        //TODO implement deleteToRecycleBin
        public static void DeleteDirectoryTreeWithParents([NotNull] string path, [NotNull] Action<string> writeLog, [NotNull] Action<string, string> deletionError, bool deleteToRecycleBin = false)
        {
            var deletionFailed = !DeleteEmptySubDirectories(path, writeLog, deletionError);
            if (deletionFailed)
                return;

            var dirPath = new DirectoryInfo(path);
            while (dirPath.Parent != null) //While root is not reached
            {
                dirPath = dirPath.Parent;
                if (!DeleteEmptySubDirectories(dirPath.FullName, writeLog, deletionError))
                    return;
            }
        }
        
        public static string MakeLongPath(string path)
        {
            return !path.StartsWith(@"\\?\") ? @"\\?\" + path : path;
        }
    }
}
