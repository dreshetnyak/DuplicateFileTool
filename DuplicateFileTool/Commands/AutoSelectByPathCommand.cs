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
        private Action<long> UpdateToDeleteSize { get; }
        private ObservableCollection<DuplicateGroup> DuplicateFileGroups { get; }
        public Func<string> SelectedDuplicatePath { get; }

        public AutoSelectByPathCommand(ObservableCollection<DuplicateGroup> duplicateFileGroups, Func<string> selectedDuplicatePath, Action<long> updateToDeleteSize)
        {
            Enabled = false;
            DuplicateFileGroups = duplicateFileGroups;
            SelectedDuplicatePath = selectedDuplicatePath;
            UpdateToDeleteSize = updateToDeleteSize;
        }

        public override void Execute(object parameter)
        {
            var selectedPath = GetThePathForSelection();
            if (selectedPath != null)
                MarkDuplicatedFiles(selectedPath);
        }

        private string GetThePathForSelection()
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = Resources.Ui_AutoSelectByPath_Description,
                RootFolder = Environment.SpecialFolder.Desktop,
                SelectedPath = new FileInfo(SelectedDuplicatePath()).DirectoryName,
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
                UpdateToDeleteSize?.Invoke(duplicateFile.FileData.Size);
            }
        }
    }
}
