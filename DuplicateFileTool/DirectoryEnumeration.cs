﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
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
                throw new ApplicationException(Resources.Error_Failed_to_close_the_file_search_handle + new Win32Exception(Marshal.GetLastWin32Error()).Message);
        }

        public bool MoveNext()
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
            if (fileName == "." || fileName == "..")
                return MoveNext();

            CurrentFileData = new FileData(SearchPath, findData);
            return true;
        }

        private bool MoveToFirstFile(out Win32.WIN32_FIND_DATA findData)
        {
            FindHandle = Win32.FindFirstFile(GetPathAdaptedForSearch(SearchPath), out findData);
            if (FindHandle.IsValidHandle())
                return true;

            var errCode = Marshal.GetLastWin32Error(); 
            if (errCode == Win32.ERROR_NO_MORE_FILES)
                return false;

            throw new FileSystemException(SearchPath, new Win32Exception(errCode).Message);
        }

        private bool MoveToNextFile(out Win32.WIN32_FIND_DATA findData)
        {
            if (Win32.FindNextFile(FindHandle, out findData))
                return true;

            var errorCode = Marshal.GetLastWin32Error();
            if (errorCode == Win32.ERROR_NO_MORE_FILES)
                return false;

            throw new ApplicationException(Resources.Error_Failed_to_find_the_next_file + new Win32Exception(errorCode).Message);
        }

        private static string GetPathAdaptedForSearch(string path)
        {
            var adaptedPath = !path.StartsWith(@"\\?\") ? @"\\?\" + path : path;
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
