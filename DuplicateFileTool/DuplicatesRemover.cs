using System.Collections.ObjectModel;
using System.ComponentModel;
using DuplicateFileTool.Properties;
using Application = System.Windows.Application;

namespace DuplicateFileTool;

#region DeletionState Event Implementation

internal sealed class DeletionState
{
    public int CurrentFileForDeletionIndex { get; set; }
    public int TotalFilesForDeletionCount { get; set; }
    public int DeletedCountDelta { get; set; }
    public int TotalDeletedCount { get; set; }
    public long TotalDeletedSize { get; set; }
    public long DeletedSizeDelta { get; set; }
}

internal sealed class DeletionStateEventArgs(DeletionState state) : EventArgs
{
    public DeletionState State { get; } = state;
}

internal delegate void DeletionStateEventHandler(object? sender, DeletionStateEventArgs eventArgs);

#endregion

#region DeletionMessage Event Implementation

internal sealed class DeletionMessage(string path, string message, MessageType messageType = MessageType.Information)
{
    public string Path { get; } = path;
    public string Text { get; } = message;
    public MessageType Type { get; } = messageType;

    public DeletionMessage(string message, MessageType messageType = MessageType.Information) : this("", message, messageType)
    { }
}

internal sealed class DeletionMessageEventArgs(DeletionMessage deletionMessage) : EventArgs
{
    public DeletionMessage Message { get; } = deletionMessage;
}

internal delegate void DeletionMessageEventHandler(object? sender, DeletionMessageEventArgs eventArgs);

#endregion

#region Recycle Failure Prompt Types

internal enum RecycleFailureDecision { Cancel, Ignore, DeletePermanently }

internal sealed record RecycleFailureResponse(RecycleFailureDecision Decision, bool ApplyToAll);

/// <summary>Asks the user what to do with a file that cannot be moved to the recycle bin.
/// Invoked on the deletion worker thread; the implementation is responsible for marshaling to the UI thread.</summary>
internal delegate RecycleFailureResponse RecycleFailurePromptHandler(string filePath, string reason);

#endregion

[Localizable(true)]
internal sealed class DuplicatesRemover
{
    public event DeletionMessageEventHandler? DeletionMessage;
    public event DeletionStateEventHandler? DeletionStateChanged;

    private RecycleFailureDecision? _stickyRecycleDecision; //"Apply to all remaining" choice, lasts for one deletion run

    public async Task RemoveDuplicates(ObservableCollection<DuplicateGroup> duplicateFiles, DeletionSelection selection, Func<string, bool> isDuplicate, bool removeEmptyDirs, bool deleteToRecycleBin, RecycleFailurePromptHandler promptRecycleFailure, CancellationToken cancellationToken)
    {
        OnDeletionMessage(Resources.Log_Performing_deletion_of_the_marked_duplicates);
        var deletionState = new DeletionState { TotalFilesForDeletionCount = selection.Count };
        _stickyRecycleDecision = null;

        try
        {
            await Task.Run(() =>
            {
                // Pass 1: duplicates, walked through the groups so the group/file collections update and emptied
                // groups collapse. Pass 2: every remaining set path that is not a duplicate (non-duplicates have
                // no group to update).
                DeleteSelectedFilesInGroupsCollection(duplicateFiles, deletionState, selection, removeEmptyDirs, deleteToRecycleBin, promptRecycleFailure, cancellationToken);
                DeleteNonDuplicateSetFiles(deletionState, selection, isDuplicate, removeEmptyDirs, deleteToRecycleBin, promptRecycleFailure, cancellationToken);
                // After both file passes, force-remove the folders the user explicitly selected as a whole. This is
                // UNCONDITIONAL (not gated on removeEmptyDirs): an explicitly-selected folder is removed even when the
                // setting is off. The per-file dup-only empty-dir cleanup above stays governed by the setting.
                RemoveSelectedDirectories(selection, cancellationToken);
            }, cancellationToken);
            OnDeletionMessage(Resources.Log_Deletion_Completed + string.Format(Resources.Log_Files_Deletion_Summary, deletionState.TotalDeletedCount, deletionState.TotalDeletedSize));
        }
        catch (OperationCanceledException) //Cancelled via the cancel button or via the recycle failure prompt
        {
            OnDeletionMessage(Resources.Log_Deletion_Cancelled + string.Format(Resources.Log_Files_Deletion_Summary, deletionState.TotalDeletedCount, deletionState.TotalDeletedSize));
        }
    }

