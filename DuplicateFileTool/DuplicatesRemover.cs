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

    public async Task RemoveDuplicates(ObservableCollection<DuplicateGroup> duplicateFiles, bool removeEmptyDirs, bool deleteToRecycleBin, RecycleFailurePromptHandler promptRecycleFailure, CancellationToken cancellationToken)
    {
        OnDeletionMessage(Resources.Log_Performing_deletion_of_the_marked_duplicates);
        var deletionState = new DeletionState { TotalFilesForDeletionCount = duplicateFiles.Sum(group => group.DuplicateFiles.Count(dupFile => dupFile.IsMarkedForDeletion)) };
        _stickyRecycleDecision = null;

        try
        {
            await Task.Run(() => DeleteSelectedFilesInGroupsCollection(duplicateFiles, deletionState, removeEmptyDirs, deleteToRecycleBin, promptRecycleFailure, cancellationToken), cancellationToken);
            OnDeletionMessage(Resources.Log_Deletion_Completed + string.Format(Resources.Log_Files_Deletion_Summary, deletionState.TotalDeletedCount, deletionState.TotalDeletedSize));
        }
        catch (OperationCanceledException) //Cancelled via the cancel button or via the recycle failure prompt
        {
            OnDeletionMessage(Resources.Log_Deletion_Cancelled + string.Format(Resources.Log_Files_Deletion_Summary, deletionState.TotalDeletedCount, deletionState.TotalDeletedSize));
        }
    }

    private void DeleteSelectedFilesInGroupsCollection(ObservableCollection<DuplicateGroup> duplicateGroups, DeletionState deletionState, bool removeEmptyDirs, bool deleteToRecycleBin, RecycleFailurePromptHandler promptRecycleFailure, CancellationToken cancellationToken)
    {
        var duplicateGroupsCount = duplicateGroups.Count;
        for (var index = 0; index < duplicateGroupsCount; index++)
        {
            var duplicateFileGroup = duplicateGroups[index];
            var duplicateFiles = duplicateFileGroup.DuplicateFiles;
            var unmarkedFilesCount = duplicateFiles.Count(file => !file.IsMarkedForDeletion);

            if (unmarkedFilesCount < 1)
            {
                Application.Current.Dispatcher.Invoke(() => OnDeletionMessage(Resources.Warning_Encountered_a_group_with_all_files_selected, MessageType.Warning));
                UnmarkAll(duplicateFileGroup, deletionState);
                continue;
            }

            DeleteSelectedFilesInGroup(duplicateFileGroup, deletionState, removeEmptyDirs, deleteToRecycleBin, promptRecycleFailure, cancellationToken);

            if (duplicateFiles.Count > 1)
                continue;

            var indexClosure = index;
            Application.Current.Dispatcher.Invoke(() => duplicateGroups.RemoveAt(indexClosure));
            index--;
            duplicateGroupsCount--;

            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    private void UnmarkAll(DuplicateGroup duplicateFileGroup, DeletionState deletionState)
    {
        foreach (var duplicatedFile in duplicateFileGroup.DuplicateFiles)
        {
            if (duplicatedFile.IsMarkedForDeletion)
                continue;

            OnDeletionMessage(string.Format(Resources.Log_Unmarking_FullFileName, duplicatedFile.FileFullName));
            duplicatedFile.IsMarkedForDeletion = false;
            deletionState.CurrentFileForDeletionIndex++;
            deletionState.DeletedSizeDelta = 0;
            deletionState.DeletedCountDelta = 0;
            OnDeletionStateChanged(deletionState);
        }
    }

    private void DeleteSelectedFilesInGroup(DuplicateGroup duplicateFileGroup, DeletionState deletionStatus, bool removeEmptyDirs, bool deleteToRecycleBin, RecycleFailurePromptHandler promptRecycleFailure, CancellationToken cancellationToken)
    {
        var duplicateFiles = duplicateFileGroup.DuplicateFiles;
        var duplicateFilesCount = duplicateFiles.Count;
        for (var index = 0; index < duplicateFilesCount; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var duplicatedFile = duplicateFiles[index];
            if (!duplicatedFile.IsMarkedForDeletion)
                continue;

            var fileData = duplicatedFile.FileData;
            var fullFileName = fileData.FullName;
            Application.Current.Dispatcher.Invoke(() => OnDeletionMessage(string.Format(Resources.Log_Deleting_Name_Size, fullFileName, duplicatedFile.FileSize)));

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
                            SkipFile(deletionStatus, fullFileName);
                            continue;
                        case RecycleFailureDecision.DeletePermanently:
                            Application.Current.Dispatcher.Invoke(() => OnDeletionMessage(fullFileName, string.Format(Resources.Log_Recycle_Deleting_Permanently, fullFileName), MessageType.Warning));
                            toRecycleBin = false;
                            break;
                    }
                }
            }

            deletionStatus.DeletedSizeDelta = 0;
            deletionStatus.DeletedCountDelta = 0;

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
                            continue;
                        case RecycleFailureDecision.DeletePermanently:
                            Application.Current.Dispatcher.Invoke(() => OnDeletionMessage(fullFileName, string.Format(Resources.Log_Recycle_Deleting_Permanently, fullFileName), MessageType.Warning));
                            FileSystem.DeleteFile(fileData, deleteToRecycleBin: false);
                            break;
                    }
                }

                var indexClosure = index;
                Application.Current.Dispatcher.Invoke(() => duplicateFiles.RemoveAt(indexClosure));
                index--;
                duplicateFilesCount--;

                deletionStatus.TotalDeletedCount++;
                var fileSize = fileData.Size;
                deletionStatus.TotalDeletedSize += fileSize;
                deletionStatus.DeletedSizeDelta = -fileSize;
                deletionStatus.DeletedCountDelta = -1;
            }
            catch (FileSystemException ex)
            {
                Application.Current.Dispatcher.Invoke(() => OnDeletionMessage(ex.FileFullName, string.Format(Resources.Log_Error_Deletion_Failed, ex.FileFullName, ex.Message), MessageType.Error));
                continue;
            }
            finally
            {
                deletionStatus.CurrentFileForDeletionIndex++;
                Application.Current.Dispatcher.Invoke(() => OnDeletionStateChanged(deletionStatus));
            }

            cancellationToken.ThrowIfCancellationRequested();

            var dirPath = fileData.DirPath;
            if (!removeEmptyDirs || !FileSystem.IsDirectoryTreeEmpty(dirPath))
                continue;

            FileSystem.DeleteDirectoryTreeWithParents(dirPath,
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