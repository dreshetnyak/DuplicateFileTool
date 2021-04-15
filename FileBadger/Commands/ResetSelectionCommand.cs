using System;
using System.Collections.ObjectModel;

namespace FileBadger.Commands
{
    internal class ResetSelectionCommand : CommandBase
    {
        public ObservableCollection<DuplicateGroup> DuplicateGroups { get; }
        private Action<long> UpdateToDeleteSize { get; }

        public ResetSelectionCommand(ObservableCollection<DuplicateGroup> duplicateGroups, Action<long> updateToDeleteSize)
        {
            DuplicateGroups = duplicateGroups;
            UpdateToDeleteSize = updateToDeleteSize;
        }

        public override void Execute(object parameter)
        {
            try
            {
                Enabled = false;
                DeselectAll();
            }
            finally
            {
                Enabled = true;
            }
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
                }
            }
        }
    }
}
