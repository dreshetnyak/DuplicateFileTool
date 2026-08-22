using DuplicateFileTool.Properties;

namespace DuplicateFileTool;

/// <summary>
/// The FileReader class is a wrapper for a single file handle. It provides functionality for reading from a file 
/// in a sequential and thread-safe manner. Additionally, it implements a caching mechanism for open file handles. 
/// The cache size is limited; if the limit is reached and a new handle is needed, the least recently accessed file handle 
/// will be closed. This approach ensures efficient use of the limited file handle resources by automatically managing 
/// the opening and closing of files.
/// </summary>
internal sealed class FileReader(string fileFullName) : IDisposable
{
    public static int MaxFileHandlesCount { get; set; } = 255; //512 is the limit set by Windows
    private static object OpenFilesCacheLock { get; } = new();
    private static List<FileHandle> OpenFilesCache { get; } = [];
    private static HashSet<FileHandle> OpenFilesInUse { get; } = [];

    private object OperationLock { get; } = new();
    private FileHandle File { get; } = new(fileFullName);
    private long Offset { get; set; }
    private bool IsDisposed { get; set; }

    public void Dispose()
    {
        lock (OperationLock)
        {
            if (IsDisposed)
                return;

            lock (OpenFilesCacheLock)
            {
                OpenFilesCache.Remove(File);
                File.Dispose();
                IsDisposed = true;

                Monitor.PulseAll(OpenFilesCacheLock);
            }
        }
    }

    public int ReadNext(byte[] bufferToReceiveData)
    {
        lock (OperationLock)
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            AcquireFileHandle();

            try
            {
                var bytesRead = FileSystem.ReadFile(File, bufferToReceiveData);
                if (bytesRead > 0)
                    Offset += bytesRead;
                return bytesRead;
            }
            finally
            {
                ReleaseFileHandle();
            }
        }
    }

    private void AcquireFileHandle()
    {
        lock (OpenFilesCacheLock)
        {
            while (!OpenFilesCache.Contains(File))
            {
                if (OpenFilesCache.Count < Math.Max(1, MaxFileHandlesCount))
                {
                    OpenFileHandle(File, Offset);
                    break;
                }

                if (!FreeOneHandle())
                {
                    // Every cached handle is currently reading. Wait for one to become evictable
                    // instead of exceeding the configured handle limit or closing an active handle.
                    Monitor.Wait(OpenFilesCacheLock);
                }
            }

            OpenFilesInUse.Add(File);
        }
    }

    private void ReleaseFileHandle()
    {
        lock (OpenFilesCacheLock)
        {
            OpenFilesInUse.Remove(File);
            Monitor.PulseAll(OpenFilesCacheLock);
        }
    }

    private static void OpenFileHandle(FileHandle file, long offset)
    {
        try
        {
            if (!FileSystem.SetFilePointer(file, offset))
                throw new FileSystemException(file.FileFullName, Resources.Error_Unable_to_set_the_file_offset);

            OpenFilesCache.Add(file);
        }
        catch
        {
            file.Dispose();
            throw;
        }
    }

    private static bool FreeOneHandle()
    {
        var indexToRemove = -1;
        var oldestDate = DateTime.MaxValue;
        FileHandle? oldestFileHandle = null;

        for (var index = 0; index < OpenFilesCache.Count; index++)
        {
            var file = OpenFilesCache[index];
            if (OpenFilesInUse.Contains(file))
                continue;

            var lastAccessTime = file.LastAccessTime;
            if (lastAccessTime >= oldestDate)
                continue;

            oldestFileHandle = file;
            oldestDate = lastAccessTime;
            indexToRemove = index;
        }

        if (indexToRemove == -1)
            return false;

#pragma warning disable S2589
        oldestFileHandle?.Dispose();
#pragma warning restore S2589
        OpenFilesCache.RemoveAt(indexToRemove);
        return true;
    }

    public bool SetFilePointer(long offset)
    {
        lock (OperationLock)
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            AcquireFileHandle();

            try
            {
                Offset = offset;
                return FileSystem.SetFilePointer(File, offset);
            }
            finally
            {
                ReleaseFileHandle();
            }
        }
    }
}
