using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using DuplicateFileTool.Properties;

//TODO must prevent selection/deselection for deletion during the deletion process

namespace DuplicateFileTool.Commands
{
    #region DeletionStatus Implementation

    internal class DeletionStatus
    {
        public int CurrentFileForDeletionIndex { get; set; }
        public int TotalFilesForDeletionCount { get; set; }
        public int TotalDeletedCount { get; set; }
        public long TotalDeletedSize { get; set; }
        public string FileFullName { get; set; }
        public string ErrorMessage { get; set; }
        public bool Finished { get; set; }
    }

    #endregion

    #region DeletionStatusUpdate Event Implementation

    internal class DeletionStatusEventArgs : EventArgs
    {
        public DeletionStatus Status { get; }

        public DeletionStatusEventArgs(DeletionStatus status)
        {
            Status = status;
        }
    }

    internal delegate void DeletionStatusEventHandler(object sender, DeletionStatusEventArgs eventArgs);

    #endregion

    [Localizable(true)]
    internal class DeleteMarkedFilesCommand : CommandBase
    {
        private Action<string> WriteLog { get; }
        private ObservableCollection<DuplicateGroup> DuplicateFiles { get; }
        public Func<bool> RemoveEmptyDirs { get; }
        public Func<bool> DeleteToRecycleBin { get; }

        public event DeletionStatusEventHandler DeletionStatusUpdate;

        public DeleteMarkedFilesCommand(
            ObservableCollection<DuplicateGroup> duplicateFiles,
            Func<bool> removeEmptyDirs,
            Func<bool> deleteToRecycleBin,
            Action<string> writeLog)
        {
            WriteLog = writeLog ?? delegate { };
            DuplicateFiles = duplicateFiles;
            RemoveEmptyDirs = removeEmptyDirs;
            DeleteToRecycleBin = deleteToRecycleBin;
        }

        public override async void Execute(object parameter)
        {
            try
            {
                Enabled = false;
                WriteLog(Environment.NewLine + Resources.Log_Performing_deletion_of_the_marked_duplicates + Environment.NewLine);
                await Task.Run(DeleteSelectedFilesInGroupsCollection);
            }
            finally
            {
                Enabled = true;
            }
        }

        private void DeleteSelectedFilesInGroupsCollection()
        {
            var deletionStatus = new DeletionStatus {TotalFilesForDeletionCount = DuplicateFiles.Sum(group => group.DuplicateFiles.Count(dupFile => dupFile.IsMarkedForDeletion))};
            var duplicateGroupsCount = DuplicateFiles.Count;
            for (var index = 0; index < duplicateGroupsCount; index++)
            {
                var duplicateFileGroup = DuplicateFiles[index];
                var unmarkedFilesCount = duplicateFileGroup.DuplicateFiles.Count(file => !file.IsMarkedForDeletion);

                if (unmarkedFilesCount < 1)
                {
                    WriteLog(Resources.Warning_Encountered_a_group_with_all_files_selected + Environment.NewLine);
                    UnmarkAll(duplicateFileGroup, deletionStatus);
                    continue;
                }

                DeleteSelectedFilesInGroup(duplicateFileGroup, deletionStatus);

                if (unmarkedFilesCount != 1)
                    continue;
                DuplicateFiles.RemoveAt(index--);
                duplicateGroupsCount--;
            }

            WriteLog(Environment.NewLine + string.Format(Resources.Log_Files_deleted_Count, deletionStatus.TotalDeletedCount) + Environment.NewLine + string.Format(Resources.Log_Deleted_Size, deletionStatus.TotalDeletedSize) + Environment.NewLine);
        }

        private void UnmarkAll(DuplicateGroup duplicateFileGroup, DeletionStatus deletionStatus)
        {
            foreach (var duplicatedFile in duplicateFileGroup.DuplicateFiles)
            {
                if (duplicatedFile.IsMarkedForDeletion)
                    continue;
                WriteLog(string.Format(Resources.Log_Unmarking_FullFileName, duplicatedFile.FileFullName) + Environment.NewLine);
                duplicatedFile.IsMarkedForDeletion = false;

                deletionStatus.FileFullName = duplicatedFile.FileFullName;
                OnDeletionStatusUpdate(deletionStatus);
                deletionStatus.CurrentFileForDeletionIndex++;
            }
        }

        private void DeleteSelectedFilesInGroup(DuplicateGroup duplicateFileGroup, DeletionStatus deletionStatus)
        {
            var duplicateFiles = duplicateFileGroup.DuplicateFiles;
            var duplicateFilesCount = duplicateFiles.Count;
            for (var index = 0; index < duplicateFilesCount; index++)
            {
                var duplicatedFile = duplicateFiles[index];
                if (!duplicatedFile.IsMarkedForDeletion)
                    continue;

                WriteLog(string.Format(Resources.Log_Deleting_Name_Size, duplicatedFile.FileFullName, duplicatedFile.FileSize) + Environment.NewLine);

                var fileData = duplicatedFile.FileData;

                try
                {
                    var fullFileName = deletionStatus.FileFullName = duplicatedFile.FileFullName;
                    FileSystem.DeleteFile(fullFileName);
                    duplicateFiles.RemoveAt(index--);
                    duplicateFilesCount--;

                    deletionStatus.TotalDeletedCount++;
                    deletionStatus.TotalDeletedSize += fileData.Size;
                    OnDeletionStatusUpdate(deletionStatus);
                }
                catch (FileSystemException ex)
                {
                    var deletionFailedMessage = string.Format(Resources.Log_Error_Deleting_failed_Name, ex.FileFullName);
                    var deletionFailedException = string.Format(Resources.Log_Error_Deleting_failed_Exception, ex.Message);

                    deletionStatus.ErrorMessage = deletionFailedMessage + ' ' + deletionFailedException;
                    OnDeletionStatusUpdate(deletionStatus);

                    WriteLog(deletionFailedMessage + Environment.NewLine + deletionFailedException + Environment.NewLine);
                    continue;
                }
                finally
                {
                    deletionStatus.CurrentFileForDeletionIndex++;
                }

                var dirPath = fileData.DirPath;
                if (!RemoveEmptyDirs() || !FileSystem.IsDirectoryTreeEmpty(dirPath))
                    continue;

                FileSystem.DeleteDirectoryTreeWithParents(dirPath, message => WriteLog(string.Format(Resources.Log_Deleting_Name, message) + Environment.NewLine));

                //TODO Report deletion errors

            }

            deletionStatus.Finished = true;
            OnDeletionStatusUpdate(deletionStatus);
        }

        #region Event Invokators

        protected virtual void OnDeletionStatusUpdate(DeletionStatus deletionStatus)
        {
            DeletionStatusUpdate?.Invoke(this, new DeletionStatusEventArgs(deletionStatus));
        }

        #endregion
    }
}
