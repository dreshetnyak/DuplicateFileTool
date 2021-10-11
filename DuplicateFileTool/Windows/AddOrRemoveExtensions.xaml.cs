using System.Windows;

namespace DuplicateFileTool.Windows
{
    /// <summary>
    /// Interaction logic for AddOrRemoveExtensions.xaml
    /// </summary>
    public partial class AddOrRemoveExtensions : Window
    {
        public AddOrRemoveExtensions(AddOrRemoveExtensionsModelView modelView)
        {
            InitializeComponent();
            DataContext = modelView;
        }
    }
}
