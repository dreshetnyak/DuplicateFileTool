using System;
using System.Collections.Generic;
using System.Threading;
using DuplicateFileTool.Properties;

namespace DuplicateFileTool
{
    /// <summary>
    /// FileReader class is wrapper over a single file handle, it implements functionality for reading from the file in a
    /// sequential and thread safe manner. Also it caches handles of the open files, the cache size is limited, if we reach
    /// the limit and a new handle is needed the file that was accessed before others will be closed. This is done in a such
    /// way because the file handles is a limited resource and here we ration it and automatically open/close it.
    /// </summary>
    internal class FileReader : IDisposable
    {
        public static int MaxFileHandlesCount { get; set; } = 255; //512 is the limit set by Windows
        private static ReaderWriterLockSlim OpenFilesCacheLock { get; } = new();
        private static List<FileHandle> OpenFilesCache { get; } = new();

        private FileHandle File { get; }
        private long Offset { get; set; }

        public FileReader(string fileFullName)
        {
            File = new FileHandle(fileFullName);
        }

        public void Dispose()
        {
            try
            {
                OpenFilesCacheLock.EnterWriteLock();

                var fileCacheIndex = OpenFilesCache.IndexOf(File);
                if (fileCacheIndex != -1)
                    OpenFilesCache.RemoveAt(fileCacheIndex);

                File?.Dispose();
            }
            finally
            {
                OpenFilesCacheLock.ExitWriteLock();
            }
        }

        public int ReadNext(byte[] bufferToReceiveData)
        {
            try
            {
                OpenFilesCacheLock.EnterUpgradeableReadLock();

                if (!OpenFilesCache.Contains(File))
                    OpenFileHandle(File, Offset);

                var bytesRead = FileSystem.ReadFile(File, bufferToReceiveData);
                if (bytesRead > 0)
                    Offset += bytesRead;
                return bytesRead;
            }
            finally
            {
                OpenFilesCacheLock.ExitUpgradeableReadLock();
            }
        }

        private static void OpenFileHandle(FileHandle file, long offset)
        {
            try
            {
                OpenFilesCacheLock.EnterWriteLock();

                if (OpenFilesCache.Count >= MaxFileHandlesCount)
                    FreeOneHandle();

                OpenFilesCache.Add(file);

                if (!FileSystem.SetFilePointer(file, offset))
                    throw new FileSystemException(file.FileFullName, Resources.Error_Unable_to_set_the_file_offset);
            }
            finally
            {
                OpenFilesCacheLock.ExitWriteLock();
            }
        }

        private static void FreeOneHandle()
        {
            var indexToRemove = -1;
            var oldestDate = DateTime.MaxValue;
            FileHandle oldestFileHandle = null;

            for (var index = 0; index < OpenFilesCache.Count; index++)
            {
                var file = OpenFilesCache[index];
                var lastAccessTime = file.LastAccessTime;
                if (lastAccessTime >= oldestDate)
                    continue;

                oldestFileHandle = file;
                oldestDate = lastAccessTime;
                indexToRemove = index;
            }

            if (indexToRemove == -1)
                return;

            oldestFileHandle?.Dispose();
            OpenFilesCache.RemoveAt(indexToRemove);
        }

        public bool SetFilePointer(long offset)
        {
            Offset = offset;
            return FileSystem.SetFilePointer(File, offset);
        }
    }
}
