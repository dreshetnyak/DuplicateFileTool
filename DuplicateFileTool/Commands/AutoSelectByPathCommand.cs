using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using DuplicateFileTool.Properties;

namespace DuplicateFileTool.Commands
{
    internal class AutoSelectByPathCommand : CommandBase
    {
        private string _path;
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

        public event UpdateToDeleteEventHandler FilesAutoMarkedForDeletion;
        
        public AutoSelectByPathCommand(ObservableCollection<DuplicateGroup> duplicateFileGroups)
        {
            Enabled = false;
            DuplicateFileGroups = duplicateFileGroups;
        }

        public override void Execute(object parameter)
        {
            var selectedPath = GetThePathForSelection();
            if (selectedPath != null)
                MarkDuplicatedFiles(selectedPath);
        }

        private string GetThePathForSelection()
        {
            var path = Path;
            if (path == null)
                return null;

            using var dialog = new FolderBrowserDialog
            {
                Description = Resources.Ui_AutoSelectByPath_Description,
                RootFolder = Environment.SpecialFolder.Desktop,
                SelectedPath = new FileInfo(path).DirectoryName,
                ShowNewFolderButton = false
            };

            return dialog.ShowDialog() == DialogResult.OK ? dialog.SelectedPath : null;
        }

        private void MarkDuplicatedFiles(string selectedPath)
        {
            foreach (var duplicateFileGroup in DuplicateFileGroups)
            {
                if (IsAfterMarkingAtLeastOneLeft(duplicateFileGroup, selectedPath))
                    MarkDuplicatesInGroup(duplicateFileGroup, selectedPath);
            }
        }

        private static bool IsAfterMarkingAtLeastOneLeft(DuplicateGroup duplicateFileGroup, string selectedPath)
        {
            return duplicateFileGroup.DuplicateFiles
                .Where(file => !file.IsMarkedForDeletion) //Among files not marked for deletion
                .Any(file => !FullFileNameStartsWithPath(file.FileFullName, selectedPath));
        }

        private void MarkDuplicatesInGroup(DuplicateGroup duplicateFileGroup, string selectedPath)
        {
            foreach (var duplicateFile in duplicateFileGroup.DuplicateFiles)
            {
                if (duplicateFile.IsMarkedForDeletion || !FullFileNameStartsWithPath(duplicateFile.FileFullName, selectedPath))
                    continue;
                duplicateFile.IsMarkedForDeletion = true;
                OnFilesMarkedForDeletion(1, duplicateFile.FileData.Size);
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
                    throw new ApplicationException("Archive parent directory does not exist");
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

        protected virtual void OnFilesMarkedForDeletion(long count, long size)
        {
            FilesAutoMarkedForDeletion?.Invoke(this, new UpdateToDeleteEventArgs(count, size));
        }
    }
}
