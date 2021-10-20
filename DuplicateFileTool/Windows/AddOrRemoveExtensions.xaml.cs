using System.Windows;

namespace DuplicateFileTool.Windows
{
    /// <summary>
    /// Interaction logic for AddOrRemoveExtensions.xaml
    /// </summary>
    public partial class AddOrRemoveExtensions : Window
    {
        public AddOrRemoveExtensions(IAddOrRemoveExtensionsViewModel modelView)
        {
            InitializeComponent();
            DataContext = modelView;
            Owner = Application.Current.MainWindow;
        }
    }
}
