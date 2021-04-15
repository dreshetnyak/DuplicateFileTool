using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace FileBadger.Commands
{
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
                WriteLog(Environment.NewLine + "Performing deletion of the marked duplicates:" + Environment.NewLine);
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
                    WriteLog("Invalid selection, encountered a group with all files selected, at leas one file should be left unselected. The group will be ignored." + Environment.NewLine);
                    UnselectAll(duplicateFileGroup);
                    continue;
                }

                DeleteSelectedFilesInGroup(duplicateFileGroup);

                if (unmarkedFilesCount == 1)
                    DuplicateFiles.RemoveAt(index);
            }

            WriteLog(Environment.NewLine + $"Files deleted: {TotalDeletedFileCount}" + Environment.NewLine + $"Deleted size: {TotalDeletedSize:N0} Bytes" + Environment.NewLine);
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

                WriteLog($"Deleting: {duplicatedFile.FileFullName}; {duplicatedFile.FileSize}" + Environment.NewLine);
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
                    WriteLog($"Deleting failed: {ex.FileFullName}" + Environment.NewLine + $"Error: {ex.Message}" + Environment.NewLine);
                    //continue;
                }

                //If the empty dirs have to be removed do it here
            }
        }
    }
}
