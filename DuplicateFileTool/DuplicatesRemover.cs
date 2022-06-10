﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using DuplicateFileTool.Properties;

namespace DuplicateFileTool
{
    #region DeletionState Event Implementation

    internal class DeletionState
    {
        public int CurrentFileForDeletionIndex { get; set; }
        public int TotalFilesForDeletionCount { get; set; }
        public int DeletedCountDelta { get; set; }
        public int TotalDeletedCount { get; set; }
        public long TotalDeletedSize { get; set; }
        public long DeletedSizeDelta { get; set; }
    }

    internal class DeletionStateEventArgs : EventArgs
    {
        public DeletionState State { get; }

        public DeletionStateEventArgs(DeletionState state)
        {
            State = state;
        }
    }

    internal delegate void DeletionStateEventHandler(object sender, DeletionStateEventArgs eventArgs);

    #endregion

    #region DeletionMessage Event Implementation

    internal class DeletionMessage
    {
        public string Path { get; }
        public string Text { get; }
        public MessageType Type { get; }

        public DeletionMessage(string path, string message, MessageType messageType = MessageType.Information)
        {
            Path = path;
            Text = message;
            Type = messageType;
        }

        public DeletionMessage(string message, MessageType messageType = MessageType.Information)
        {
            Text = message;
            Type = messageType;
        }
    }

    internal class DeletionMessageEventArgs : EventArgs
    {
        public DeletionMessage Message { get; }

        public DeletionMessageEventArgs(DeletionMessage deletionMessage)
        {
            Message = deletionMessage;
        }
    }

    internal delegate void DeletionMessageEventHandler(object sender, DeletionMessageEventArgs eventArgs);

    #endregion
    
    [Localizable(true)]
    internal class DuplicatesRemover
    {
        public event DeletionMessageEventHandler DeletionMessage;
        public event DeletionStateEventHandler DeletionStateChanged;

        public async Task RemoveDuplicates(ObservableCollection<DuplicateGroup> duplicateFiles, bool removeEmptyDirs, bool deleteToRecycleBin, CancellationToken cancellationToken)
        {
            OnDeletionMessage(Resources.Log_Performing_deletion_of_the_marked_duplicates);
            var deletionState = new DeletionState { TotalFilesForDeletionCount = duplicateFiles.Sum(group => group.DuplicateFiles.Count(dupFile => dupFile.IsMarkedForDeletion)) };

            try
            {
                await Task.Run(() => DeleteSelectedFilesInGroupsCollection(duplicateFiles, deletionState, removeEmptyDirs, deleteToRecycleBin, cancellationToken), cancellationToken);
                OnDeletionMessage(Resources.Log_Deletion_Completed + string.Format(Resources.Log_Files_Deletion_Summary, deletionState.TotalDeletedCount, deletionState.TotalDeletedSize));
            }
            catch (TaskCanceledException)
            {
                OnDeletionMessage(Resources.Log_Deletion_Cancelled + string.Format(Resources.Log_Files_Deletion_Summary, deletionState.TotalDeletedCount, deletionState.TotalDeletedSize));
            }
        }

        private void DeleteSelectedFilesInGroupsCollection(IList<DuplicateGroup> duplicateGroups, DeletionState deletionState, bool removeEmptyDirs, bool deleteToRecycleBin, CancellationToken cancellationToken)
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

                DeleteSelectedFilesInGroup(duplicateFileGroup, deletionState, removeEmptyDirs, deleteToRecycleBin, cancellationToken);

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
                OnDeletionStateChanged(deletionState);
            }
        }

        private void DeleteSelectedFilesInGroup(DuplicateGroup duplicateFileGroup, DeletionState deletionStatus, bool removeEmptyDirs, bool deleteToRecycleBin, CancellationToken cancellationToken)
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

                try
                {
                    FileSystem.DeleteFile(fileData, deleteToRecycleBin);
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
                    (path, errorMessage) => Application.Current.Dispatcher.Invoke(() => OnDeletionMessage(path, errorMessage, MessageType.Error)),
                    deleteToRecycleBin);
            }
        }

        #region Event Invokators

        protected virtual void OnDeletionMessage(string message, MessageType messageType = MessageType.Information)
        {
            DeletionMessage?.Invoke(this, new DeletionMessageEventArgs(new DeletionMessage(message, messageType)));
        }

        protected virtual void OnDeletionMessage(string path, string message, MessageType messageType = MessageType.Information)
        {
            DeletionMessage?.Invoke(this, new DeletionMessageEventArgs(new DeletionMessage(path, message, messageType)));
        }

        protected virtual void OnDeletionStateChanged(DeletionState deletionState)
        {
            DeletionStateChanged?.Invoke(this, new DeletionStateEventArgs(deletionState));
        }

        #endregion
    }
}
