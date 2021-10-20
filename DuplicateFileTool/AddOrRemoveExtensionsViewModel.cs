using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using DuplicateFileTool.Annotations;
using DuplicateFileTool.Commands;
using DuplicateFileTool.Configuration;

namespace DuplicateFileTool
{
    public interface IAddOrRemoveExtensionsViewModel
    {
        ObservableCollection<FileExtension> SelectedExtensions { get; }

        ICommand AddCommand { get; }
        ICommand RemoveCommand { get; }
        ICommand CancelCommand { get; }
        ICommand DocumentsCommand { get; }
        ICommand ImagesCommand { get; }
        ICommand AudioCommand { get; }
        ICommand VideoCommand { get; }
        ICommand SourceCodeCommand { get; }
        ICommand BinaryCommand { get; }
    }

    internal class AddOrRemoveExtensionsViewModel : IAddOrRemoveExtensionsViewModel, INotifyPropertyChanged
    {
        private ExtensionsConfiguration ExtensionsConfig { get; }
        private ObservableCollection<FileExtension> Extensions { get; }

        public ObservableCollection<FileExtension> SelectedExtensions { get; } = new();

        public ICommand AddCommand { get; }
        public ICommand RemoveCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand DocumentsCommand { get; }
        public ICommand ImagesCommand { get; }
        public ICommand AudioCommand { get; }
        public ICommand VideoCommand { get; }
        public ICommand SourceCodeCommand { get; }
        public ICommand BinaryCommand { get; }

        public event EventHandler CommandFinishedEvent;
        
        public AddOrRemoveExtensionsViewModel(ObservableCollection<FileExtension> extensions, ExtensionsConfiguration extensionsConfig)
        {
            Extensions = extensions;
            ExtensionsConfig = extensionsConfig;
            AddCommand = new RelayCommand(_ => AddSelected());
            RemoveCommand = new RelayCommand(_ => RemoveSelected());
            CancelCommand = new RelayCommand(_ => OnCommandFinishedEvent());

            DocumentsCommand = new RelayCommand(isChecked => ToggleExtensionType(isChecked, FileExtensionType.Documents));
            ImagesCommand = new RelayCommand(isChecked => ToggleExtensionType(isChecked, FileExtensionType.Images));
            AudioCommand = new RelayCommand(isChecked => ToggleExtensionType(isChecked, FileExtensionType.Audio));
            VideoCommand = new RelayCommand(isChecked => ToggleExtensionType(isChecked, FileExtensionType.Video));
            SourceCodeCommand = new RelayCommand(isChecked => ToggleExtensionType(isChecked, FileExtensionType.SourceCode));
            BinaryCommand = new RelayCommand(isChecked => ToggleExtensionType(isChecked, FileExtensionType.Binaries));
        }
        
        private void AddSelected()
        {
            foreach (var extension in SelectedExtensions)
                AddIfMissing(Extensions, extension);
            OnCommandFinishedEvent();
        }

        private void RemoveSelected()
        {
            foreach (var extension in SelectedExtensions)
                RemoveIfPresent(Extensions, extension);
            OnCommandFinishedEvent();
        }

        public void ToggleExtensionType(object isCheckedObj, FileExtensionType fileExtensionType)
        {
            if (isCheckedObj is not bool isChecked || fileExtensionType == FileExtensionType.Other)
                return;

            foreach (var extension in ExtensionsConfig.Extensions.Where(fileExtension => fileExtension.Type == fileExtensionType))
            {
                if (isChecked)
                    AddIfMissing(SelectedExtensions, extension);
                else
                    RemoveIfPresent(SelectedExtensions, extension);
            }
        }

        private static void AddIfMissing(ICollection<FileExtension> extensions, FileExtension fileExtension)
        {
            if (extensions.All(selectedExtension => !selectedExtension.Equals(fileExtension)))
                extensions.Add((FileExtension)fileExtension.Clone());
        }

        private static void RemoveIfPresent(IList<FileExtension> extensions, FileExtension fileExtension)
        {
            for (var index = extensions.Count - 1; index >= 0; index--)
            {
                var selectedExtension = extensions[index];
                if (fileExtension.Equals(selectedExtension))
                    extensions.RemoveAt(index);
            }
        }

        #region INotifyPropertyChanged Implementation

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion

        protected virtual void OnCommandFinishedEvent()
        {
            CommandFinishedEvent?.Invoke(this, EventArgs.Empty);
        }
    }
}