using System;
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
                .Any(file => !file.FileFullName.StartsWith(selectedPath));
        }

        private void MarkDuplicatesInGroup(DuplicateGroup duplicateFileGroup, string selectedPath)
        {
            var duplicatesForMarking = duplicateFileGroup.DuplicateFiles.Where(file => !file.IsMarkedForDeletion && file.FileFullName.StartsWith(selectedPath));
            foreach (var duplicateFile in duplicatesForMarking)
            {
                duplicateFile.IsMarkedForDeletion = true;
                OnFilesMarkedForDeletion(1, duplicateFile.FileData.Size);
            }
        }

        protected virtual void OnFilesMarkedForDeletion(long count, long size)
        {
            FilesAutoMarkedForDeletion?.Invoke(this, new UpdateToDeleteEventArgs(count, size));
        }
    }
}
