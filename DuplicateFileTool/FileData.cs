using System;
using System.Diagnostics;

namespace DuplicateFileTool
{
    internal class FileAttributes
    {
        private Win32.FileAttributes Attributes { get; set; }

        public bool IsReadonly
        {
            get => (Attributes & Win32.FileAttributes.Readonly) == Win32.FileAttributes.Readonly;
            set => Attributes = value ? Attributes | Win32.FileAttributes.Readonly : Attributes & ~Win32.FileAttributes.Readonly;
        }
        public bool IsHidden
        {
            get => (Attributes & Win32.FileAttributes.Hidden) == Win32.FileAttributes.Hidden;
            set => Attributes = value ? Attributes | Win32.FileAttributes.Hidden : Attributes & ~Win32.FileAttributes.Hidden;
        }
        public bool IsSystem
        {
            get => (Attributes & Win32.FileAttributes.System) == Win32.FileAttributes.System;
            set => Attributes = value ? Attributes | Win32.FileAttributes.System : Attributes & ~Win32.FileAttributes.System;
        }
        public bool IsDirectory
        {
            get => (Attributes & Win32.FileAttributes.Directory) == Win32.FileAttributes.Directory;
            set => Attributes = value ? Attributes | Win32.FileAttributes.Directory : Attributes & ~Win32.FileAttributes.Directory;
        }
        public bool IsArchive
        {
            get => (Attributes & Win32.FileAttributes.Archive) == Win32.FileAttributes.Archive;
            set => Attributes = value ? Attributes | Win32.FileAttributes.Archive : Attributes & ~Win32.FileAttributes.Archive;
        }
        public bool IsDevice
        {
            get => (Attributes & Win32.FileAttributes.Device) == Win32.FileAttributes.Device;
            set => Attributes = value ? Attributes | Win32.FileAttributes.Device : Attributes & ~Win32.FileAttributes.Device;
        }
        public bool IsNormal
        {
            get => (Attributes & Win32.FileAttributes.Normal) == Win32.FileAttributes.Normal;
            set => Attributes = value ? Attributes | Win32.FileAttributes.Normal : Attributes & ~Win32.FileAttributes.Normal;
        }
        public bool IsTemporary
        {
            get => (Attributes & Win32.FileAttributes.Temporary) == Win32.FileAttributes.Temporary;
            set => Attributes = value ? Attributes | Win32.FileAttributes.Temporary : Attributes & ~Win32.FileAttributes.Temporary;
        }
        public bool IsSparseFile
        {
            get => (Attributes & Win32.FileAttributes.SparseFile) == Win32.FileAttributes.SparseFile;
            set => Attributes = value ? Attributes | Win32.FileAttributes.SparseFile : Attributes & ~Win32.FileAttributes.SparseFile;
        }
        public bool IsReparsePoint
        {
            get => (Attributes & Win32.FileAttributes.ReparsePoint) == Win32.FileAttributes.ReparsePoint;
            set => Attributes = value ? Attributes | Win32.FileAttributes.ReparsePoint : Attributes & ~Win32.FileAttributes.ReparsePoint;
        }
        public bool IsCompressed
        {
            get => (Attributes & Win32.FileAttributes.Compressed) == Win32.FileAttributes.Compressed;
            set => Attributes = value ? Attributes | Win32.FileAttributes.Compressed : Attributes & ~Win32.FileAttributes.Compressed;
        }
        public bool IsOffline
        {
            get => (Attributes & Win32.FileAttributes.Offline) == Win32.FileAttributes.Offline;
            set => Attributes = value ? Attributes | Win32.FileAttributes.Offline : Attributes & ~Win32.FileAttributes.Offline;
        }
        public bool IsNotContentIndexed
        {
            get => (Attributes & Win32.FileAttributes.NotContentIndexed) == Win32.FileAttributes.NotContentIndexed;
            set => Attributes = value ? Attributes | Win32.FileAttributes.NotContentIndexed : Attributes & ~Win32.FileAttributes.NotContentIndexed;
        }
        public bool IsEncrypted
        {
            get => (Attributes & Win32.FileAttributes.Encrypted) == Win32.FileAttributes.Encrypted;
            set => Attributes = value ? Attributes | Win32.FileAttributes.Encrypted : Attributes & ~Win32.FileAttributes.Encrypted;
        }
        public bool IsWriteThrough
        {
            get => (Attributes & Win32.FileAttributes.Write_Through) == Win32.FileAttributes.Write_Through;
            set => Attributes = value ? Attributes | Win32.FileAttributes.Write_Through : Attributes & ~Win32.FileAttributes.Write_Through;
        }
        public bool IsOverlapped
        {
            get => (Attributes & Win32.FileAttributes.Overlapped) == Win32.FileAttributes.Overlapped;
            set => Attributes = value ? Attributes | Win32.FileAttributes.Overlapped : Attributes & ~Win32.FileAttributes.Overlapped;
        }
        public bool IsNoBuffering
        {
            get => (Attributes & Win32.FileAttributes.NoBuffering) == Win32.FileAttributes.NoBuffering;
            set => Attributes = value ? Attributes | Win32.FileAttributes.NoBuffering : Attributes & ~Win32.FileAttributes.NoBuffering;
        }
        public bool IsRandomAccess
        {
            get => (Attributes & Win32.FileAttributes.RandomAccess) == Win32.FileAttributes.RandomAccess;
            set => Attributes = value ? Attributes | Win32.FileAttributes.RandomAccess : Attributes & ~Win32.FileAttributes.RandomAccess;
        }
        public bool IsSequentialScan
        {
            get => (Attributes & Win32.FileAttributes.SequentialScan) == Win32.FileAttributes.SequentialScan;
            set => Attributes = value ? Attributes | Win32.FileAttributes.SequentialScan : Attributes & ~Win32.FileAttributes.SequentialScan;
        }
        public bool IsDeleteOnClose
        {
            get => (Attributes & Win32.FileAttributes.DeleteOnClose) == Win32.FileAttributes.DeleteOnClose;
            set => Attributes = value ? Attributes | Win32.FileAttributes.DeleteOnClose : Attributes & ~Win32.FileAttributes.DeleteOnClose;
        }
        public bool IsBackupSemantics
        {
            get => (Attributes & Win32.FileAttributes.BackupSemantics) == Win32.FileAttributes.BackupSemantics;
            set => Attributes = value ? Attributes | Win32.FileAttributes.BackupSemantics : Attributes & ~Win32.FileAttributes.BackupSemantics;
        }
        public bool IsPosixSemantics
        {
            get => (Attributes & Win32.FileAttributes.PosixSemantics) == Win32.FileAttributes.PosixSemantics;
            set => Attributes = value ? Attributes | Win32.FileAttributes.PosixSemantics : Attributes & ~Win32.FileAttributes.PosixSemantics;
        }
        public bool IsOpenReparsePoint
        {
            get => (Attributes & Win32.FileAttributes.OpenReparsePoint) == Win32.FileAttributes.OpenReparsePoint;
            set => Attributes = value ? Attributes | Win32.FileAttributes.OpenReparsePoint : Attributes & ~Win32.FileAttributes.OpenReparsePoint;
        }
        public bool IsOpenNoRecall
        {
            get => (Attributes & Win32.FileAttributes.OpenNoRecall) == Win32.FileAttributes.OpenNoRecall;
            set => Attributes = value ? Attributes | Win32.FileAttributes.OpenNoRecall : Attributes & ~Win32.FileAttributes.OpenNoRecall;
        }
        public bool IsFirstPipeInstance
        {
            get => (Attributes & Win32.FileAttributes.FirstPipeInstance) == Win32.FileAttributes.FirstPipeInstance;
            set => Attributes = value ? Attributes | Win32.FileAttributes.FirstPipeInstance : Attributes & ~Win32.FileAttributes.FirstPipeInstance;
        }

        public FileAttributes(uint attributes)
        {
            Attributes = (Win32.FileAttributes)attributes;
        }

        public static implicit operator uint(FileAttributes fileAttributes) => (uint) fileAttributes.Attributes;
    }

    [DebuggerDisplay("{" + nameof(FullName) + "}")]
    internal class FileData
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
            Attributes = new FileAttributes((uint)findData.dwFileAttributes);
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
