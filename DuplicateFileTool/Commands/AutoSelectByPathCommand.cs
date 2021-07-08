using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Forms;

namespace DuplicateFileTool.Commands
{
    internal class AutoSelectByPathCommand : CommandBase
    {
        private Action<long> UpdateToDeleteSize { get; }
        private ObservableCollection<DuplicateGroup> DuplicateFileGroups { get; }

        public AutoSelectByPathCommand(ObservableCollection<DuplicateGroup> duplicateFileGroups, Action<long> updateToDeleteSize)
        {
            DuplicateFileGroups = duplicateFileGroups;
            UpdateToDeleteSize = updateToDeleteSize;
        }

        public override void Execute(object parameter)
        {
            try
            {
                Enabled = false;
                var selectedPath = GetThePathForSelection();
                if (selectedPath != null)
                    MarkDuplicatedFiles(selectedPath);
            }
            finally
            {
                Enabled = true;
            }
        }

        private static string GetThePathForSelection()
        {
            using var dialog = new FolderBrowserDialog();
            return dialog.ShowDialog() == DialogResult.OK 
                ? dialog.SelectedPath 
                : null;
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
