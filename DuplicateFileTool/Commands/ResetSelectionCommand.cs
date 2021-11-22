using System.Collections.ObjectModel;

namespace DuplicateFileTool.Commands
{
    internal class ResetSelectionCommand : CommandBase
    {
        private ObservableCollection<DuplicateGroup> DuplicateGroups { get; }

        public event UpdateToDeleteEventHandler UpdateToDeleteSize;

        public ResetSelectionCommand(ObservableCollection<DuplicateGroup> duplicateGroups)
        {
            Enabled = false;
            DuplicateGroups = duplicateGroups;
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
                    OnUpdateToDeleteSize(-1, -duplicatedFile.FileData.Size);
                }
            }
        }

        protected virtual void OnUpdateToDeleteSize(long count, long size)
        {
            UpdateToDeleteSize?.Invoke(this, new UpdateToDeleteEventArgs(count, size));
        }
    }
}