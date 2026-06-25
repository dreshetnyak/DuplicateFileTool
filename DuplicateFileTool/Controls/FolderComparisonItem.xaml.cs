using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Windows;

namespace DuplicateFileTool.Controls;

/// <summary>
/// Represents ONE folder column in the folder comparison: a header (the folder path, clipped with a full-path
/// tooltip, plus a clear button), the <see cref="FolderTree"/> rendering the folder's contents, and a warning
/// line shown only while a zero-survivor file is marked anywhere in this folder's subtree.
/// <para>
/// The host (issue 018) supplies the data through two dependency properties: <see cref="Root"/> (the root
/// <see cref="FolderItem"/> for the folder) and <see cref="Engine"/> (the <see cref="DuplicatesEngine"/>). The
/// control's <see cref="FrameworkElement.DataContext"/> is set to <see cref="Root"/>, so the header binds
/// <c>{Binding FullName}</c> and the tree binds <c>{Binding Children}</c>.
/// </para>
/// </summary>
public partial class FolderComparisonItem : System.Windows.Controls.UserControl
{
    /// <summary>
    /// The root <see cref="FolderItem"/> for this folder column (its <see cref="FolderItem.Children"/> are the
    /// rendered contents). Set by the host (issue 018). Setting it also drives the control's DataContext.
    /// </summary>
    public static readonly DependencyProperty RootProperty = DependencyProperty.Register(
        nameof(Root), typeof(FolderItem), typeof(FolderComparisonItem),
        new PropertyMetadata(null, OnRootOrEngineChanged));

    internal FolderItem? Root
    {
        get => (FolderItem?)GetValue(RootProperty);
        set => SetValue(RootProperty, value);
    }

    /// <summary>
    /// The duplicate-finding engine, source of the deletion selection and the zero-survivor / current-group
    /// classification. Set by the host (issue 018).
    /// </summary>
    public static readonly DependencyProperty EngineProperty = DependencyProperty.Register(
        nameof(Engine), typeof(DuplicatesEngine), typeof(FolderComparisonItem),
        new PropertyMetadata(null, OnRootOrEngineChanged));

    internal DuplicatesEngine? Engine
    {
        get => (DuplicatesEngine?)GetValue(EngineProperty);
        set => SetValue(EngineProperty, value);
    }

