using System;
using System.ComponentModel;
using System.Windows;
using System.Diagnostics;
using System.Windows.Controls;
using System.IO;

namespace DuplicateFileTool
{
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
        }

        private void OnWindowClosed(object sender, EventArgs e)
        {
            if (DataContext is not IDisposable disposable)
                return;
            try { disposable.Dispose(); }
            catch { /* ignore */ }
        }

        private void OnOpenWithDefaultApp(object sender, System.Windows.Input.MouseButtonEventArgs eventArgs)
        {
            if (eventArgs.Source is ContentControl contentControl && contentControl.DataContext is DuplicateFile duplicateFile && File.Exists(duplicateFile.FileFullName))
                Process.Start("explorer.exe", $"\"{duplicateFile.FileFullName}\"");
        }
    }
}

