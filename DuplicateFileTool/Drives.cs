using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace DuplicateFileTool;

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
    public static IEnumerable<DriveData> Get() => 
        DriveInfo.GetDrives().Select(drive => new DriveData(drive, GetDriveDescription(drive.Name), GetPhysicalDriveNumber(drive.Name)));

    private static object? _shellObject;

    private static object? ShellObject
    {
        get
        {
            if (_shellObject != null)
                return _shellObject;

            var type = Type.GetTypeFromProgID("Shell.Application");
            return type == null ? null : _shellObject = Activator.CreateInstance(type);
        }
    }

    public static string GetDriveDescription(string drivePath)
    {
        var shellObject = ShellObject;
        if (shellObject == null)
            return drivePath;

        try
        {
            var shellType = shellObject.GetType();
            var folder = shellType.InvokeMember(
                "NameSpace",
                BindingFlags.InvokeMethod,
                binder: null,
                target: shellObject,
                args: [drivePath]);

            if (folder == null)
                return drivePath;

            var folderType = folder.GetType();
            var self = folderType.InvokeMember(
                "Self",
                BindingFlags.GetProperty,
                binder: null,
                target: folder,
                args: null);

            if (self == null)
                return drivePath;

            var name = self.GetType().InvokeMember(
                "Name",
                BindingFlags.GetProperty,
                binder: null,
                target: self,
                args: null) as string;

            return string.IsNullOrWhiteSpace(name) ? drivePath : name;
        }
        catch (COMException)
        {
            return drivePath;
        }
        catch (MissingMethodException)
        {
            return drivePath;
        }
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
        return (int)volumeDiskExtents.Extents!.DiskNumber;
    }

    // Spinning disks report a seek penalty, SSDs do not. Returns null when the drive does not
    // support the query (some USB enclosures and virtual drives), the caller should then assume
    // the worst case, that is a drive with a seek penalty.
    public static bool? GetIncursSeekPenalty(int physicalDriveNumber)
    {
        using var driveHandle = Win32.CreateFile($"\\\\.\\PhysicalDrive{physicalDriveNumber}", Win32.FileAccess.None, Win32.FileShare.Read | Win32.FileShare.Write, null, Win32.CreationDisposition.OpenExisting, 0, IntPtr.Zero);
        if (driveHandle.IsInvalid)
            return null;

        var propertyQuery = new Win32.STORAGE_PROPERTY_QUERY { PropertyId = Win32.StorageDeviceSeekPenaltyProperty, QueryType = Win32.PropertyStandardQuery };
        var inBufferSize = Marshal.SizeOf(propertyQuery);
        using var inBuffer = new UnmanagedMemory(inBufferSize);
        Marshal.StructureToPtr(propertyQuery, inBuffer, false);

        var seekPenaltyDescriptor = new Win32.DEVICE_SEEK_PENALTY_DESCRIPTOR();
        var outBufferSize = Marshal.SizeOf(seekPenaltyDescriptor);
        using var outBuffer = new UnmanagedMemory(outBufferSize);

        if (!Win32.DeviceIoControl(driveHandle, Win32.IOCTL_STORAGE_QUERY_PROPERTY, inBuffer, inBufferSize, outBuffer, outBufferSize, out _, IntPtr.Zero))
            return null;

        Marshal.PtrToStructure(outBuffer, seekPenaltyDescriptor);
        return seekPenaltyDescriptor.IncursSeekPenalty != 0;
    }
}