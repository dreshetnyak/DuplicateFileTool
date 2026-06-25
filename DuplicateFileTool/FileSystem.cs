using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace DuplicateFileTool;

#region File System Error Types

internal sealed class FileSystemError(string path, string message, Exception? exception = null)
{
    public string Path { get; } = path;
    public string Message { get; } = message;
    public Exception? Exception { get; } = exception;
}

internal sealed class FileSystemErrorEventArgs(string path, string message, Exception? exception = null) : EventArgs
{
    public FileSystemError FileSystemError { get; } = new(path, message, exception);
}
internal delegate void FileSystemErrorEventHandler(object? sender, FileSystemErrorEventArgs eventArgs);

[Localizable(true)]
internal class FileSystemException(string fileFullName, string message) : Exception(message)
{
    public string FileFullName { get; } = fileFullName;
}

/// <summary>Thrown when moving a file to the recycle bin fails; the file itself is still in place.</summary>
internal sealed class RecycleOperationException(string fileFullName, string message) : FileSystemException(fileFullName, message);

#endregion

/// <summary>
/// FileHandle is a wrapper around SafeFileHandle it opens it when needed, closes it, contains the information indicating
/// if the file is open or not and the last access to the stored handle.
/// </summary>
internal sealed class FileHandle : IDisposable
{
    private SafeFileHandle? Handle { get; set; }

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
    private sealed class RedirectDisable : IDisposable
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
    private static RedirectDisable Redirect { get; }

    #endregion

    static FileSystem()
    {
        Redirect = new RedirectDisable();
        Use(Redirect);
    }

    // Making sure that Redirect is not stripped out by the optimization
#pragma warning disable IDE0060 // Remove unused parameter
    [MethodImpl(MethodImplOptions.NoOptimization)]
    // ReSharper disable once UnusedParameter.Local
    private static void Use(object obj)
    { /* do nothing */}
#pragma warning restore IDE0060 // Remove unused parameter

    public static FileHandle OpenRead(string filePath) => 
        OpenFile(filePath, Win32.FileAccess.GenericRead, Win32.FileShare.Read, Win32.CreationDisposition.OpenExisting);

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

#pragma warning disable CA2020
        if (lowOffsetDword != (int)Win32.INVALID_HANDLE_VALUE)
#pragma warning restore CA2020
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
#pragma warning disable CA2020
        if (lowOffsetDword != (int)Win32.INVALID_HANDLE_VALUE)
#pragma warning restore CA2020
            return Data.JoinToLong(highOffsetDword, lowOffsetDword);
            
        var errorCode = Marshal.GetLastWin32Error();
        if (errorCode != Win32.NO_ERROR)
            throw new FileSystemException(fileHandle.FileFullName, new Win32Exception(errorCode).Message);

