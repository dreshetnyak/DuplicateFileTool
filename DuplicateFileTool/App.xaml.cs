using System;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using DuplicateFileTool.Configuration;
using Button = System.Windows.Controls.Button;
using Clipboard = System.Windows.Clipboard;
using Grid = System.Windows.Controls.Grid;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using Orientation = System.Windows.Controls.Orientation;
using RowDefinition = System.Windows.Controls.RowDefinition;
using ScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility;
using StackPanel = System.Windows.Controls.StackPanel;
using TextBlock = System.Windows.Controls.TextBlock;
using TextBox = System.Windows.Controls.TextBox;
using VerticalAlignment = System.Windows.VerticalAlignment;

namespace DuplicateFileTool;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        var culture = new CultureInfo(FileAppConfig.Get($"{nameof(ProgramConfiguration)}.{nameof(ProgramConfiguration.SelectedCulture)}", "en"));
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
    }

    private void OnDispatcherUnhandledException(object? sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // Mark as handled so we suppress the default OS crash dialog and show our own first;
        // ShowCrashMessage shuts the app down once the user dismisses it.
        e.Handled = true;
        ShowCrashMessage(e.Exception, "UI thread (Dispatcher)");
    }

    private void OnDomainUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        ShowCrashMessage(e.ExceptionObject as Exception, $"AppDomain (terminating: {e.IsTerminating})");
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        ShowCrashMessage(e.Exception, "Unobserved Task");
        // Prevent the process from being torn down by the unobserved exception.
        e.SetObserved();
    }

    private static void ShowCrashMessage(Exception? exception, string source)
    {
        var message = BuildExceptionReport(exception, source);

        var dispatcher = Current?.Dispatcher;
        if (dispatcher != null && !dispatcher.CheckAccess())
            dispatcher.Invoke(() => ShowCrashWindow(message));
        else
            ShowCrashWindow(message);

        // The application state is unreliable after an unhandled exception, so close the app
        // once the user has had a chance to read and copy the error.
        ShutdownApp();
    }

    private static void ShutdownApp()
    {
        var dispatcher = Current?.Dispatcher;
        if (dispatcher != null && !dispatcher.CheckAccess())
            dispatcher.Invoke(() => Current?.Shutdown());
        else if (Current != null)
            Current.Shutdown();
        else
            Environment.Exit(1);
    }

    private static void ShowCrashWindow(string message)
    {
        var window = new Window
        {
            Title = "DuplicateFileTool - Unhandled Error",
            Width = 800,
            Height = 500,
            MinWidth = 400,
            MinHeight = 250,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            ShowInTaskbar = true
        };

        var owner = Current?.MainWindow;
        if (owner != null && owner != window && owner.IsVisible)
            window.Owner = owner;

        var grid = new Grid { Margin = new Thickness(10) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new TextBlock
        {
            Text = "An unhandled error occurred. You can select and copy the details below.",
            Margin = new Thickness(0, 0, 0, 8),
            TextWrapping = TextWrapping.Wrap
        };
        Grid.SetRow(header, 0);
        grid.Children.Add(header);

        var textBox = new TextBox
        {
            Text = message,
            IsReadOnly = true,
            IsReadOnlyCaretVisible = true,
            TextWrapping = TextWrapping.NoWrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            FontFamily = new System.Windows.Media.FontFamily("Consolas"),
            AcceptsReturn = true,
            VerticalContentAlignment = VerticalAlignment.Top
        };
        Grid.SetRow(textBox, 1);
        grid.Children.Add(textBox);

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 8, 0, 0)
        };

        var copyButton = new Button { Content = "Copy to Clipboard", Padding = new Thickness(12, 4, 12, 4), Margin = new Thickness(0, 0, 8, 0), MinWidth = 120 };
        copyButton.Click += (_, _) =>
        {
            try { Clipboard.SetText(message); }
            catch { /* Clipboard can intermittently be locked by another process; ignore. */ }
        };
        buttonPanel.Children.Add(copyButton);

        var closeButton = new Button { Content = "Close", Padding = new Thickness(12, 4, 12, 4), MinWidth = 80, IsDefault = true, IsCancel = true };
        closeButton.Click += (_, _) => window.Close();
        buttonPanel.Children.Add(closeButton);

        Grid.SetRow(buttonPanel, 2);
        grid.Children.Add(buttonPanel);

        window.Content = grid;
        window.ShowDialog();
    }

    private static string BuildExceptionReport(Exception? exception, string source)
    {
        var report = new StringBuilder();
        report.AppendLine($"Source: {source}");
        report.AppendLine();

        if (exception == null)
        {
            report.AppendLine("An unknown error occurred (no exception object was provided).");
            return report.ToString();
        }

        var current = exception;
        var level = 0;
        while (current != null)
        {
            var indent = level == 0 ? "" : new string(' ', level * 2);
            report.AppendLine($"{indent}{current.GetType().FullName}: {current.Message}");
            if (!string.IsNullOrWhiteSpace(current.StackTrace))
                report.AppendLine(current.StackTrace);
            report.AppendLine();
            current = current.InnerException;
            level++;
        }

        return report.ToString();
    }
}