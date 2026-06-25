using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using GridSplitter = System.Windows.Controls.GridSplitter;
using ColumnDefinition = System.Windows.Controls.ColumnDefinition;
using Grid = System.Windows.Controls.Grid;
using GridResizeBehavior = System.Windows.Controls.GridResizeBehavior;
using GridResizeDirection = System.Windows.Controls.GridResizeDirection;

namespace DuplicateFileTool.Controls;

/// <summary>
/// The folder-comparison container: shows ONE <see cref="FolderComparisonItem"/> column per distinct containing
/// folder among the current group's files, side by side, separated by draggable vertical <see cref="GridSplitter"/>s,
/// horizontally scrollable, with a placeholder shown while no group is selected.
/// <para>
/// The host (issue 019) supplies the <see cref="Engine"/> dependency property and binds it to the view-model's engine.
/// The container watches the engine's <see cref="DuplicatesEngine.CurrentComparisonGroup"/> (set by issue 020) and
/// rebuilds its columns whenever it changes. The subscription is weak so a placed-and-forgotten container does not
/// pin the long-lived engine's handler list.
/// </para>
/// </summary>
public partial class FolderComparison : System.Windows.Controls.UserControl
{
    /// <summary>The minimum width a folder column may be dragged down to (pixels). Below this it would be unusable.</summary>
    private const double MinColumnWidth = 200;

    /// <summary>The width a folder column is created with (pixels). Equal-ish default sizing; not persisted (OQ-3).</summary>
    private const double DefaultColumnWidth = 300;

    /// <summary>The width of the vertical splitter between adjacent folder columns (pixels).</summary>
    private const double SplitterWidth = 5;

    /// <summary>Default expanded width of the rail column (pixels); the user can resize it with PART_RailSplitter.</summary>
    private const double RailExpandedWidth = 250;

    /// <summary>Floor on the expanded rail width (pixels) so the splitter can't drag the folder list down to nothing.</summary>
    private const double RailMinWidth = 150;

    /// <summary>How many distinct folders are selected (rendered as columns) by default on each (re)build. Fewer if the group has fewer.</summary>
    internal const int DefaultSelectedCount = 5;

    /// <summary>
    /// A large safety ceiling on how many folders may be selected (rendered as columns) at once, so a stray click
    /// cannot try to render hundreds of heavy columns. At the ceiling, UNSELECTED checkboxes are disabled (with the
    /// "limit reached" tooltip); selected ones stay enabled so they can still be unchecked. There is no hard cap for
    /// normal use and no "select all" affordance — the user owns the cost up to this ceiling.
    /// </summary>
    internal const int SafetyCeiling = 50;

    /// <summary>
    /// The duplicate-finding engine. Set by the host (issue 019) and propagated to every column. When it changes the
    /// container (re)subscribes weakly to it and rebuilds.
    /// </summary>
    public static readonly DependencyProperty EngineProperty = DependencyProperty.Register(
        nameof(Engine), typeof(DuplicatesEngine), typeof(FolderComparison),
        new PropertyMetadata(null, OnEngineChanged));

    internal DuplicatesEngine? Engine
    {
        get => (DuplicatesEngine?)GetValue(EngineProperty);
        set => SetValue(EngineProperty, value);
    }

    // The engine we are currently subscribed to, so a re-set of Engine detaches the previous weak handler's source.
    private DuplicatesEngine? _subscribedEngine;

    // The root FolderItems behind the columns built on the last rebuild. Kept so the next rebuild can cancel any
    // in-flight eager mark scan on each of them (OQ-5 group-change cancellation). Kept in sync with _slots.
    private readonly List<FolderItem> _roots = [];

    // The currently-shown columns, in display order, plus a normalized-path lookup. One ColumnSlot per rendered
    // FolderComparisonItem; the slot also tracks the trailing GridSplitter (none after the last column). The
    // incremental insert/remove (issue 050) does in-place ColumnDefinition surgery against this model and never
    // tears down the untouched items, so their tree expansion, scroll, and dragged width are preserved.
    private readonly List<ColumnSlot> _slots = [];
    private readonly Dictionary<string, ColumnSlot> _slotsByPath = new(System.StringComparer.OrdinalIgnoreCase);

