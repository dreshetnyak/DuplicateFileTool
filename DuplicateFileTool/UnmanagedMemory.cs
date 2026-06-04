using System.Runtime.InteropServices;

namespace DuplicateFileTool;

internal sealed class UnmanagedMemory(int size) : IDisposable
{
    private IntPtr Pointer { get; } = Marshal.AllocHGlobal(size);
    private bool Disposed { get; set; }

    ~UnmanagedMemory() => 
        Dispose();

    public void Dispose()
    {
        if (Disposed)
            return;
        Marshal.FreeHGlobal(Pointer);
        Disposed = true;
        GC.SuppressFinalize(this);
    }

    public static implicit operator IntPtr(UnmanagedMemory unmanagedMemory) => unmanagedMemory.Pointer;
}