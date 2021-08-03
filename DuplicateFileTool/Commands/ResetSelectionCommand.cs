using System;
using System.Collections.ObjectModel;

namespace DuplicateFileTool.Commands
{
    internal class ResetSelectionCommand : CommandBase
    {
        public ObservableCollection<DuplicateGroup> DuplicateGroups { get; }
        private Action<long> UpdateToDeleteSize { get; }
        private Action<long> UpdateToDeleteCount { get; }

        public ResetSelectionCommand(ObservableCollection<DuplicateGroup> duplicateGroups, Action<long> updateToDeleteSize, Action<long> updateToDeleteCount)
        {
            Enabled = false;
            DuplicateGroups = duplicateGroups;
            UpdateToDeleteSize = updateToDeleteSize;
            UpdateToDeleteCount = updateToDeleteCount;
        }

        public override void Execute(object parameter)
        {
            DeselectAll();
        }

        private void DeselectAll()
        {
            foreach (var duplicateFileGroup in DuplicateGroups)
            {
                foreach (var duplicatedFile in duplicateFileGroup.DuplicateFiles)
                {
                    if (!duplicatedFile.IsMarkedForDeletion) 
                        continue;
                    duplicatedFile.IsMarkedForDeletion = false;
                    UpdateToDeleteSize(-duplicatedFile.FileData.Size);
                    UpdateToDeleteCount(-1);
                }
            }
        }
    }
}
