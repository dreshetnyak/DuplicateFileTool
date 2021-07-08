using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using DuplicateFileTool.Properties;

namespace DuplicateFileTool.Commands
{
    [Localizable(true)]
    internal class DeleteMarkedFilesCommand : CommandBase
    {
        private Action<string> WriteLog { get; }
        private Action<long> UpdateToDeleteSize { get; }
        private ObservableCollection<DuplicateGroup> DuplicateFiles { get; }
        private int TotalDeletedFileCount { get; set; }
        private long TotalDeletedSize { get; set; }

        public DeleteMarkedFilesCommand(ObservableCollection<DuplicateGroup> duplicateFiles, Action<long> updateToDeleteSize, Action<string> writeLog)
        {
            WriteLog = writeLog ?? delegate { };
            DuplicateFiles = duplicateFiles;
            UpdateToDeleteSize = updateToDeleteSize;
        }

        public override void Execute(object parameter)
        {
            try
            {
                Enabled = false;
                TotalDeletedFileCount = 0;
                TotalDeletedSize = 0;
                WriteLog(Environment.NewLine + Resources.Log_Performing_deletion_of_the_marked_duplicates + Environment.NewLine);
                DeleteSelectedFilesInGroupsCollection();
            }
            finally
            {
                Enabled = true;
            }
        }

        private void DeleteSelectedFilesInGroupsCollection()
        {
            for (var index = 0; index < DuplicateFiles.Count; index++)
            {
                var duplicateFileGroup = DuplicateFiles[index];
                var unmarkedFilesCount = GetUnmarkedFilesCount(duplicateFileGroup);

                if (unmarkedFilesCount < 1)
                {
                    WriteLog(Resources.Warning_Encountered_a_group_with_all_files_selected + Environment.NewLine);
                    UnselectAll(duplicateFileGroup);
                    continue;
                }

                DeleteSelectedFilesInGroup(duplicateFileGroup);

                if (unmarkedFilesCount == 1)
                    DuplicateFiles.RemoveAt(index);
            }

            WriteLog(Environment.NewLine + string.Format(Resources.Log_Files_deleted_Count, TotalDeletedFileCount) + Environment.NewLine + string.Format(Resources.Log_Deleted_Size, TotalDeletedSize) + Environment.NewLine);
        }

        private static int GetUnmarkedFilesCount(DuplicateGroup duplicateFileGroup)
        {
            return duplicateFileGroup.DuplicateFiles.Count(file => !file.IsMarkedForDeletion);
        }

        private void UnselectAll(DuplicateGroup duplicateFileGroup)
        {
            foreach (var duplicatedFile in duplicateFileGroup.DuplicateFiles)
            {
                if (duplicatedFile.IsMarkedForDeletion)
                    continue;
                WriteLog($"Unselecting: {duplicatedFile.FileFullName}" + Environment.NewLine);
                duplicatedFile.IsMarkedForDeletion = false;
                UpdateToDeleteSize(-duplicatedFile.FileData.Size);
            }
        }

        private void DeleteSelectedFilesInGroup(DuplicateGroup duplicateFileGroup)
        {
            var duplicatedFilesCollection = duplicateFileGroup.DuplicateFiles;

            for (var index = 0; index < duplicatedFilesCollection.Count; index++)
            {
                var duplicatedFile = duplicatedFilesCollection[index];
                if (!duplicatedFile.IsMarkedForDeletion)
                    continue;

                var fileSize = duplicatedFile.FileData.Size;

                WriteLog(string.Format(Resources.Log_Deleting_Name_Size, duplicatedFile.FileFullName, duplicatedFile.FileSize) + Environment.NewLine);
                TotalDeletedSize += fileSize;
                TotalDeletedFileCount++;

                try
                {
                    FileSystem.DeleteFile(duplicatedFile.FileFullName);
                    duplicatedFilesCollection.RemoveAt(index);
                    UpdateToDeleteSize(-fileSize);
                }
                catch (FileSystemException ex)
                {
                    WriteLog(string.Format(Resources.Log_Error_Deleting_failed_Name, ex.FileFullName) + Environment.NewLine + string.Format(Resources.Log_Error_Deleting_failed_Exception, ex.Message) + Environment.NewLine);
                    //continue;
                }

                //If the empty dirs have to be removed do it here
            }
        }
    }
}
