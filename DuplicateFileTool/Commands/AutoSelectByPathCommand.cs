using System.Collections.ObjectModel;
using System.IO;
using DuplicateFileTool.Properties;

namespace DuplicateFileTool.Commands;

internal sealed class AutoSelectProgressEventArgs(int selectedCount, int currentFileIndex, int totalFilesCount) : EventArgs
{
    public int SelectedCount { get; } = selectedCount;
    public int CurrentFileIndex { get; } = currentFileIndex;
    public int TotalFilesCount { get; } = totalFilesCount;
}
internal delegate void AutoSelectProgressEventHandler(object? sender, AutoSelectProgressEventArgs eventArgs);

internal sealed class AutoSelectStartingEventArgs(int selectedCount) : EventArgs
{
    public int SelectedCount { get; } = selectedCount;
}
internal delegate void AutoSelectStartingEventHandler(object? sender, AutoSelectStartingEventArgs eventEventArgs);

internal sealed class AutoSelectByPathCommand : CommandBase
{
    private string _path = "";
    private ObservableCollection<DuplicateGroup> DuplicateFileGroups { get; }

    public string Path
    {
        get => _path;
        set
        {
            if (_path == value)
                return;
            _path = value;
            Enabled = !string.IsNullOrEmpty(value);
            OnPropertyChanged();
        }
    }

    public event AutoSelectProgressEventHandler? Progress;
    public event EventHandler? Starting;
    public event AutoSelectStartingEventHandler? Finished;

    public AutoSelectByPathCommand(ObservableCollection<DuplicateGroup> duplicateFileGroups)
    {
        Enabled = false;
        DuplicateFileGroups = duplicateFileGroups;
    }

    public override async void Execute(object? parameter)
    {
        var selectedPath = GetThePathForSelection();
        if (selectedPath == "")
            return;
        try
        {
            Enabled = false;
            await Task.Run(() => MarkDuplicatedFiles(selectedPath));
        }
        finally
        {
            Enabled = true;
        }
    }

    private string GetThePathForSelection()
    {
        var path = Path;
        if (path == "")
            return "";

        // The classic SHBrowseForFolder tree picker centered over the main window. WinForms' FolderBrowserDialog can
        // render the same legacy tree but cannot be centered on the parent (it centers on screen and exposes no
        // reposition hook), so this drives SHBrowseForFolder directly via the BFFM_INITIALIZED callback. Runs on the
        // UI (STA) thread because Execute() calls this synchronously before Task.Run.
        var mainWindow = System.Windows.Application.Current?.MainWindow;
        var ownerHandle = mainWindow != null
            ? new System.Windows.Interop.WindowInteropHelper(mainWindow).Handle
            : IntPtr.Zero;
        var initialPath = new FileInfo(path).DirectoryName ?? "";
        return ClassicFolderPicker.Show(ownerHandle, Resources.Ui_AutoSelectByPath_Description, initialPath);
    }

    private void MarkDuplicatedFiles(string selectedPath)
    {
        OnAutoSelectStarting();

        var selectedCount = 0;
        var currentFileIndex = 0;
        var totalFiles = DuplicateFileGroups.Sum(group => group.FilesCount);
            
        foreach (var duplicateFileGroup in DuplicateFileGroups)
        {
            if (IsAfterMarkingAtLeastOneLeft(duplicateFileGroup, selectedPath))
                MarkDuplicatesInGroup(duplicateFileGroup, selectedPath, totalFiles, ref currentFileIndex, ref selectedCount);
        }

        OnAutoSelectFinished(selectedCount);
    }

    private static bool IsAfterMarkingAtLeastOneLeft(DuplicateGroup duplicateFileGroup, string selectedPath)
    {
        foreach (var file in duplicateFileGroup.DuplicateFiles)
        {
            if (!file.IsMarkedForDeletion && !FullFileNameStartsWithPath(file.FileFullName, selectedPath)) 
                return true;
        }

        return false;
    }

    private void MarkDuplicatesInGroup(DuplicateGroup duplicateFileGroup, string selectedPath, int totalFiles, ref int currentFileIndex, ref int selectedCount)
    {
        foreach (var duplicateFile in duplicateFileGroup.DuplicateFiles)
        {
            try
            {
                if (duplicateFile.IsMarkedForDeletion || !FullFileNameStartsWithPath(duplicateFile.FileFullName, selectedPath))
                    continue;
                // Marking mutates the engine's DeletionSelection set, which updates the to-be-deleted totals via
                // DuplicatesEngine.OnDeletionSelectionChanged. No delta event is fired here.
                duplicateFile.IsMarkedForDeletion = true;
                selectedCount++;
            }
            finally
            {
                currentFileIndex++;
                OnAutoSelectProgress(selectedCount, currentFileIndex, totalFiles);
            }
        }
    }

    private static bool FullFileNameStartsWithPath(string fullFileName, string startsWithPath)
    {
        var fullFileNamePathItems = GetPathItems(fullFileName);
        var startsWithPathItems = GetPathItems(startsWithPath);

        var fullFileNamePathItemsCount = fullFileNamePathItems.Count;
        var startsWithPathItemsCount = startsWithPathItems.Count;

        if (fullFileNamePathItemsCount < startsWithPathItemsCount)
            return false;

        for (var index = 0; index < startsWithPathItems.Count; index++)
        {
            if (!startsWithPathItems[index].Equals(fullFileNamePathItems[index], StringComparison.CurrentCultureIgnoreCase))
                return false;
        }

        return true;
    }
        
    private static List<string> GetPathItems(string path)
    {
        var pathDirInfo = new DirectoryInfo(path);
        if ((pathDirInfo.Attributes & System.IO.FileAttributes.Archive) == System.IO.FileAttributes.Archive)
        {
            pathDirInfo = pathDirInfo.Parent;
            if (pathDirInfo == null)
                throw new InvalidOperationException("Archive parent directory does not exist");
        }

        var currentDir = pathDirInfo;
        var pathItems = new List<string>(path.Count(System.IO.Path.PathSeparator) + 1);
        do
        {
            pathItems.Add(currentDir.Name);
        } while ((currentDir = currentDir.Parent) != null);

        pathItems.Reverse();
        return pathItems;
    }

    private void OnAutoSelectProgress(int selectedCount, int currentFileIndex, int totalFilesCount) =>
        Progress?.Invoke(this, new AutoSelectProgressEventArgs(selectedCount, currentFileIndex, totalFilesCount));

    private void OnAutoSelectStarting() => 
        Starting?.Invoke(this, EventArgs.Empty);

    private void OnAutoSelectFinished(int selectedCount) => 
        Finished?.Invoke(this, new AutoSelectStartingEventArgs(selectedCount));
}