    // The entries we have subscribed OnEntrySelectionChanged to, so Rebuild can detach the previous set before
    // building a new FolderEntries collection (entries are short-lived and re-created per group).
    private readonly List<FolderSelectionEntry> _subscribedEntries = [];

    /// <summary>
    /// The observable folder-selection model bound by the rail (issue 030+). One entry per distinct folder of the
    /// current group, in group order; rebuilt on every group change / post-deletion refresh, resetting to the default 5.
    /// Public (and the entry type below) so the rail's WPF data binding resolves it reliably — binding to non-public
    /// members can silently fail at runtime.
    /// </summary>
    public ObservableCollection<FolderSelectionEntry> FolderEntries { get; } = [];

    // Set while FolderEntries is bulk-built/reset so the (future, issue 050) per-entry selection-changed handler does
    // not fire mid-build. Real user toggles run with it clear, so they are never permanently suppressed.
    private bool _suppressSelection;

    // The expanded rail width (pixels), remembered across collapse/expand cycles so re-expanding restores the width the
    // user last dragged the rail to. While collapsed the rail column is Auto (the slim header strip); while expanded it
    // is set to this. See OnRailExpanded/OnRailCollapsed.
    private double _railWidth = RailExpandedWidth;

    public FolderComparison()
    {
        InitializeComponent();
    }