    private void DeleteSelectedFilesInGroupsCollection(ObservableCollection<DuplicateGroup> duplicateGroups, DeletionState deletionState, DeletionSelection selection, bool removeEmptyDirs, bool deleteToRecycleBin, RecycleFailurePromptHandler promptRecycleFailure, CancellationToken cancellationToken)
    {
        var duplicateGroupsCount = duplicateGroups.Count;
        for (var index = 0; index < duplicateGroupsCount; index++)
        {
            var duplicateFileGroup = duplicateGroups[index];
            var duplicateFiles = duplicateFileGroup.DuplicateFiles;

            DeleteSelectedFilesInGroup(duplicateFileGroup, deletionState, selection, removeEmptyDirs, deleteToRecycleBin, promptRecycleFailure, cancellationToken);

            if (duplicateFiles.Count > 1)
                continue;

            var indexClosure = index;
            Application.Current.Dispatcher.Invoke(() => duplicateGroups.RemoveAt(indexClosure));
            index--;
            duplicateGroupsCount--;

            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    private void DeleteSelectedFilesInGroup(DuplicateGroup duplicateFileGroup, DeletionState deletionStatus, DeletionSelection selection, bool removeEmptyDirs, bool deleteToRecycleBin, RecycleFailurePromptHandler promptRecycleFailure, CancellationToken cancellationToken)
    {
        var duplicateFiles = duplicateFileGroup.DuplicateFiles;
        var duplicateFilesCount = duplicateFiles.Count;
        for (var index = 0; index < duplicateFilesCount; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var duplicatedFile = duplicateFiles[index];
            if (!duplicatedFile.IsMarkedForDeletion)
                continue;

            if (!TryDeleteFile(duplicatedFile.FileData, deletionStatus, selection, removeEmptyDirs, deleteToRecycleBin, promptRecycleFailure, cancellationToken))
                continue; //Skipped/ignored: the file stays in its group and in the set; pass 2 excludes it (still a duplicate).

            var indexClosure = index;
            Application.Current.Dispatcher.Invoke(() => duplicateFiles.RemoveAt(indexClosure));
            index--;
            duplicateFilesCount--;
        }
    }

    private void DeleteNonDuplicateSetFiles(DeletionState deletionState, DeletionSelection selection, Func<string, bool> isDuplicate, bool removeEmptyDirs, bool deleteToRecycleBin, RecycleFailurePromptHandler promptRecycleFailure, CancellationToken cancellationToken)
    {
        foreach (var path in selection.GetFilePaths())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (isDuplicate(path))
                continue; //A duplicate (or a duplicate skipped in pass 1): handled by pass 1, never deleted here.

            var fileData = FileSystem.GetFileData(path);
            if (fileData.IsEmpty) //Gone or inaccessible: log and skip without disturbing the set/totals.
            {
                Application.Current.Dispatcher.Invoke(() => OnDeletionMessage(path, string.Format(Resources.Log_Error_Deletion_Failed, path, Resources.Error_File_not_found), MessageType.Error));
                continue;
            }

            TryDeleteFile(fileData, deletionState, selection, removeEmptyDirs, deleteToRecycleBin, promptRecycleFailure, cancellationToken);
        }
    }

    /// <summary>
    /// Deletes one file with the full recycle/permanent/fallback/sticky logic, updates the throttled deletion-state
    /// deltas, removes the path from the set silently and runs the (setting-gated) empty-dir cleanup. Used by both
    /// the duplicate group-walk (pass 1) and the non-duplicate set pass (pass 2).
    /// </summary>
    /// <returns>True if the file was deleted; false if it was skipped/ignored.</returns>
    private bool TryDeleteFile(FileData fileData, DeletionState deletionState, DeletionSelection selection, bool removeEmptyDirs, bool deleteToRecycleBin, RecycleFailurePromptHandler promptRecycleFailure, CancellationToken cancellationToken)
    {
        var fullFileName = fileData.FullName;
        Application.Current.Dispatcher.Invoke(() => OnDeletionMessage(string.Format(Resources.Log_Deleting_Name_Size, fullFileName, fileData.Size)));

        var toRecycleBin = deleteToRecycleBin;
        if (toRecycleBin)
        {
            var recycleCheck = FileSystem.CanRecycle(fullFileName);
            if (recycleCheck != FileSystem.RecycleCheck.Ok)
            {
                var reason = recycleCheck == FileSystem.RecycleCheck.PathTooLong
                    ? Resources.Error_Recycle_Path_Too_Long
                    : Resources.Error_Recycle_Not_Available;

                switch (GetRecycleFailureDecision(promptRecycleFailure, fullFileName, reason))
                {
                    case RecycleFailureDecision.Cancel:
                        throw new OperationCanceledException();
                    case RecycleFailureDecision.Ignore:
                        SkipFile(deletionState, fullFileName);
                        return false;
                    case RecycleFailureDecision.DeletePermanently:
                        Application.Current.Dispatcher.Invoke(() => OnDeletionMessage(fullFileName, string.Format(Resources.Log_Recycle_Deleting_Permanently, fullFileName), MessageType.Warning));
                        toRecycleBin = false;
                        break;
                }
            }
        }

        deletionState.DeletedSizeDelta = 0;
        deletionState.DeletedCountDelta = 0;

        try
        {
            try
            {
                FileSystem.DeleteFile(fileData, toRecycleBin);
            }
            catch (RecycleOperationException ex) //The file is still in place, ask the user what to do
            {
                switch (GetRecycleFailureDecision(promptRecycleFailure, fullFileName, ex.Message))
                {
                    case RecycleFailureDecision.Cancel:
                        throw new OperationCanceledException();
                    case RecycleFailureDecision.Ignore:
                        Application.Current.Dispatcher.Invoke(() => OnDeletionMessage(fullFileName, string.Format(Resources.Log_Recycle_Skipped, fullFileName), MessageType.Warning));
                        return false;
                    case RecycleFailureDecision.DeletePermanently:
                        Application.Current.Dispatcher.Invoke(() => OnDeletionMessage(fullFileName, string.Format(Resources.Log_Recycle_Deleting_Permanently, fullFileName), MessageType.Warning));
                        FileSystem.DeleteFile(fileData, deleteToRecycleBin: false);
                        break;
                }
            }

            deletionState.TotalDeletedCount++;
            var fileSize = fileData.Size;
            deletionState.TotalDeletedSize += fileSize;
            deletionState.DeletedSizeDelta = -fileSize;
            deletionState.DeletedCountDelta = -1;

            // Silent set removal: DeletionStateChanged (throttled) owns the displayed-totals decrement, so removing
            // the entry here keeps the set consistent without double-counting. The directories set is left intact.
            selection.RemoveSilent(fullFileName);
        }
        catch (FileSystemException ex)
        {
            Application.Current.Dispatcher.Invoke(() => OnDeletionMessage(ex.FileFullName, string.Format(Resources.Log_Error_Deletion_Failed, ex.FileFullName, ex.Message), MessageType.Error));
            return false;
        }
        finally
        {
            deletionState.CurrentFileForDeletionIndex++;
            Application.Current.Dispatcher.Invoke(() => OnDeletionStateChanged(deletionState));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var dirPath = fileData.DirPath;
        if (removeEmptyDirs && FileSystem.IsDirectoryTreeEmpty(dirPath))
        {
            FileSystem.DeleteDirectoryTreeWithParents(dirPath,
                message => Application.Current.Dispatcher.Invoke(() => OnDeletionMessage(string.Format(Resources.Log_Deleting_Name, message))),
                (path, errorMessage) => Application.Current.Dispatcher.Invoke(() => OnDeletionMessage(path, errorMessage, MessageType.Error)));
        }

        return true;
    }

    /// <summary>
    /// Force-removes every folder the user marked as a whole (issue 012's directories set) whose tree the run
    /// emptied, regardless of the removeEmptyDirs setting (OQ-1). A directory still holding a leftover file, or one
    /// containing a junction, reports non-empty (see FileSystem.IsDirectoryTreeEmpty's reparse guard) and is left in
    /// place; a directory already removed by the per-file cleanup (setting on) is skipped. Uses the same
    /// dispatcher-wrapped logging callbacks as the per-file empty-dir cleanup in <see cref="TryDeleteFile"/>.
    /// </summary>
    private void RemoveSelectedDirectories(DeletionSelection selection, CancellationToken cancellationToken)
    {
        foreach (var dir in selection.GetSelectedDirectories())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!FileSystem.DirectoryExists(dir) || !FileSystem.IsDirectoryTreeEmpty(dir))
                continue; //Already gone (per-file cleanup) or not empty (leftover file or contained junction): leave it.

            FileSystem.DeleteDirectoryTreeWithParents(dir,
                message => Application.Current.Dispatcher.Invoke(() => OnDeletionMessage(string.Format(Resources.Log_Deleting_Name, message))),
                (path, errorMessage) => Application.Current.Dispatcher.Invoke(() => OnDeletionMessage(path, errorMessage, MessageType.Error)));
        }
    }

