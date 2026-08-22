using System.ComponentModel;
using System.Windows;
using System.Diagnostics;
using System.Windows.Controls;
using System.IO;
using AppResources = DuplicateFileTool.Properties.Resources;

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
        Closing += OnWindowClosing;
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
        // Whole pixels keep the settings stable and avoid meaningless fractional layout changes.
        width = Math.Round(width);
        if (width > 0)
            configProperty.Value = width;
    }

    private void OnWindowActivated(object? sender, EventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
            viewModel.RefreshExpandedFileTreeItems();
    }

    private void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel)
            return;

        try
        {
            SaveResultsColumnWidths(viewModel.Config.ResultsConfig);
            viewModel.SaveSettings();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                this,
                string.Format(AppResources.Ui_Settings_Save_Failed, App.GetSettingsErrorDetails(ex)),
                AppResources.Ui_Errors_Type_Error,
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        if (DataContext is not MainViewModel viewModel)
            return;

        try { viewModel.Dispose(); }
        catch { /* Window teardown cannot be recovered at this point. */ }
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

    // The folder-comparison row holds a star (resizable) height while the expander is open. Collapsing the
    // expander must shrink the row to the header instead of leaving the star space reserved, so we swap the row
    // to Auto on collapse and restore the previous height (including any GridSplitter drag) on expand. A Style
    // trigger cannot do this because the GridSplitter writes a local Height value that outranks Style setters.
    private GridLength _folderComparisonExpandedHeight = new(1, GridUnitType.Star);
    private double _folderComparisonExpandedMinHeight = 120;

    private void OnFolderComparisonCollapsed(object sender, RoutedEventArgs eventArgs)
    {
        if (!IsInitialized || !ReferenceEquals(eventArgs.OriginalSource, FolderComparisonExpander))
            return;
        _folderComparisonExpandedHeight = FolderComparisonRow.Height;
        _folderComparisonExpandedMinHeight = FolderComparisonRow.MinHeight;
        FolderComparisonRow.MinHeight = 0;
        FolderComparisonRow.Height = GridLength.Auto;
        FolderComparisonSplitter.Visibility = Visibility.Collapsed;
    }

    private void OnFolderComparisonExpanded(object sender, RoutedEventArgs eventArgs)
    {
        if (!IsInitialized || !ReferenceEquals(eventArgs.OriginalSource, FolderComparisonExpander))
            return;
        FolderComparisonRow.MinHeight = _folderComparisonExpandedMinHeight;
        FolderComparisonRow.Height = _folderComparisonExpandedHeight;
        FolderComparisonSplitter.Visibility = Visibility.Visible;
    }

#pragma warning disable S2325
    private void OnOpenWithDefaultApp(object? sender, System.Windows.Input.MouseButtonEventArgs eventArgs)
#pragma warning restore S2325
    {
        if (eventArgs.Source is ContentControl { DataContext: DuplicateFile duplicateFile } && File.Exists(duplicateFile.FileFullName))
            Process.Start("explorer.exe", $"\"{duplicateFile.FileFullName}\"");
    }
}
