using System.Windows;
using System.Windows.Controls;
using TreeView = System.Windows.Controls.TreeView;
using TreeViewItem = System.Windows.Controls.TreeViewItem;

namespace DuplicateFileTool.Controls;

/// <summary>
/// A TreeView that renders its items as rows of a multi-column table. The shared column collection
/// drives both the header row (GridViewHeaderRowPresenter in the control template) and every item row
/// (GridViewRowPresenter in the TreeListViewItem template). Styled in Controls/TreeListView.xaml.
/// </summary>
internal sealed class TreeListView : TreeView
{
    public GridViewColumnCollection Columns { get; } = [];

    protected override DependencyObject GetContainerForItemOverride() => new TreeListViewItem();
    protected override bool IsItemItsOwnContainerOverride(object item) => item is TreeListViewItem;
}

internal sealed class TreeListViewItem : TreeViewItem
{
    protected override DependencyObject GetContainerForItemOverride() => new TreeListViewItem();
    protected override bool IsItemItsOwnContainerOverride(object item) => item is TreeListViewItem;
}

/// <summary>
/// Selects a GridViewColumn cell template based on whether the row is a duplicate group or a file.
/// </summary>
internal sealed class DuplicateCellTemplateSelector : DataTemplateSelector
{
    public DataTemplate? GroupTemplate { get; set; }
    public DataTemplate? FileTemplate { get; set; }

    public override DataTemplate? SelectTemplate(object? item, DependencyObject container)
    {
        return item switch
        {
            DuplicateGroup => GroupTemplate,
            DuplicateFile => FileTemplate,
            _ => base.SelectTemplate(item, container)
        };
    }
}
