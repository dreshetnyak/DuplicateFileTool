using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

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
    }
}
