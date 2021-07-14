using System.Windows;
using System.Windows.Media;

namespace DuplicateFileTool
{
    internal static class ViewExtensions
    {
        public static T FindChild<T>(this DependencyObject parent, string childName) where T : DependencyObject
        {
            if (parent == null)
                return null;

            for (var childIndex = 0; childIndex < VisualTreeHelper.GetChildrenCount(parent); childIndex++)
            {
                var childDependencyObject = VisualTreeHelper.GetChild(parent, childIndex);
                if (!(childDependencyObject is T child))
                {
                    var foundChild = FindChild<T>(childDependencyObject, childName);
                    if (foundChild != null)
                        return foundChild;
                }
                else
                {
                    if (childDependencyObject is FrameworkElement frameworkElement && frameworkElement.Name == childName)
                        return (T)childDependencyObject;
                }
            }

            return null;
        }
    }
}
