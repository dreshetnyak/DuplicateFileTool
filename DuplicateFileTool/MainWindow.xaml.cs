using System.ComponentModel;
using System.Windows;
using System.Diagnostics;
using System.Windows.Controls;
using System.IO;

namespace DuplicateFileTool;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
[Localizable(true)]
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel(ResultsTreeView);
        Closed += OnWindowClosed;
        Activated += OnWindowActivated;
    }

    private void OnWindowActivated(object? sender, EventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
            viewModel.RefreshExpandedFileTreeItems();
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        if (DataContext is not IDisposable disposable)
            return;
        try { disposable.Dispose(); }
        catch { /* ignore */ }
    }

    // Keeps the window wide enough that the Results toolbar (sort, filter, paging) is never clipped.
    // The required width is measured at runtime instead of hardcoded so it stays correct for any UI culture.
    private void OnResultsToolbarSizeChanged(object sender, SizeChangedEventArgs eventArgs)
    {
        var toolbar = (FrameworkElement)sender;
        var nonToolbarWidth = ActualWidth - toolbar.ActualWidth;
        var neededWidth = toolbar.DesiredSize.Width + nonToolbarWidth;
        if (neededWidth > MinWidth)
            MinWidth = neededWidth;
    }

#pragma warning disable S2325
    private void OnOpenWithDefaultApp(object? sender, System.Windows.Input.MouseButtonEventArgs eventArgs)
#pragma warning restore S2325
    {
        if (eventArgs.Source is ContentControl { DataContext: DuplicateFile duplicateFile } && File.Exists(duplicateFile.FileFullName))
            Process.Start("explorer.exe", $"\"{duplicateFile.FileFullName}\"");
    }
}