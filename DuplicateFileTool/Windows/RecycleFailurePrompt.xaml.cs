using System.Windows;
using Application = System.Windows.Application;

namespace DuplicateFileTool.Windows;

/// <summary>
/// Interaction logic for RecycleFailurePrompt.xaml
/// </summary>
public partial class RecycleFailurePrompt : Window
{
    private RecycleFailurePromptViewModel ViewModel { get; }

    public RecycleFailurePrompt(RecycleFailurePromptViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = viewModel;
        Owner = Application.Current.MainWindow;
    }

    private void OnDeletePermanentlyClick(object sender, RoutedEventArgs e)
    {
        ViewModel.Decision = RecycleFailureDecision.DeletePermanently;
        Close();
    }

    private void OnIgnoreClick(object sender, RoutedEventArgs e)
    {
        ViewModel.Decision = RecycleFailureDecision.Ignore;
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        ViewModel.Decision = RecycleFailureDecision.Cancel;
        Close();
    }
}
