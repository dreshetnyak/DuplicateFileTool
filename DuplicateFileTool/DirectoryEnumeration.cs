using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;
using DuplicateFileTool.Properties;

namespace DuplicateFileTool
{
    public class DirectoryAccessFailedException : Exception
    {
        public string DirectoryPath { get; }

        public DirectoryAccessFailedException(string directoryPath, string message) : base(message)
        {
            DirectoryPath = directoryPath;
        }
    }

    internal class DirectoryEnumeration : IEnumerable<FileData>
    {
        private string DirPath { get; }
        public DirectoryEnumeration(string dirPath) { DirPath = dirPath; }

        public IEnumerator<FileData> GetEnumerator() { return new DirectoryEnumerator(DirPath); }

        IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }
    }

    internal class DirectoryEnumerator : IEnumerator<FileData>
    {
        private IntPtr FindHandle { get; set; } = IntPtr.Zero;
        private string SearchPath { get; }
        private FileData CurrentFileData { get; set; }
        private bool IsNoMoreFiles { get; set; }

        public DirectoryEnumerator(string searchPath)
        {
            SearchPath = searchPath;
            IsNoMoreFiles = false;
        }

        public void Dispose()
        {
            CloseFindHandle();
        }

        [Localizable(true)]
        private void CloseFindHandle()
        {
            if (FindHandle != IntPtr.Zero && FindHandle != Win32.INVALID_HANDLE_VALUE && !Win32.FindClose(FindHandle))
                throw new FileSystemException(SearchPath, Resources.Error_Failed_to_close_the_file_search_handle + Marshal.GetLastWin32Error().GetExceptionMessageForCulture(Thread.CurrentThread.CurrentCulture));
        }

        public bool MoveNext()
        {
            do
            {
                if (IsNoMoreFiles)
                    return false;

                Win32.WIN32_FIND_DATA findData;
                IsNoMoreFiles = FindHandle == IntPtr.Zero
                    ? !MoveToFirstFile(out findData)
                    : !MoveToNextFile(out findData);

                if (IsNoMoreFiles)
                    return false;

                var fileName = findData.cFileName;
                if (fileName is "." or "..")
                    continue;

                CurrentFileData = new FileData(SearchPath, findData);
                return true;

            } while (true);
        }

        private bool MoveToFirstFile(out Win32.WIN32_FIND_DATA findData)
        {
            FindHandle = Win32.FindFirstFile(GetPathAdaptedForSearch(SearchPath), out findData);
            if (FindHandle.IsValidHandle())
                return true;

            var errorCode = Marshal.GetLastWin32Error();
            if (errorCode == Win32.ERROR_NO_MORE_FILES)
                return false;

            throw new FileSystemException(SearchPath, errorCode.GetExceptionMessageForCulture(Thread.CurrentThread.CurrentCulture));
        }

        private bool MoveToNextFile(out Win32.WIN32_FIND_DATA findData)
        {
            if (Win32.FindNextFile(FindHandle, out findData))
                return true;

            var errorCode = Marshal.GetLastWin32Error();
            if (errorCode == Win32.ERROR_NO_MORE_FILES)
                return false;

            throw new FileSystemException(SearchPath, Resources.Error_Failed_to_find_the_next_file + errorCode.GetExceptionMessageForCulture(Thread.CurrentThread.CurrentCulture));
        }

        private static string GetPathAdaptedForSearch(string path)
        {
            var adaptedPath = FileSystem.MakeLongPath(path);
            return adaptedPath.EndsWith("\\") ? adaptedPath + '*' : adaptedPath + "\\*";
        }

        public void Reset()
        {
            IsNoMoreFiles = false;
            CurrentFileData = null;
            FindHandle = IntPtr.Zero;
            CloseFindHandle();
        }

        public FileData Current => CurrentFileData;

        object IEnumerator.Current => Current;
    }
}
