using System;
using System.Runtime.InteropServices;

namespace DuplicateFileTool
{
    internal class UnmanagedMemory : IDisposable
    {
        private IntPtr Pointer { get; }
        private bool Disposed { get; set; }

        public UnmanagedMemory(int size)
        {
            Pointer = Marshal.AllocHGlobal(size);
        }

        ~UnmanagedMemory()
        {
            Dispose();
        }

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
}
