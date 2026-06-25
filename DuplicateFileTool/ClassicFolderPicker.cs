using System.Text;

namespace DuplicateFileTool;

/// <summary>
/// Shows the classic Win32 SHBrowseForFolder tree-view "Browse For Folder" dialog (the pre-Vista picker), centered
/// over an owner window and pre-selecting an initial folder.
/// <para>
/// WinForms' <c>FolderBrowserDialog</c> with <c>AutoUpgradeEnabled = false</c> renders the same legacy tree, but it
/// exposes no hook to reposition the window — and the classic dialog otherwise centers on the screen, not the parent.
/// Calling SHBrowseForFolder directly gives access to the BFFM_INITIALIZED callback, which is where both the initial
/// selection and the centering are applied.
/// </para>
/// <para>
/// Must be invoked on an STA thread: BIF_NEWDIALOGSTYLE (the resizable tree) requires apartment-threaded COM. The WPF
/// UI thread satisfies this, so call this from the UI thread (as <see cref="Commands.AutoSelectByPathCommand"/> does,
/// before any <c>Task.Run</c>).
/// </para>
/// </summary>
internal static class ClassicFolderPicker
{
    /// <summary>
    /// Opens the picker and returns the selected filesystem path, or an empty string if the user cancels.
    /// </summary>
    /// <param name="ownerHandle">HWND the dialog is owned by and centered over; pass <see cref="IntPtr.Zero"/> for none.</param>
    /// <param name="title">Text shown above the tree.</param>
    /// <param name="initialPath">Folder to pre-select and scroll into view; may be empty.</param>
    internal static string Show(IntPtr ownerHandle, string title, string initialPath)
    {
        // Rooted for the dialog's modal lifetime; GC.KeepAlive below makes that explicit.
        var callback = new Win32.BrowseCallbackProc((hwnd, msg, _, _) =>
        {
            if (msg != Win32.BFFM_INITIALIZED)
                return 0;
            if (!string.IsNullOrEmpty(initialPath))
                Win32.SendMessage(hwnd, Win32.BFFM_SETSELECTIONW, new IntPtr(1), initialPath);
            CenterOverOwner(hwnd, ownerHandle);
            return 0;
        });

        var browseInfo = new Win32.BROWSEINFO
        {
            hwndOwner = ownerHandle,
            lpszTitle = title,
            // Filesystem dirs only, resizable tree, no "Make New Folder" button (mirrors the old ShowNewFolderButton=false).
            ulFlags = Win32.BIF_RETURNONLYFSDIRS | Win32.BIF_NEWDIALOGSTYLE | Win32.BIF_NONEWFOLDERBUTTON,
            lpfn = callback
        };

        var pidl = Win32.SHBrowseForFolder(ref browseInfo);
        GC.KeepAlive(callback);
        if (pidl == IntPtr.Zero)
            return ""; // user cancelled

        try
        {
            var pathBuffer = new StringBuilder(Win32.MAX_PATH);
            return Win32.SHGetPathFromIDList(pidl, pathBuffer) ? pathBuffer.ToString() : "";
        }
        finally
        {
            Win32.CoTaskMemFree(pidl);
        }
    }

    /// <summary>
    /// Moves the just-created dialog so its center matches the owner window's center. Coordinates from GetWindowRect
    /// are physical screen pixels for both windows, so no DPI conversion is needed.
    /// </summary>
    private static void CenterOverOwner(IntPtr dialogHandle, IntPtr ownerHandle)
    {
        if (ownerHandle == IntPtr.Zero
            || !Win32.GetWindowRect(ownerHandle, out var owner)
            || !Win32.GetWindowRect(dialogHandle, out var dialog))
            return;

        var x = owner.Left + ((owner.Right - owner.Left) - (dialog.Right - dialog.Left)) / 2;
        var y = owner.Top + ((owner.Bottom - owner.Top) - (dialog.Bottom - dialog.Top)) / 2;

        Win32.SetWindowPos(dialogHandle, IntPtr.Zero, x, y, 0, 0,
            Win32.SWP_NOSIZE | Win32.SWP_NOZORDER | Win32.SWP_NOACTIVATE);
    }
}
