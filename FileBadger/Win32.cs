﻿using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace FileBadger
{
    internal class Win32
    {
        #region Types Definitions
        // ReSharper disable InconsistentNaming
        internal static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);
        internal static readonly int NO_ERROR = 0;
        internal static readonly int ERROR_ACCESS_DENIED = 5;
        internal static readonly int ERROR_NO_MORE_FILES = 18;
        internal static readonly int FILE_ATTRIBUTE_DIRECTORY = 0x00000010;
        internal const int MAX_PATH = 260;
        internal const uint IOCTL_VOLUME_GET_VOLUME_DISK_EXTENTS = 0x00560000;
        // ReSharper enable InconsistentNaming

        [StructLayout(LayoutKind.Sequential)]
        // ReSharper disable once IdentifierTypo
        public struct FILETIME
        {
            internal uint dwLowDateTime;
            internal uint dwHighDateTime;
        };

        // ReSharper disable once InconsistentNaming
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct WIN32_FIND_DATA
        {
            internal FileAttributes dwFileAttributes;
            internal FILETIME ftCreationTime;
            internal FILETIME ftLastAccessTime;
            internal FILETIME ftLastWriteTime;
            internal int nFileSizeHigh;
            internal int nFileSizeLow;
            internal int dwReserved0;
            internal int dwReserved1;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MAX_PATH)]
            internal string cFileName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
            internal string cAlternate;
        }

        [Flags]
        public enum FileAccess : uint
        {
            None = 0x00000000,
            GenericRead = 0x80000000,
            GenericWrite = 0x40000000,
            GenericExecute = 0x20000000,
            GenericAll = 0x10000000,
        }

        [Flags]
        public enum FileShare : uint
        {
            None = 0x00000000,
            Read = 0x00000001,
            Write = 0x00000002,
            Delete = 0x00000004,
        }

        public enum CreationDisposition : uint
        {
            New = 1,
            CreateAlways = 2,
            OpenExisting = 3,
            OpenAlways = 4,
            TruncateExisting = 5,
        }

        [Flags]
        public enum FileAttributes : uint
        {
            Readonly = 0x00000001,
            Hidden = 0x00000002,
            System = 0x00000004,
            Directory = 0x00000010,
            Archive = 0x00000020,
            Device = 0x00000040,
            Normal = 0x00000080,
            Temporary = 0x00000100,
            SparseFile = 0x00000200,
            ReparsePoint = 0x00000400,
            Compressed = 0x00000800,
            Offline = 0x00001000,
            NotContentIndexed = 0x00002000,
            Encrypted = 0x00004000,
            Write_Through = 0x80000000,
            Overlapped = 0x40000000,
            NoBuffering = 0x20000000,
            RandomAccess = 0x10000000,
            SequentialScan = 0x08000000,
            DeleteOnClose = 0x04000000,
            BackupSemantics = 0x02000000,
            PosixSemantics = 0x01000000,
            OpenReparsePoint = 0x00200000,
            OpenNoRecall = 0x00100000,
            FirstPipeInstance = 0x00080000
        }

        [StructLayout(LayoutKind.Sequential)]
        internal class SECURITY_ATTRIBUTES
        {
            internal int nLength = 0;
            internal unsafe byte* pSecurityDescriptor = null;
            internal int bInheritHandle = 0;
        }

        public enum FINDEX_INFO_LEVELS : uint
        {
            FindExInfoStandard = 0,
            FindExInfoBasic = 1
        }

        public enum FINDEX_SEARCH_OPS : uint
        {
            FindExSearchNameMatch = 0,
            FindExSearchLimitToDirectories = 1,
            FindExSearchLimitToDevices = 2
        }

        [StructLayout(LayoutKind.Sequential)]
        internal class DISK_EXTENT
        {
            public uint DiskNumber;
            public long StartingOffset;
            public long ExtentLength;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal class VOLUME_DISK_EXTENTS
        {
            public uint NumberOfDiskExtents;
            public DISK_EXTENT Extents;
        }

        #endregion

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool DeleteFile(string lpFileName);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool RemoveDirectory(string lpFileName);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto, BestFitMapping = false)]
        internal static extern int GetFullPathName(string path, int numBufferChars, [Out]StringBuilder buffer, IntPtr mustBeZero);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto, BestFitMapping = false)]
        internal static extern SafeFileHandle CreateFile(string lpFileName, FileAccess dwDesiredAccess, FileShare dwShareMode, SECURITY_ATTRIBUTES securityAttrs, CreationDisposition dwCreationDisposition, int dwFlagsAndAttributes, IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern IntPtr FindFirstFile(string lpFileName, out WIN32_FIND_DATA lpFindFileData);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern bool FindNextFile(IntPtr hFindFile, out WIN32_FIND_DATA lpFindFileData);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool FindClose(IntPtr hFindFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool Wow64DisableWow64FsRedirection(ref IntPtr ptr);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool Wow64RevertWow64FsRedirection(IntPtr ptr);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern unsafe int ReadFile(SafeFileHandle handle, byte* bytes, int numBytesToRead, out int numBytesRead, IntPtr mustBeZero);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern unsafe int SetFilePointer(SafeFileHandle handle, int lo, int* hi, int origin);

        [DllImport("shlwapi.dll", EntryPoint = "PathFileExistsW", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool PathFileExists([MarshalAs(UnmanagedType.LPTStr)]string pszPath);

        [DllImport("gdi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        internal static extern bool DeleteObject(IntPtr hObject);

        [DllImport("Kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [ResourceExposure(ResourceScope.Machine)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool DeviceIoControl(SafeFileHandle fileHandle, uint ioControlCode, IntPtr inBuffer, uint cbInBuffer, IntPtr outBuffer, uint cbOutBuffer, out uint cbBytesReturned, IntPtr overlapped);
        
        #region File Tree Related

        public enum FileAttribute : uint
        {
            Directory = 16,
            File = 256
        }

        [Flags]
        public enum ShellAttribute : uint
        {
            LargeIcon = 0x000000000,
            SmallIcon = 0x000000001,
            OpenIcon = 0x000000002,
            ShellIconSize = 0x000000004,
            Pidl = 0x000000008,
            UseFileAttributes = 0x000000010,
            AddOverlays = 0x000000020,
            OverlayIndex = 0x000000040,
            Others = 128, // Not defined
            Icon = 0x000000100,
            DisplayName = 0x000000200,
            TypeName = 0x000000400,
            Attributes = 0x000000800,
            IconLocation = 0x000001000,
            ExeType = 0x000002000,
            SystemIconIndex = 0x000004000,
            LinkOverlay = 0x000008000,
            Selected = 0x000010000,
            AttributeSpecified = 0x000020000
        }

        public enum IconSize : short
        {
            Small,
            Large
        }

        public enum ItemState : short
        {
            Undefined,
            Open,
            Close
        }

        public enum ItemType
        {
            Drive,
            Folder,
            File
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct ShellFileInfo
        {
            public IntPtr hIcon;

            public int iIcon;

            public uint dwAttributes;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SHGetFileInfo(string path, uint attributes, out ShellFileInfo fileInfo, uint size, uint flags);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DestroyIcon(IntPtr pointer);

        #endregion
    }
}
