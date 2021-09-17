using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DuplicateFileTool
{
    internal static class TreeViewExtension
    {
        public static void ResetView(this TreeView treeView)
        {
            if (VisualTreeHelper.GetChildrenCount(treeView) == 0)
                return;

            DependencyObject depObj;
            try { depObj = VisualTreeHelper.GetChild(treeView, 0); }
            catch { return; }
            var decorator = depObj as Decorator;
            if (decorator?.Child is not ScrollViewer scroll)
                return;

            scroll.ScrollToTop();
            scroll.ScrollToLeftEnd();
        }
    }
}