    private static void OnEngineChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (FolderComparison)d;
        control.SubscribeToEngine(oldEngine: e.OldValue as DuplicatesEngine);
        // Rebuild once when the engine is first set (or replaced); the current group may already be set.
        control.Rebuild();
    }

    /// <summary>
    /// (Re)subscribes weakly to the current <see cref="Engine"/>'s <see cref="INotifyPropertyChanged.PropertyChanged"/>,
    /// detaching any previous engine first. Weak so a stale container is collectable and stops being notified.
    /// </summary>
    private void SubscribeToEngine(DuplicatesEngine? oldEngine)
    {
        var engine = Engine;
        if (ReferenceEquals(engine, _subscribedEngine))
            return;

        var previous = oldEngine ?? _subscribedEngine;
        if (previous is not null)
            WeakEventManager<INotifyPropertyChanged, PropertyChangedEventArgs>.RemoveHandler(
                previous, nameof(INotifyPropertyChanged.PropertyChanged), OnEnginePropertyChanged);

        _subscribedEngine = engine;
        if (engine is null)
            return;

        WeakEventManager<INotifyPropertyChanged, PropertyChangedEventArgs>.AddHandler(
            engine, nameof(INotifyPropertyChanged.PropertyChanged), OnEnginePropertyChanged);
    }

    private void OnEnginePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // The current comparison group (issue 020) drives which folders are shown, so a group switch rebuilds.
        if (e.PropertyName == nameof(DuplicatesEngine.CurrentComparisonGroup))
            Rebuild();
    }

    /// <summary>
    /// Tears down the previous columns and builds one <see cref="FolderComparisonItem"/> per distinct containing
    /// folder of the current group's files, or shows the placeholder when there is no group / no surviving folder.
    /// </summary>
    private void Rebuild()
    {
        // Cancel any in-flight eager mark scan on the roots from the previous build (OQ-5): they are about to be
        // discarded, and a scan completing afterwards would commit marks for a folder no longer shown.
        foreach (var root in _roots)
            root.CancelPendingScan();
        _roots.Clear();
        _slots.Clear();
        _slotsByPath.Clear();

        // Detach the per-entry selection handlers from the previous group's entries before they are replaced; the new
        // FolderEntries built below get fresh subscriptions. Without this, stale entries would keep firing.
        foreach (var entry in _subscribedEntries)
            entry.PropertyChanged -= OnEntryPropertyChanged;
        _subscribedEntries.Clear();

        PART_Columns.Children.Clear();
        PART_Columns.ColumnDefinitions.Clear();

        // The rail starts collapsed on every (re)build — group switch or post-deletion refresh (US-35, no persistence).
        // Checkbox toggles do not pass through Rebuild (they route via OnEntryPropertyChanged → Insert/RemoveColumn), so
        // they never collapse the rail underneath the user.
        PART_RailExpander.IsExpanded = false;

        var engine = Engine;
        var group = engine?.CurrentComparisonGroup;
        if (engine is null || group is null)
        {
            FolderEntries.Clear();
            UpdateColumnsAreaState(); // no current group -> placeholder, rail hidden.
            return;
        }

        // Distinct containing folders, case-insensitively (same-folder duplicates collapse to one column), in the
        // group's own order (first-appearance over the full-path-sorted DuplicateFiles), so the column order matches
        // the results-tree order. The pure helper is the highest test seam and is reused by the rail.
        var distinctFolders = GroupFolders.OrderedDistinct(group.DuplicateFiles.Select(file => file.FileData.DirPath));

        // Rebuild the rail's selection model (one entry per folder, in order) and select the default first-N, bounding
        // the automatic cost: only the selected slice is rendered as columns. The guard keeps the (issue 050) per-entry
        // handler from firing during the bulk reset; it is cleared in finally so real user toggles still take effect.
        var defaultSelectedCount = System.Math.Min(DefaultSelectedCount, distinctFolders.Count);
        _suppressSelection = true;
        try
        {
            FolderEntries.Clear();
            for (var i = 0; i < distinctFolders.Count; i++)
            {
                var folder = distinctFolders[i];
                var entry = new FolderSelectionEntry(folder, DeletionSelection.Normalize(folder))
                {
                    IsSelected = i < defaultSelectedCount
                };
                // Subscribe so a real user toggle (after the guard clears) inserts/removes exactly that one column.
                entry.PropertyChanged += OnEntryPropertyChanged;
                _subscribedEntries.Add(entry);
                FolderEntries.Add(entry);
            }
        }
        finally
        {
            _suppressSelection = false;
        }

        var selectedFolders = FolderEntries.Where(entry => entry.IsSelected).Select(entry => entry.DisplayPath).ToList();
        BuildColumns(engine, selectedFolders);

        // Apply the safety ceiling to the freshly-built entries (the default-5 build is below the ceiling, but a group
        // with exactly SafetyCeiling default-selected folders would already be at it). Flips IsEnabled only.
        EnforceSafetyCeiling();
    }

    /// <summary>
    /// Builds the folder columns from a list of distinct folder paths, populating the <see cref="_slots"/> model
    /// (and <see cref="_roots"/>) that the incremental insert/remove (issue 050) maintains in place afterwards. Kept
    /// as its own method that takes the folder list so a future pager can feed it a page slice without reworking the
    /// rebuild trigger (pager is out of scope).
    /// </summary>
    private void BuildColumns(DuplicatesEngine engine, IReadOnlyList<string> folderPaths)
    {
        var slots = new List<ColumnSlot>();
        foreach (var folderPath in folderPaths)
        {
            var fd = FileSystem.GetFileData(folderPath);
            if (fd.IsEmpty) // the folder is gone since the search; skip it.
                continue;

            var root = new FolderItem(fd, engine) { IsExpanded = true }; // eagerly load the folder's contents.
            var item = new FolderComparisonItem { Engine = engine, Root = root };
            slots.Add(new ColumnSlot(DeletionSelection.Normalize(folderPath), item, root));
        }

        if (slots.Count == 0)
        {
            // A group is selected but nothing survived (all chosen folders gone since the search): empty-selection state,
            // not the no-group placeholder. UpdateColumnsAreaState distinguishes the two from FolderEntries / the group.
            UpdateColumnsAreaState();
            return;
        }

        // Pixel widths (not star) so the columns' total can exceed the viewport and the horizontal scrollbar appears;
        // a GridSplitter then resizes the adjacent pixel widths. Layout per folder: [folder col][Auto splitter col],
        // and a single zero-width spacer column at the very end (added after the loop) so the LAST column's trailing
        // splitter has a "next" definition to resize against.
        for (var i = 0; i < slots.Count; i++)
        {
            var folderColumn = PART_Columns.ColumnDefinitions.Count;
            PART_Columns.ColumnDefinitions.Add(NewFolderColumnDefinition());

            var slot = slots[i];
            Grid.SetColumn(slot.Item, folderColumn);
            PART_Columns.Children.Add(slot.Item);

            // A trailing splitter on every column, the last included: each resizes the column to its left. The last
            // one resizes against the tail spacer added after the loop, so the last column is adjustable too.
            PART_Columns.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var splitter = NewSplitter();
            Grid.SetColumn(splitter, folderColumn + 1);
            PART_Columns.Children.Add(splitter);
            slot.Splitter = splitter;

            _slots.Add(slot);
            _slotsByPath[slot.NormalizedPath] = slot;
            _roots.Add(slot.Root);
        }

        PART_Columns.ColumnDefinitions.Add(NewTrailingSpacerColumn());

        UpdateColumnsAreaState();
    }

    // Fired by any FolderSelectionEntry property change; only IsSelected drives column add/remove. The _suppressSelection
    // guard skips changes made during the bulk (re)build so it does not fight the full build in Rebuild/BuildColumns.
    private void OnEntryPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_suppressSelection || e.PropertyName != nameof(FolderSelectionEntry.IsSelected))
            return;
        if (sender is FolderSelectionEntry entry)
            OnEntrySelectionChanged(entry);
    }

    /// <summary>
    /// A user toggled a rail checkbox: now-selected → insert exactly that folder's column at its group-order position;
    /// now-unselected → remove exactly that column (and its adjacent splitter). The other columns are never torn down.
    /// </summary>
    private void OnEntrySelectionChanged(FolderSelectionEntry entry)
    {
        if (entry.IsSelected)
            InsertColumn(entry);
        else
            RemoveColumn(entry);

        EnforceSafetyCeiling();
    }

    /// <summary>
    /// Enforces the <see cref="SafetyCeiling"/>: at the ceiling, every UNSELECTED entry is disabled (so its checkbox
    /// cannot be checked, surfacing the "limit reached" tooltip); below the ceiling, all entries are enabled. Selected
    /// entries always stay enabled so they can still be unchecked. This flips only <see cref="FolderSelectionEntry.IsEnabled"/>,
    /// never <see cref="FolderSelectionEntry.IsSelected"/>, so it cannot re-trigger column insert/remove.
    /// </summary>
    private void EnforceSafetyCeiling()
    {
        var atCeiling = FolderEntries.Count(entry => entry.IsSelected) == SafetyCeiling;
        foreach (var entry in FolderEntries)
            entry.IsEnabled = entry.IsSelected || !atCeiling;
    }

    /// <summary>
    /// Inserts exactly one folder column for <paramref name="entry"/> at its position in the group order, by in-place
    /// <see cref="ColumnDefinition"/> insertion and <see cref="Grid.Column"/> renumbering of the retained children —
    /// never touching the other items' instances. Mirrors <see cref="BuildColumns"/>' gone-folder guard: if the folder
    /// has disappeared since the search the entry's selection is reverted (under the suppression guard) so the checkbox
    /// state and the rendered columns stay in agreement.
    /// </summary>
    private void InsertColumn(FolderSelectionEntry entry)
    {
        var engine = Engine;
        if (engine is null || _slotsByPath.ContainsKey(entry.NormalizedPath))
            return;

        var fd = FileSystem.GetFileData(entry.DisplayPath);
        if (fd.IsEmpty)
        {
            // The folder is gone since the search; do not create a slot. Revert the checkbox under the guard so the
            // handler does not re-enter, keeping the rail state and the rendered columns in agreement (US-34).
            _suppressSelection = true;
            try { entry.IsSelected = false; }
            finally { _suppressSelection = false; }
            return;
        }

        var root = new FolderItem(fd, engine) { IsExpanded = true }; // eagerly load the folder's contents.
        var item = new FolderComparisonItem { Engine = engine, Root = root };
        var slot = new ColumnSlot(entry.NormalizedPath, item, root);

        // The insertion index OVER THE ACTUALLY-SHOWN SLOTS: how many currently-shown folders precede this one in the
        // canonical group order (FolderEntries is already in GroupFolders.OrderedDistinct order).
        var canonicalIndex = IndexInCanonicalOrder(entry.NormalizedPath);
        var insertAt = _slots.Count(s => IndexInCanonicalOrder(s.NormalizedPath) < canonicalIndex);

        if (_slots.Count == 0)
        {
            // First-ever column: its folder column + trailing splitter, then the tail spacer the splitter resizes
            // against (so even the sole column is adjustable).
            PART_Columns.ColumnDefinitions.Add(NewFolderColumnDefinition());
            Grid.SetColumn(item, 0);
            PART_Columns.Children.Add(item);

            PART_Columns.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var splitter = NewSplitter();
            Grid.SetColumn(splitter, 1);
            PART_Columns.Children.Add(splitter);
            slot.Splitter = splitter;

            PART_Columns.ColumnDefinitions.Add(NewTrailingSpacerColumn());
        }
        else if (insertAt == _slots.Count)
        {
            // Append at the end: insert the new folder column + its trailing splitter just BEFORE the tail spacer
            // (which stays last). Layout grows by [folder][splitter]; no child renumber needed — the spacer has no
            // child and nothing else sits at/after the insert point. The previously-last splitter now resizes against
            // this new column instead of the spacer.
            var spacerIndex = PART_Columns.ColumnDefinitions.Count - 1; // the trailing spacer is always last.
            PART_Columns.ColumnDefinitions.Insert(spacerIndex, NewFolderColumnDefinition());
            PART_Columns.ColumnDefinitions.Insert(spacerIndex + 1, new ColumnDefinition { Width = GridLength.Auto });

            Grid.SetColumn(item, spacerIndex);
            PART_Columns.Children.Add(item);
            var splitter = NewSplitter();
            Grid.SetColumn(splitter, spacerIndex + 1);
            PART_Columns.Children.Add(splitter);
            slot.Splitter = splitter;
        }
        else
        {
            // Insert before an existing column: bump the retained children at/after the target by 2, insert the two
            // [folder][splitter] ColumnDefinitions at the right index, place the new folder + its trailing splitter.
            var folderColumnIndex = Grid.GetColumn(_slots[insertAt].Item);
            foreach (var child in PART_Columns.Children.OfType<UIElement>())
            {
                if (Grid.GetColumn(child) >= folderColumnIndex)
                    Grid.SetColumn(child, Grid.GetColumn(child) + 2);
            }

            PART_Columns.ColumnDefinitions.Insert(folderColumnIndex, NewFolderColumnDefinition());
            PART_Columns.ColumnDefinitions.Insert(folderColumnIndex + 1, new ColumnDefinition { Width = GridLength.Auto });

            Grid.SetColumn(item, folderColumnIndex);
            PART_Columns.Children.Add(item);
            var splitter = NewSplitter();
            Grid.SetColumn(splitter, folderColumnIndex + 1);
            PART_Columns.Children.Add(splitter);
            slot.Splitter = splitter;
        }

        _slots.Insert(insertAt, slot);
        _slotsByPath[slot.NormalizedPath] = slot;
        _roots.Add(slot.Root);

        UpdateColumnsAreaState(); // now >=1 shown: leave the empty-selection state for the columns.
    }

    /// <summary>
    /// Removes exactly the folder column for <paramref name="entry"/> (and its adjacent splitter) by in-place
    /// <see cref="ColumnDefinition"/> removal and <see cref="Grid.Column"/> renumbering of the retained children —
    /// never touching the other items' instances. Cancels only the root's uncommitted mark-scan; existing deletion
    /// marks are never removed.
    /// </summary>
    private void RemoveColumn(FolderSelectionEntry entry)
    {
        if (!_slotsByPath.TryGetValue(entry.NormalizedPath, out var slot))
            return;

        var index = _slots.IndexOf(slot);
        slot.Root.CancelPendingScan(); // aborts only an uncommitted mark-scan; commits nothing; removes no existing marks.

        var folderColumnIndex = Grid.GetColumn(slot.Item);

        if (_slots.Count == 1)
        {
            // The only column: clear the folder column, its trailing splitter, and the tail spacer in one go.
            // UpdateColumnsAreaState() below then routes to the "no folders selected" empty state.
            PART_Columns.Children.Remove(slot.Item);
            if (slot.Splitter is not null)
                PART_Columns.Children.Remove(slot.Splitter);
            PART_Columns.ColumnDefinitions.Clear();
        }
        else
        {
            // N>=2: every column owns a TRAILING splitter, so removal is uniform — drop the item + its splitter and
            // their two ColumnDefinitions ([folder][splitter]). The tail spacer stays last (it has no child). Only an
            // interior removal needs the children after the gap renumbered down by 2; removing the last column does
            // not (nothing with a child sits to its right — only the spacer).
            var isLast = index == _slots.Count - 1;
            PART_Columns.Children.Remove(slot.Item);
            if (slot.Splitter is not null)
                PART_Columns.Children.Remove(slot.Splitter);
            PART_Columns.ColumnDefinitions.RemoveAt(folderColumnIndex + 1); // the trailing splitter column.
            PART_Columns.ColumnDefinitions.RemoveAt(folderColumnIndex);     // the folder column.

            if (!isLast)
            {
                foreach (var child in PART_Columns.Children.OfType<UIElement>())
                {
                    if (Grid.GetColumn(child) > folderColumnIndex)
                        Grid.SetColumn(child, Grid.GetColumn(child) - 2);
                }
            }
        }

        _slots.RemoveAt(index);
        _slotsByPath.Remove(slot.NormalizedPath);
        _roots.Remove(slot.Root);

        UpdateColumnsAreaState(); // if that was the last shown column, switch to the "no folders selected" empty state.
    }

    /// <summary>The position of <paramref name="normalizedPath"/> in the canonical group order (the FolderEntries order).</summary>
    private int IndexInCanonicalOrder(string normalizedPath)
    {
        for (var i = 0; i < FolderEntries.Count; i++)
        {
            if (System.StringComparer.OrdinalIgnoreCase.Equals(FolderEntries[i].NormalizedPath, normalizedPath))
                return i;
        }
        return FolderEntries.Count; // not found: treat as last (defensive; entries always contain shown folders).
    }

    private static ColumnDefinition NewFolderColumnDefinition() => new()
    {
        Width = new GridLength(DefaultColumnWidth, GridUnitType.Pixel),
        MinWidth = MinColumnWidth
    };

    /// <summary>
    /// A zero-width spacer column kept at the tail of <see cref="PART_Columns"/> so the LAST folder column's trailing
    /// splitter has a "next" definition to resize against — a <see cref="GridSplitter"/> only activates with a column
    /// definition on each side. Dragging that splitter widens the last column and merely pushes this (invisible) spacer.
    /// </summary>
    private static ColumnDefinition NewTrailingSpacerColumn() => new() { Width = new GridLength(0) };

    private static GridSplitter NewSplitter()
    {
        var splitter = new GridSplitter
        {
            Width = SplitterWidth,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
            VerticalAlignment = System.Windows.VerticalAlignment.Stretch,
            ResizeBehavior = GridResizeBehavior.PreviousAndNext,
            ResizeDirection = GridResizeDirection.Columns
        };
        // Match the XAML splitters (MainWindow.xaml): paint them with the window brush instead of the
        // default grayish control background. DynamicResource so it tracks theme/system-color changes.
        splitter.SetResourceReference(System.Windows.Controls.Control.BackgroundProperty, System.Windows.SystemColors.WindowBrushKey);
        return splitter;
    }

    /// <summary>
    /// Selects the column-area's three mutually-exclusive states (issue 070) and the rail's visibility, from the current
    /// group and the shown-column count, after every (re)build, insert, and remove:
    /// <list type="bullet">
    /// <item><b>No current group</b> → the existing <c>PART_Placeholder</c> ("no group selected"); the rail column is
    /// hidden (width 0, <c>Collapsed</c>) so the empty state stays clear.</item>
    /// <item><b>Group, zero folders shown</b> → the distinct <c>PART_EmptySelection</c> ("no folders selected"); the rail
    /// stays visible so the user can re-check a folder.</item>
    /// <item><b>Group, ≥1 folder shown</b> → the columns (<c>PART_Scroll</c>); the rail stays visible.</item>
    /// </list>
    /// </summary>
    /// <summary>
    /// Expanding the rail switches its column from Auto (the slim header strip) to a resizable pixel width, restoring the
    /// width the user last dragged it to (<see cref="RailExpandedWidth"/> on first use). PART_RailSplitter — enabled only
    /// while expanded (XAML binding) — then adjusts this width.
    /// </summary>
    private void OnRailExpanded(object sender, RoutedEventArgs e)
    {
        PART_RailColumn.MinWidth = RailMinWidth;
        PART_RailColumn.Width = new GridLength(_railWidth);
    }

    /// <summary>
    /// Collapsing the rail remembers the current (possibly dragged) width and returns the column to Auto, so the
    /// collapsed rail is just the slim Expander header strip again and the disabled splitter cannot resize it. The
    /// MinWidth floor is also cleared so Auto can shrink to the header strip (and to 0 when the rail is hidden).
    /// </summary>
    private void OnRailCollapsed(object sender, RoutedEventArgs e)
    {
        if (PART_RailColumn.Width.IsAbsolute)
            _railWidth = PART_RailColumn.Width.Value;
        PART_RailColumn.MinWidth = 0;
        PART_RailColumn.Width = GridLength.Auto;
    }

    private void UpdateColumnsAreaState()
    {
        var hasGroup = Engine?.CurrentComparisonGroup is not null;
        var hasColumns = _slots.Count > 0;

        // The rail only makes sense when a group is selected; with no group the rail Border is hidden, so its Auto-width
        // column 0 collapses to 0. With a group it stays visible at the user's current expand state — a checkbox toggle
        // (which routes here via InsertColumn/RemoveColumn) must not collapse the Expander shut underneath the user.
        PART_Rail.Visibility = hasGroup ? Visibility.Visible : Visibility.Collapsed;

        PART_Scroll.Visibility = hasGroup && hasColumns ? Visibility.Visible : Visibility.Collapsed;
        PART_Placeholder.Visibility = hasGroup ? Visibility.Collapsed : Visibility.Visible;
        PART_EmptySelection.Visibility = hasGroup && !hasColumns ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// One currently-shown folder column: its rendered <see cref="FolderComparisonItem"/>, the column's root
    /// <see cref="FolderItem"/>, and the trailing <see cref="GridSplitter"/> to this column's right. Every column has
    /// one; the last column's resizes against the tail spacer column. The incremental insert/remove (issue 050)
    /// maintains these in place so the untouched items are never re-added, preserving their tree expansion, scroll,
    /// and dragged width.
    /// </summary>
    private sealed class ColumnSlot
    {
        public string NormalizedPath { get; }
        public FolderComparisonItem Item { get; }
        public FolderItem Root { get; }
        public GridSplitter? Splitter { get; set; }

        public ColumnSlot(string normalizedPath, FolderComparisonItem item, FolderItem root)
        {
            NormalizedPath = normalizedPath;
            Item = item;
            Root = root;
        }
    }
}