    private RecycleFailureDecision GetRecycleFailureDecision(RecycleFailurePromptHandler promptRecycleFailure, string filePath, string reason)
    {
        if (_stickyRecycleDecision.HasValue)
            return _stickyRecycleDecision.Value;

        var response = promptRecycleFailure(filePath, reason);
        if (response.ApplyToAll && response.Decision != RecycleFailureDecision.Cancel)
            _stickyRecycleDecision = response.Decision;

        return response.Decision;
    }

    private void SkipFile(DeletionState deletionState, string fileFullName)
    {
        Application.Current.Dispatcher.Invoke(() => OnDeletionMessage(fileFullName, string.Format(Resources.Log_Recycle_Skipped, fileFullName), MessageType.Warning));
        deletionState.DeletedSizeDelta = 0;
        deletionState.DeletedCountDelta = 0;
        deletionState.CurrentFileForDeletionIndex++;
        Application.Current.Dispatcher.Invoke(() => OnDeletionStateChanged(deletionState));
    }

    #region Event Invokators

    private void OnDeletionMessage(string message, MessageType messageType = MessageType.Information) => 
        DeletionMessage?.Invoke(this, new DeletionMessageEventArgs(new DeletionMessage(message, messageType)));

    private void OnDeletionMessage(string path, string message, MessageType messageType = MessageType.Information) => 
        DeletionMessage?.Invoke(this, new DeletionMessageEventArgs(new DeletionMessage(path, message, messageType)));

    private void OnDeletionStateChanged(DeletionState deletionState) => 
        DeletionStateChanged?.Invoke(this, new DeletionStateEventArgs(deletionState));

    #endregion
}