        return Data.JoinToLong(highOffsetDword, lowOffsetDword);
    }

    public static FileData GetFileSystemItemData(string path)
    {
        var findHandle = Win32.FindFirstFile(MakeLongPath(path), out var foundFileInfo);
        if (findHandle.IsInvalidHandle())
            throw new FileSystemException(path, new Win32Exception(Marshal.GetLastWin32Error()).Message);

        var closeSuccess = Win32.FindClose(findHandle);
        if (!closeSuccess)
            throw new FileSystemException(path, new Win32Exception(Marshal.GetLastWin32Error()).Message);

        if (new FileAttributes((uint)foundFileInfo.dwFileAttributes).IsArchive)
            path = path.SubstringBeforeLast('\\');

        return new FileData(path, foundFileInfo);
    }

    /// <summary>
    /// Reads a single file's <see cref="FileData"/> (attributes, size, times) via <c>FindFirstFile</c> on the
    /// long-path form, mirroring how <see cref="DirectoryEnumeration"/> builds its items. Used by the deletion
    /// run's non-duplicate pass, which has only path→size in the set and needs the real attributes for readonly
    /// handling, recycling and long-path deletion. Returns <see cref="FileData.Empty"/> when the file is gone or
    /// inaccessible so the caller can skip it gracefully (a reparse-point FILE is a normal deletion target and is
    /// returned like any other file).
    /// </summary>
    public static FileData GetFileData(string path)
    {
        var findHandle = Win32.FindFirstFile(MakeLongPath(path), out var findData);
        if (findHandle.IsInvalidHandle())
            return FileData.Empty;

        Win32.FindClose(findHandle);
        return new FileData(Path.GetDirectoryName(path) ?? path, findData);
    }

    public enum RecycleCheck { Ok, PathTooLong, NoRecycleBin }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, bool> RemoteRootCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Tells whether a file can be moved to the recycle bin before attempting the operation.
    /// SHFileOperation is limited to MAX_PATH (it does not understand the long-path prefix) and
    /// network locations have no recycle bin (the shell would delete the file permanently without any indication).</summary>
    public static RecycleCheck CanRecycle(string path)
    {
        if (path.Length >= Win32.MAX_PATH)
            return RecycleCheck.PathTooLong;

        if (path.StartsWith(@"\\"))
            return RecycleCheck.NoRecycleBin; //UNC path

        var root = Path.GetPathRoot(path);
        if (string.IsNullOrEmpty(root))
            return RecycleCheck.Ok;

        var isRemote = RemoteRootCache.GetOrAdd(root, rootPath => Win32.GetDriveType(rootPath) == Win32.DRIVE_REMOTE);
        return isRemote ? RecycleCheck.NoRecycleBin : RecycleCheck.Ok;
    }

    public static void DeleteFile(FileData fileData, bool deleteToRecycleBin = false)
    {
        if (fileData.Attributes.IsReadonly)
            RemoveFileReadonlyAttribute(fileData);

        if (deleteToRecycleBin)
        {
            DeleteFileToRecycleBin(fileData.FullName);
            return;
        }

        if (!Win32.DeleteFile(MakeLongPath(fileData.FullName)))
            throw new FileSystemException(fileData.FullName, new Win32Exception(Marshal.GetLastWin32Error()).Message);
    }

    private static void DeleteFileToRecycleBin(string fileFullName)
    {
        var fileOperation = new Win32.SHFILEOPSTRUCT
        {
            wFunc = Win32.FO_DELETE,
            pFrom = fileFullName + '\0', //The marshaler appends the second terminator of the double-null-terminated list
            fFlags = Win32.FOF_ALLOWUNDO | Win32.FOF_NOCONFIRMATION | Win32.FOF_SILENT | Win32.FOF_NOERRORUI
        };

        var result = Win32.SHFileOperation(ref fileOperation);
        if (result != 0)
            throw new RecycleOperationException(fileFullName, GetRecycleErrorMessage(result));
        if (fileOperation.fAnyOperationsAborted)
            throw new RecycleOperationException(fileFullName, Properties.Resources.Error_Recycle_Aborted);
    }

    private static string GetRecycleErrorMessage(int shellOperationResult)
    {
        // SHFileOperation reports legacy DE_* codes; map them to the closest Win32 error so the
        // message text comes from the OS, localized, like every other error in the application.
        var win32ErrorCode = shellOperationResult switch
        {
            0x74 => 123,           //DE_ROOTDIR -> ERROR_INVALID_NAME
            0x76 => 16,            //DE_DELCURDIR -> ERROR_CURRENT_DIRECTORY
            0x78 => 5,             //DE_ACCESSDENIEDSRC -> ERROR_ACCESS_DENIED
            0x79 or 0xB7 => 206,   //DE_PATHTOODEEP, DE_ERROR_MAX -> ERROR_FILENAME_EXCED_RANGE
            0x7C => 161,           //DE_INVALIDFILES -> ERROR_BAD_PATHNAME
            0x86 or 0x87 or 0x88 => 19, //DE_SRC_IS_CDROM/DVD/CDRECORD -> ERROR_WRITE_PROTECT
            > 0 and < 0x71 => shellOperationResult, //Below the DE_* range SHFileOperation returns standard Win32 error codes
            _ => 0
        };

        return win32ErrorCode != 0
            ? new Win32Exception(win32ErrorCode).Message
            : string.Format(Properties.Resources.Error_Recycle_Failed, shellOperationResult);
    }

    public static bool RemoveFileReadonlyAttribute(FileData fileData)
    {
        var fileAttributes = GetFileAttributes(fileData.FullName);
        return fileAttributes != null && Win32.SetFileAttributes(fileData.FullName, fileAttributes);
    }

    public static FileAttributes? GetFileAttributes(string fileFullName)
    {
        var fileAttributesInt = Win32.GetFileAttributes(fileFullName);
        return fileAttributesInt != Win32.INVALID_FILE_ATTRIBUTES
            ? new FileAttributes(fileAttributesInt) { IsReadonly = false }
            : null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool PathExists(string path) => 
        Win32.GetFileAttributes(MakeLongPath(path)) != Win32.INVALID_FILE_ATTRIBUTES;

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
        // Junction safety (story 32, deletion side): a reparse point (junction/symlink) is checked FIRST so a tree
        // that contains one is reported NON-empty and the reparse entry is never passed to the recursive call, so
        // empty-dir detection never traverses behind a junction.
        try { return !new DirectoryEnumeration(path).Any(dirItem => dirItem.Attributes.IsReparsePoint || !dirItem.Attributes.IsDirectory || !IsDirectoryTreeEmpty(dirItem.FullName)); }
        catch (Exception) { return false; }
    }

    public static bool DeleteEmptySubDirectories(string path, Action<string> directoryToRemoveName, Action<string, string> deletionError)
    {
        if (!DirectoryExists(path))
            return true;

        try
        {
            // Junction safety (story 32, deletion side): a reparse point (junction/symlink) is checked FIRST so a
            // directory containing one is reported non-empty (returns false, not deleted) and the reparse entry is
            // never passed to the recursive call, so deletion never traverses behind a junction.
            if (new DirectoryEnumeration(path).Any(dirItem => dirItem.Attributes.IsReparsePoint || !dirItem.Attributes.IsDirectory || !DeleteEmptySubDirectories(dirItem.FullName, directoryToRemoveName, deletionError)))
                return false; //Found a file, a reparse point, or a deletion failure
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

    // Empty directories are always removed permanently, even when files go to the recycle bin: they
    // carry no data to restore, and recycling each one would only clutter the bin (Explorer recreates
    // missing directories when restoring a file anyway).
    public static void DeleteDirectoryTreeWithParents(string path, Action<string> writeLog, Action<string, string> deletionError)
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
        
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string MakeLongPath(string path) => 
        !path.StartsWith(@"\\?\") ? @"\\?\" + path : path;
}