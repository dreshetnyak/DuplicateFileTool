using System.ComponentModel;
using System.Windows;

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
            DataContext = new MainViewModel(this);
        }
    }
}
