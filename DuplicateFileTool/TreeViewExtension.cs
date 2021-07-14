using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DuplicateFileTool
{
    internal sealed class TreeViewResetEventArgs : EventArgs
    {
        public string TreeViewName { get; }

        public TreeViewResetEventArgs(string treeViewName)
        {
            TreeViewName = treeViewName;
        }
    }

    internal delegate void TreeViewResetHandler(object sender, TreeViewResetEventArgs eventArgs);

    internal class TreeViewExtension
    {
        private Window AppMainWindow { get; }

        public TreeViewExtension(Window appMainWindow)
        {
            AppMainWindow = appMainWindow;
        }

        public void ViewModelOnTreeViewReset(object sender, TreeViewResetEventArgs eventArgs)
        {
            var treeView = AppMainWindow.FindChild<TreeView>(eventArgs.TreeViewName);
            if (treeView == null)
                return;

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