    private static readonly DependencyPropertyKey ShowWarningPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(ShowWarning), typeof(bool), typeof(FolderComparisonItem), new PropertyMetadata(false));

    /// <summary>
    /// True while any file under this folder's subtree is marked AND deleting the selection would leave no
    /// surviving copy of it (a zero-survivor). Drives the warning line's visibility; recomputed live whenever the
    /// deletion selection changes or the engine's current comparison group changes.
    /// </summary>
    public static readonly DependencyProperty ShowWarningProperty = ShowWarningPropertyKey.DependencyProperty;

    public bool ShowWarning => (bool)GetValue(ShowWarningProperty);

    private static readonly DependencyPropertyKey IsSelectedColumnPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(IsSelectedColumn), typeof(bool), typeof(FolderComparisonItem), new PropertyMetadata(false));

    /// <summary>
    /// True while this folder column corresponds to the results-tree row currently selected: the engine's
    /// <see cref="DuplicatesEngine.SelectedDuplicateFilePath"/> is non-null and its directory equals this column's
    /// <see cref="Root"/> folder (case-insensitive). Drives the outer-background highlight; recomputed when
    /// <see cref="Root"/>/<see cref="Engine"/> are set and on a weak engine notification for SelectedDuplicateFilePath.
    /// </summary>
    public static readonly DependencyProperty IsSelectedColumnProperty = IsSelectedColumnPropertyKey.DependencyProperty;

    public bool IsSelectedColumn => (bool)GetValue(IsSelectedColumnProperty);

    private static readonly DependencyPropertyKey HasSelectionPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(HasSelection), typeof(bool), typeof(FolderComparisonItem), new PropertyMetadata(false));

    /// <summary>
    /// True while at least one file under this folder's subtree (or the folder path itself) is in the engine's
    /// deletion selection — i.e. the clear-folder button has something to clear. (A directory cannot be in the
    /// selection without its descendant files, so the file set alone answers this.) Drives that button's
    /// <see cref="UIElement.IsEnabled"/>, which in turn swaps its image to the gray variant; recomputed alongside
    /// <see cref="ShowWarning"/> whenever the deletion selection changes.
    /// </summary>
    public static readonly DependencyProperty HasSelectionProperty = HasSelectionPropertyKey.DependencyProperty;

    public bool HasSelection => (bool)GetValue(HasSelectionProperty);

    public FolderComparisonItem()
    {
        InitializeComponent();
    }

    private static void OnRootOrEngineChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (FolderComparisonItem)d;

        // The header and tree bind through the DataContext (= the root folder), so keep it in sync with Root.
        if (e.Property == RootProperty)
            control.DataContext = control.Root;

        control.SubscribeToEngine(oldEngine: e.Property == EngineProperty ? e.OldValue as DuplicatesEngine : null);
        control.RecomputeSelectionState();
        // A column created right after a selection (selecting a file rebuilds the control) must highlight immediately
        // from the persisted SelectedDuplicateFilePath.
        control.RecomputeIsSelectedColumn();
    }

    // The engine we are currently subscribed to, so a re-set of Engine can detach the previous weak handlers' source
    // (WeakEventManager dedupes by source+handler, so re-adding for the same source is harmless, but a new engine
    // needs a fresh subscription and the old one removed).
    private DuplicatesEngine? _subscribedEngine;

    /// <summary>
    /// (Re)subscribes weakly to the current <see cref="Engine"/>'s <see cref="DeletionSelection.Changed"/> and
    /// <see cref="INotifyPropertyChanged.PropertyChanged"/>, detaching any previous engine first. Weak subscriptions
    /// (mirroring <see cref="FolderItem"/>/DuplicateFile) keep a rebuilt column from pinning the long-lived engine's
    /// handler list with stale controls.
    /// </summary>
    private void SubscribeToEngine(DuplicatesEngine? oldEngine)
    {
        var engine = Engine;
        if (ReferenceEquals(engine, _subscribedEngine))
            return;

        var previous = oldEngine ?? _subscribedEngine;
        if (previous is not null)
        {
            WeakEventManager<DeletionSelection, DeletionSelectionChangedEventArgs>.RemoveHandler(
                previous.DeletionSelection, nameof(DeletionSelection.Changed), OnDeletionSelectionChanged);
            WeakEventManager<INotifyPropertyChanged, PropertyChangedEventArgs>.RemoveHandler(
                previous, nameof(INotifyPropertyChanged.PropertyChanged), OnEnginePropertyChanged);
        }

        _subscribedEngine = engine;
        if (engine is null)
            return;

        WeakEventManager<DeletionSelection, DeletionSelectionChangedEventArgs>.AddHandler(
            engine.DeletionSelection, nameof(DeletionSelection.Changed), OnDeletionSelectionChanged);
        WeakEventManager<INotifyPropertyChanged, PropertyChangedEventArgs>.AddHandler(
            engine, nameof(INotifyPropertyChanged.PropertyChanged), OnEnginePropertyChanged);
    }

    // Coalescing gates for the marshaled recomputes (0 = none queued, 1 = a recompute is already queued on the
    // dispatcher). One per recompute so a queued selection-state recompute never swallows a pending IsSelectedColumn.
    private int _selectionStateQueued;
    private int _isSelectedColumnQueued;

    private void OnDeletionSelectionChanged(object? sender, DeletionSelectionChangedEventArgs e) =>
        QueueRecomputeSelectionState();

    private void OnEnginePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // A duplicate's zero-survivor verdict depends on the current group, so a group switch can flip the warning.
        if (e.PropertyName == nameof(DuplicatesEngine.CurrentComparisonGroup))
            QueueRecomputeSelectionState();
        // Moving the results-tree selection moves the outer-background highlight to the new file's folder column,
        // without rebuilding the control (selection within the same group does not rebuild).
        else if (e.PropertyName == nameof(DuplicatesEngine.SelectedDuplicateFilePath))
            QueueRecomputeIsSelectedColumn();
    }

    /// <summary>
    /// Runs <see cref="RecomputeSelectionState"/> on the UI thread. The engine/selection change events can arrive on a
    /// BACKGROUND thread — Auto Select marks files inside a <c>Task.Run</c>, so <see cref="DeletionSelection.Changed"/>
    /// fires off-thread — and the recompute reads <see cref="Root"/>/<see cref="Engine"/> and calls <c>SetValue</c> on
    /// dependency properties, which have UI-thread affinity (off-thread access throws "the calling thread cannot access
    /// this object"). When already on the UI thread we run inline (a true no-op for the existing UI-thread paths);
    /// otherwise we marshal. The test-and-set gate coalesces an Auto Select burst of thousands of <c>Changed</c> events
    /// into a single recompute: the flag is cleared inside the dispatched callback BEFORE it reads the live selection,
    /// so a mutation landing after the clear re-queues and no final state is missed (the recompute reads the whole live
    /// selection, not a per-event delta, so coalescing is lossless).
    /// </summary>
    private void QueueRecomputeSelectionState()
    {
        if (Dispatcher.CheckAccess())
        {
            RecomputeSelectionState();
            return;
        }

        if (Interlocked.Exchange(ref _selectionStateQueued, 1) != 0)
            return; // a recompute is already queued; it will pick up the latest state.

        Dispatcher.BeginInvoke(() =>
        {
            Interlocked.Exchange(ref _selectionStateQueued, 0);
            RecomputeSelectionState();
        });
    }

    /// <summary>
    /// Marshals <see cref="RecomputeIsSelectedColumn"/> to the UI thread, mirroring <see cref="QueueRecomputeShowWarning"/>.
    /// The triggering <see cref="DuplicatesEngine.SelectedDuplicateFilePath"/> change is UI-thread-only today, so this is
    /// defensive symmetry; the inline path is taken in practice and is a no-op over the previous direct call.
    /// </summary>
    private void QueueRecomputeIsSelectedColumn()
    {
        if (Dispatcher.CheckAccess())
        {
            RecomputeIsSelectedColumn();
            return;
        }

        if (Interlocked.Exchange(ref _isSelectedColumnQueued, 1) != 0)
            return;

        Dispatcher.BeginInvoke(() =>
        {
            Interlocked.Exchange(ref _isSelectedColumnQueued, 0);
            RecomputeIsSelectedColumn();
        });
    }

    /// <summary>
    /// Recomputes the two deletion-selection-derived properties in a single pass over the live selection, using only
    /// public engine APIs: <see cref="HasSelection"/> (true when any file under this folder's subtree, or the folder
    /// path itself, is marked) and <see cref="ShowWarning"/> (true when such a marked file would leave no surviving
    /// copy). The control may be instantiated before <see cref="Root"/>/<see cref="Engine"/> are set, so both are
    /// null-guarded.
    /// </summary>
    private void RecomputeSelectionState()
    {
        var root = Root;
        var engine = Engine;
        if (root is null || engine is null)
        {
            SetValue(ShowWarningPropertyKey, false);
            SetValue(HasSelectionPropertyKey, false);
            return;
        }

        var normalizedRoot = DeletionSelection.Normalize(root.FullName);
        var subtreePrefix = normalizedRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                            + Path.DirectorySeparatorChar;

        var show = false;
        var hasSelection = false;
        foreach (var path in engine.DeletionSelection.GetFilePaths())
        {
            // GetFilePaths returns already-normalized keys, but normalize defensively in case that changes.
            var normalized = DeletionSelection.Normalize(path);
            var underThisFolder = string.Equals(normalized, normalizedRoot, StringComparison.OrdinalIgnoreCase)
                                  || normalized.StartsWith(subtreePrefix, StringComparison.OrdinalIgnoreCase);
            if (!underThisFolder)
                continue;

            // Any marked file under this folder means the clear-folder button has something to clear.
            hasSelection = true;
            if (engine.WouldLeaveZeroSurvivors(path))
            {
                show = true;
                break; // both flags are at their final value; nothing further in the loop can change them.
            }
        }

        SetValue(ShowWarningPropertyKey, show);
        SetValue(HasSelectionPropertyKey, hasSelection);
    }

    /// <summary>
    /// Recomputes <see cref="IsSelectedColumn"/>: true when the engine's selected results-tree file path is non-null
    /// and its directory equals this column's <see cref="Root"/> folder (case-insensitive, long-path-prefix agnostic
    /// via <see cref="DeletionSelection.Normalize"/>). The control may be instantiated before
    /// <see cref="Root"/>/<see cref="Engine"/> are set, so both are null-guarded.
    /// </summary>
    private void RecomputeIsSelectedColumn()
    {
        var root = Root;
        var engine = Engine;
        var selectedPath = engine?.SelectedDuplicateFilePath;
        if (root is null || string.IsNullOrEmpty(selectedPath))
        {
            SetValue(IsSelectedColumnPropertyKey, false);
            return;
        }

        var selectedDir = DeletionSelection.Normalize(Path.GetDirectoryName(selectedPath) ?? "");
        var isSelected = string.Equals(selectedDir, DeletionSelection.Normalize(root.FullName),
            StringComparison.OrdinalIgnoreCase);
        SetValue(IsSelectedColumnPropertyKey, isSelected);
    }

    /// <summary>
    /// Clears this folder's marks: removes every marked file under the folder plus the folder's directory-set
    /// entry. Because the deletion selection is the unified source of truth, this also clears those rows in the
    /// results tree. (Issue 017 verifies/extends this recursive clear.)
    /// </summary>
    private void OnClearFolder(object sender, RoutedEventArgs e)
    {
        var root = Root;
        var engine = Engine;
        if (root is null || engine is null)
            return;

        engine.DeletionSelection.RemoveAllUnder(root.FullName);
    }
}
