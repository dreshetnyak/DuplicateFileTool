using System;
using System.Diagnostics;

namespace DuplicateFileTool
{
    public class FileAttributes
    {
        private Win32.FileAttributes Attributes { get; }

        // ReSharper disable UnusedMember.Global
        public bool IsReadonly => (Attributes & Win32.FileAttributes.Readonly) == Win32.FileAttributes.Readonly;
        public bool IsHidden => (Attributes & Win32.FileAttributes.Hidden) == Win32.FileAttributes.Hidden;
        public bool IsSystem => (Attributes & Win32.FileAttributes.System) == Win32.FileAttributes.System;
        public bool IsDirectory => (Attributes & Win32.FileAttributes.Directory) == Win32.FileAttributes.Directory;
        public bool IsArchive => (Attributes & Win32.FileAttributes.Archive) == Win32.FileAttributes.Archive;
        public bool IsDevice => (Attributes & Win32.FileAttributes.Device) == Win32.FileAttributes.Device;
        public bool IsNormal => (Attributes & Win32.FileAttributes.Normal) == Win32.FileAttributes.Normal;
        public bool IsTemporary => (Attributes & Win32.FileAttributes.Temporary) == Win32.FileAttributes.Temporary;
        public bool IsSparseFile => (Attributes & Win32.FileAttributes.SparseFile) == Win32.FileAttributes.SparseFile;
        public bool IsReparsePoint => (Attributes & Win32.FileAttributes.ReparsePoint) == Win32.FileAttributes.ReparsePoint;
        public bool IsCompressed => (Attributes & Win32.FileAttributes.Compressed) == Win32.FileAttributes.Compressed;
        public bool IsOffline => (Attributes & Win32.FileAttributes.Offline) == Win32.FileAttributes.Offline;
        public bool IsNotContentIndexed => (Attributes & Win32.FileAttributes.NotContentIndexed) == Win32.FileAttributes.NotContentIndexed;
        public bool IsEncrypted => (Attributes & Win32.FileAttributes.Encrypted) == Win32.FileAttributes.Encrypted;
        public bool IsWriteThrough => (Attributes & Win32.FileAttributes.Write_Through) == Win32.FileAttributes.Write_Through;
        public bool IsOverlapped => (Attributes & Win32.FileAttributes.Overlapped) == Win32.FileAttributes.Overlapped;
        public bool IsNoBuffering => (Attributes & Win32.FileAttributes.NoBuffering) == Win32.FileAttributes.NoBuffering;
        public bool IsRandomAccess => (Attributes & Win32.FileAttributes.RandomAccess) == Win32.FileAttributes.RandomAccess;
        public bool IsSequentialScan => (Attributes & Win32.FileAttributes.SequentialScan) == Win32.FileAttributes.SequentialScan;
        public bool IsDeleteOnClose => (Attributes & Win32.FileAttributes.DeleteOnClose) == Win32.FileAttributes.DeleteOnClose;
        public bool IsBackupSemantics => (Attributes & Win32.FileAttributes.BackupSemantics) == Win32.FileAttributes.BackupSemantics;
        public bool IsPosixSemantics => (Attributes & Win32.FileAttributes.PosixSemantics) == Win32.FileAttributes.PosixSemantics;
        public bool IsOpenReparsePoint => (Attributes & Win32.FileAttributes.OpenReparsePoint) == Win32.FileAttributes.OpenReparsePoint;
        public bool IsOpenNoRecall => (Attributes & Win32.FileAttributes.OpenNoRecall) == Win32.FileAttributes.OpenNoRecall;
        public bool IsFirstPipeInstance => (Attributes & Win32.FileAttributes.FirstPipeInstance) == Win32.FileAttributes.FirstPipeInstance;
        // ReSharper restore UnusedMember.Global

        internal FileAttributes(Win32.FileAttributes attributes)
        {
            Attributes = attributes;
        }
    }

    [DebuggerDisplay("{" + nameof(FullName) + "}")]
    public class FileData
    {
        public string DirPath { get; }
        public string FileName { get; }
        public string ShortFileName { get; }
        public string FullName { get; }
        public string Extension { get; }

        public DateTime CreationTime { get; }
        public DateTime LastAccessTime { get; }
        public DateTime LastWriteTime { get; }
        public long Size { get; }

        public FileAttributes Attributes { get; }

        internal FileData(string dirPath, Win32.WIN32_FIND_DATA findData)
        {
            Attributes = new FileAttributes(findData.dwFileAttributes);
            DirPath = !Attributes.IsDevice ? dirPath.TrimEnd('\\') : dirPath;
            FileName = findData.cFileName;
            FullName = Attributes.IsDevice ? DirPath : DirPath + '\\' + FileName;
            ShortFileName = findData.cAlternate;
            Extension = FileName.SubstringAfterLast('.');
            CreationTime = findData.ftCreationTime.ToDateTime();
            LastAccessTime = findData.ftLastAccessTime.ToDateTime();
            LastWriteTime = findData.ftLastWriteTime.ToDateTime();
            Size = Data.JoinToLong(findData.nFileSizeHigh, findData.nFileSizeLow);
        }
    }
}