/// <summary>
/// One observable folder entry backing the selection rail: a distinct containing folder of the current group, in the
/// group's own order. <see cref="IsSelected"/> is the single source of truth the rendered columns derive from, so the
/// rail list and the columns can never disagree. <see cref="IsEnabled"/> backs the safety ceiling (issue 060).
/// <para>
/// Public so the rail's WPF data binding resolves its bound properties reliably; binding to a non-public type can
/// silently fail at runtime even though it compiles.
/// </para>
/// </summary>
public sealed class FolderSelectionEntry : INotifyPropertyChanged
{
    private bool _isSelected;
    private bool _isEnabled = true;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string propertyName = "") =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    /// <summary>The folder path as it appears on disk (the first real, un-normalized path seen for this folder).</summary>
    public string DisplayPath { get; }

    /// <summary>The normalized key (case-insensitive, long-path tolerant) used to match this entry to its column.</summary>
    public string NormalizedPath { get; }

    /// <summary>Whether this folder is rendered as a column. Toggling it drives column add/remove (issue 050).</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
                return;
            _isSelected = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Whether this folder's checkbox can be toggled. Cleared on unselected entries at the safety ceiling (issue 060).</summary>
    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (_isEnabled == value)
                return;
            _isEnabled = value;
            OnPropertyChanged();
        }
    }

    public FolderSelectionEntry(string displayPath, string normalizedPath)
    {
        DisplayPath = displayPath;
        NormalizedPath = normalizedPath;
    }
}
