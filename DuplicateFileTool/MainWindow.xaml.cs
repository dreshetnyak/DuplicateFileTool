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
        var viewModel = new MainViewModel(ResultsTreeView);
        DataContext = viewModel;
        LoadResultsColumnWidths(viewModel.Config.ResultsConfig);
        Closed += OnWindowClosed;
        Activated += OnWindowActivated;
    }

    private void LoadResultsColumnWidths(Configuration.ResultsConfiguration resultsConfig)
    {
        ResultsNameColumn.Width = resultsConfig.NameColumnWidth.Value;
        ResultsSizeColumn.Width = resultsConfig.SizeColumnWidth.Value;
        ResultsModifiedColumn.Width = resultsConfig.ModifiedColumnWidth.Value;
    }

    private void SaveResultsColumnWidths(Configuration.ResultsConfiguration resultsConfig)
    {
        SaveColumnWidth(ResultsNameColumn, resultsConfig.NameColumnWidth);
        SaveColumnWidth(ResultsSizeColumn, resultsConfig.SizeColumnWidth);
        SaveColumnWidth(ResultsModifiedColumn, resultsConfig.ModifiedColumnWidth);
    }

    private static void SaveColumnWidth(GridViewColumn column, ConfigurationProperty<double> configProperty)
    {
        var width = double.IsNaN(column.Width) ? column.ActualWidth : column.Width;
        // Whole pixels round-trip through the config file the same way under every culture,
        // unlike fractional values whose decimal separator is culture-specific.
        width = Math.Round(width);
        if (width > 0)
            configProperty.Value = width;
    }

    private void OnWindowActivated(object? sender, EventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
            viewModel.RefreshExpandedFileTreeItems();
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        if (DataContext is not MainViewModel viewModel)
            return;
        try { SaveResultsColumnWidths(viewModel.Config.ResultsConfig); }
        catch { /* ignore */ }
        try { viewModel.Dispose(); }
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