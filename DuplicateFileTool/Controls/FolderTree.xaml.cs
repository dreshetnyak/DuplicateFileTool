using System.Collections;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using DuplicateFileTool.Converters;

namespace DuplicateFileTool.Controls;

/// <summary>
/// Renders a <see cref="FolderItem"/> tree as a multi-column table (mark | Name | Size | Last-Modified)
/// using the shared <see cref="TreeListView"/>. The host (issue 016) binds <see cref="ItemsSource"/> to a
/// root folder's children. Marking is toggled through a code-behind Click handler rather than a two-way
/// bound control because a directory's <see cref="FolderItem.IsMarkedForDeletion"/> setter starts an async
/// subtree scan and its getter only flips after that scan commits — a two-way control would visually revert.
/// </summary>
public partial class FolderTree : System.Windows.Controls.UserControl
{
    /// <summary>
    /// The folder nodes to render. The inner <see cref="TreeListView"/>'s ItemsSource binds to this, so a host
    /// can bind it to a root folder's <see cref="FolderItem.Children"/>.
    /// </summary>
    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource), typeof(IEnumerable), typeof(FolderTree), new PropertyMetadata(null));

    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public FolderTree()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Toggles the row's mark-for-deletion state. For a file this is immediate; for a directory the setter
    /// starts an eager background subtree scan that commits (and notifies) on completion.
    /// </summary>
    private void OnToggleMark(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: FolderItem item })
            item.IsMarkedForDeletion = !item.IsMarkedForDeletion;
    }
}

/// <summary>
/// Formats a file size (a <see cref="long"/> byte count) for the Size column using the shared
/// <see cref="DataConversion.BytesLengthToString"/> helper. One-way only.
/// </summary>
internal sealed class BytesLengthConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is long length ? length.BytesLengthToString() : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>
/// Converts a <see cref="FolderItem.Level"/> to a left-margin <see cref="Thickness"/> so each Name cell is
/// indented by its depth, giving the folder tree a standard hierarchical indent. The shared
/// <see cref="TreeListViewItem"/> template does not indent rows (to keep the columns aligned), so the indent is
/// applied here, inside the first column's cell. The column root is level 0 and never shown; its children (the
/// top-level rows) are level 1 and sit flush-left, so the indent is <c>(level - 1) * step</c>. One-way only.
/// </summary>
internal sealed class LevelToIndentConverter : IValueConverter
{
    private const double IndentPerLevel = 16d;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var level = value is int i ? i : 0;
        var depth = level > 0 ? level - 1 : 0;
        return new Thickness(depth * IndentPerLevel, 0, 0, 0);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
