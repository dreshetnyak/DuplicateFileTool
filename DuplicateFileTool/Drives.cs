using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Shell32;

namespace DuplicateFileTool
{
    [DebuggerDisplay("Name: {Name}; Description: {Description}")]
    internal sealed class DriveData
    {
        public const int PHYSICAL_DRIVE_NUMBER_UNKNOWN = -1;

        public string Name { get; }
        public string Description { get; }
        public DriveType Type { get; }
        public string Format { get; } = "";
        public bool IsReady { get; }
        public string VolumeLabel { get; } = "";
        public long AvailableFreeSpace { get; }
        public long TotalFreeSpace { get; }
        public long TotalSize { get; }
        public int PhysicalDriveNumber { get; }

        public DriveData(DriveInfo driveInfo, string description, int physicalDriveNumber = PHYSICAL_DRIVE_NUMBER_UNKNOWN)
        {
            Name = driveInfo.Name;
            Type = driveInfo.DriveType;
            IsReady = driveInfo.IsReady;
            if (IsReady)
            {
                Format = driveInfo.DriveFormat;
                VolumeLabel = driveInfo.VolumeLabel;
                AvailableFreeSpace = driveInfo.AvailableFreeSpace;
                TotalFreeSpace = driveInfo.TotalFreeSpace;
                TotalSize = driveInfo.TotalSize;
            }

            Description = description;
            PhysicalDriveNumber = physicalDriveNumber;
        }
    }

    internal static class Drives
    {
        public static IEnumerable<DriveData> Get()
        {
            return DriveInfo.GetDrives().Select(drive => new DriveData(drive, GetDriveDescription(drive.Name), GetPhysicalDriveNumber(drive.Name)));
        }

        private static dynamic _shellObject;
        private static dynamic ShellObject => _shellObject ??= Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application"));
        public static string GetDriveDescription(string drivePath)
        {
            var driveData = (Folder2)ShellObject.NameSpace(drivePath);
            return driveData != null
                ? driveData.Self.Name
                : drivePath;
        }

        public static int GetPhysicalDriveNumber(string drivePath)
        {
            using var driveHandle = Win32.CreateFile($"\\\\.\\{drivePath[0]}:", Win32.FileAccess.None, Win32.FileShare.Read | Win32.FileShare.Write, null, Win32.CreationDisposition.OpenExisting, 0, IntPtr.Zero);
            if (driveHandle.IsInvalid)
                return DriveData.PHYSICAL_DRIVE_NUMBER_UNKNOWN;

            var volumeDiskExtents = new Win32.VOLUME_DISK_EXTENTS();
            var outBufferSize = Marshal.SizeOf(volumeDiskExtents);
            using var outBuffer = new UnmanagedMemory(outBufferSize);

            if (!Win32.DeviceIoControl(driveHandle, Win32.IOCTL_VOLUME_GET_VOLUME_DISK_EXTENTS, IntPtr.Zero, 0, outBuffer, outBufferSize, out _, IntPtr.Zero))
                return DriveData.PHYSICAL_DRIVE_NUMBER_UNKNOWN;

            Marshal.PtrToStructure(outBuffer, volumeDiskExtents);
            return (int)volumeDiskExtents.Extents.DiskNumber;
        }
    }
}
