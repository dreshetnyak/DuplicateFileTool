using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using DuplicateFileTool.Configuration;
using DuplicateFileTool.Windows;

namespace DuplicateFileTool.Commands;

internal sealed class AddOrRemoveExtensionsCommand(ObservableCollection<FileExtension> extensions, ExtensionsConfiguration extensionsConfig) : ICommand, INotifyPropertyChanged
{
    private bool _canExecuteCommand = true;
    private ObservableCollection<FileExtension> Extensions { get; } = extensions;
    private ExtensionsConfiguration ExtensionsConfig { get; } = extensionsConfig;

    public event EventHandler? CanExecuteChanged;
    public event PropertyChangedEventHandler? PropertyChanged;

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

    public bool CanExecute(object? parameter) => CanExecuteCommand;

    public void Execute(object? parameter)
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

    private void OnCanExecuteChanged() => 
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);

    private void OnPropertyChanged([CallerMemberName] string propertyName = "") => 
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    #endregion
}