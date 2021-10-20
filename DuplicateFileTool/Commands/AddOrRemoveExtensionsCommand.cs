using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using DuplicateFileTool.Annotations;
using DuplicateFileTool.Configuration;
using DuplicateFileTool.Windows;

namespace DuplicateFileTool.Commands
{
    internal class AddOrRemoveExtensionsCommand : ICommand, INotifyPropertyChanged
    {
        private bool _canExecuteCommand = true;
        private ObservableCollection<FileExtension> Extensions { get; }
        private ExtensionsConfiguration ExtensionsConfig { get; }

        public event EventHandler CanExecuteChanged;
        public event PropertyChangedEventHandler PropertyChanged;

        public AddOrRemoveExtensionsCommand(ObservableCollection<FileExtension> extensions, ExtensionsConfiguration extensionsConfig)
        {
            Extensions = extensions;
            ExtensionsConfig = extensionsConfig;
        }

        public bool CanExecuteCommand
        {
            get => _canExecuteCommand;
            set
            {
                _canExecuteCommand = value;
                OnPropertyChanged();
                OnCanExecuteChanged();
            }
        }

        public bool CanExecute(object parameter)
        {
            return CanExecuteCommand;
        }

        public void Execute(object parameter)
        {
            try
            {
                CanExecuteCommand = false;

                var viewModel = new AddOrRemoveExtensionsViewModel(Extensions, ExtensionsConfig);
                var dialogWindow = new AddOrRemoveExtensions(viewModel);
                viewModel.CommandFinishedEvent += (_, _) => dialogWindow.Close();
                dialogWindow.ShowDialog();
            }
            finally
            {
                CanExecuteCommand = true;
            }
        }

        #region Event Invokators

        protected virtual void OnCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}
