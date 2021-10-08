using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using DuplicateFileTool.Annotations;
using DuplicateFileTool.Windows;

namespace DuplicateFileTool.Commands
{
    internal class AddOrRemoveExtensionsCommand : ICommand, INotifyPropertyChanged
    {
        private bool _canExecuteCommand = true;
        private ObservableCollection<ObservableString> Extensions { get; }

        public event EventHandler CanExecuteChanged;
        public event PropertyChangedEventHandler PropertyChanged;

        public AddOrRemoveExtensionsCommand(ObservableCollection<ObservableString> extensions)
        {
            Extensions = extensions;
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

                var dialogWindow = new AddOrRemoveExtensions();
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
