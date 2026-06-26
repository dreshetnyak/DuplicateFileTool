# 090 — Independence & no-regression pass

- **Status:** awaiting-verification
- **Plan step:** Step 9

## Goal
Verify the rail is purely a comparison control: it never touches the deletion-selection set, never changes deletion counts/totals, leaves the results-tree coupling unchanged, and does not regress any existing folder-comparison behavior in shown columns.

## Satisfies
- User stories: **US-21, US-22, US-28, US-29, US-30, US-31, US-32**
- PRD sections: §"Comparison vs deletion independence", §"Results-tree coupling (unchanged, not extended)"

## Exact files / classes / methods (copied from plan)
- **Change:** none expected — this is a verification + guard step. Confirm the rail only reads/writes `FolderEntries[].IsSelected` and **never** touches `DeletionSelection`; confirm `InsertColumn`/`RemoveColumn` don't alter deletion marks/counts/freed-size — in particular `RemoveColumn`'s `CancelPendingScan()` only aborts an **uncommitted** mark-scan and never removes committed marks, so existing marks **persist whether or not their folder's column is shown**; confirm results-tree selection still drives `CurrentComparisonGroup` + the column highlight on shown columns and does **not** auto-add a folder; confirm splitters, h-scroll, per-folder clear, zero-survivor warning, belonging highlight, busy overlay all still work in shown columns.

## Depends-on
- 010, 020, 030, 040, 050, 060, 070, 080

## Manual verification (PRD scenarios)
- **scenario 11** — "Confirm rail toggles never change deletion marks or totals; per-folder clear, zero-survivor warning, belonging highlight, and busy overlay still work in shown columns."
- **scenario 12** — "Select a results-tree row whose folder is shown → its column highlights; select one whose folder is not shown → nothing is added and nothing is highlighted."

## Assumptions from plan
- (none new; this step exists to enforce the comparison-vs-deletion independence and results-tree-coupling decisions, not to add behavior.)

## Result

**No code change — verification only.** All four invariants hold by code inspection; the build passes; no contradictions found.

**Build:** PASS — `dotnet build DuplicateFileTool/DuplicateFileTool.csproj` → "Build succeeded. 0 Warning(s), 0 Error(s)".

### Invariant 1 — the rail only reads/writes `FolderEntries[].IsSelected` / `.IsEnabled` and NEVER touches `DeletionSelection`
Confirmed.
- `FolderComparison.xaml.cs` references `DeletionSelection` on exactly two lines — `line 202` (`new FolderSelectionEntry(folder, DeletionSelection.Normalize(folder))`) and `line 242` (`DeletionSelection.Normalize(folderPath)`). Both are calls to the **static** `DeletionSelection.Normalize(string)` (pure, WPF-independent string normalization, `DeletionSelection.cs:300`). Neither touches the engine's `DeletionSelection` instance, and there are no `engine.DeletionSelection` accesses anywhere in the file.
- The rail's selection-change pipeline (`OnEntryPropertyChanged` → `OnEntrySelectionChanged` → `InsertColumn`/`RemoveColumn`, `lines 284-463`) and the ceiling enforcer (`EnforceSafetyCeiling`, `lines 312-317`) read/write only `FolderEntries[].IsSelected` (e.g. `line 314`, `line 338`) and `.IsEnabled` (`line 316`). The `EnforceSafetyCeiling` docstring explicitly notes it "flips only `IsEnabled`, never `IsSelected`".
- A scan of all `.Add(`/`.Remove(`/`.Clear()` calls in the container (lines 164-166, 172-209, 242-276, 355-460) shows every one targets a UI/model collection (`_roots`, `_slots`, `_slotsByPath`, `_subscribedEntries`, `FolderEntries`, `PART_Columns.Children`, `PART_Columns.ColumnDefinitions`) — never the deletion set. There is no call to `DeletionSelection.Add`/`Remove`/`RemoveAllUnder`/`AddDirectory`/`Clear` from any rail/insert/remove path.

### Invariant 2 — `InsertColumn`/`RemoveColumn` do not alter deletion marks, counts, or freed-size; `RemoveColumn`'s `CancelPendingScan()` aborts only an uncommitted mark-scan
Confirmed.
- `InsertColumn` (`lines 326-402`): creates a `FolderItem` + `FolderComparisonItem`, does in-place `ColumnDefinition` surgery, and updates `_slots`/`_slotsByPath`/`_roots`. No deletion-set mutation. The gone-folder guard (`lines 333-341`) reverts `entry.IsSelected` under `_suppressSelection` — still only `IsSelected`, not the deletion set.
- `RemoveColumn` (`lines 410-463`): the only marks-related call is `slot.Root.CancelPendingScan()` (`line 416`). `FolderItem.CancelPendingScan()` (`FolderItem.cs:360-367`) does `Interlocked.Exchange(ref _scanCts, null)` then `cts.Cancel()` — it cancels the in-flight `StartMarkScan` token and nothing else.
- Crucially, `StartMarkScan` (`FolderItem.cs:240-306`) commits marks ONLY on successful completion, inside `token.ThrowIfCancellationRequested()` + `if (token.IsCancellationRequested) return;` guards (`lines 275-280`). A cancelled scan throws `OperationCanceledException` and "leave[s] the selection untouched (atomic per folder)" (`line 291`). So `CancelPendingScan()` aborts only an UNCOMMITTED scan and commits/removes nothing — already-committed marks (which live in the engine's `DeletionSelection` set, independent of any column) persist whether or not the folder's column is shown. `RemoveColumn` removes only the UI item/splitter/column-defs and drops the slot from `_slots`/`_slotsByPath`/`_roots`; it never calls `DeletionSelection.Remove`/`RemoveAllUnder`/`RemoveDirectory`.
- Counts/freed-size are owned by `DeletionSelection` (`Count`/`Size`, `DeletionSelection.cs:73-82`) and change only on `Changed` events from `Add`/`Remove`/`Clear`/`RemoveAllUnder` — none of which the rail invokes. So insert/remove cannot move the counts or freed-size.

### Invariant 3 — results-tree selection still drives `CurrentComparisonGroup` + the column highlight on shown columns, and does NOT auto-add a folder/column
Confirmed.
- `CurrentComparisonGroup`/`SelectedDuplicateFilePath` are written only outside this control: `DuplicatesEngine.cs:717-718` (reset) and `MainViewModel.cs:470-471, 486, 489, 498` (results-tree selection). The container never assigns either.
- The container subscribes weakly to the engine and rebuilds on `CurrentComparisonGroup` change only (`OnEnginePropertyChanged`, `lines 147-152`). A group switch rebuilds the rail+default-5 (`Rebuild`, `line 151`); it does not add an arbitrary folder.
- Per-row highlight: each shown column's `FolderComparisonItem` recomputes `IsSelectedColumn` from `engine.SelectedDuplicateFilePath` (`FolderComparisonItem.xaml.cs:138-147` subscribes to `SelectedDuplicateFilePath`; `RecomputeIsSelectedColumn`, `lines 246-261`, compares the selected file's directory to the column's `Root` folder). This highlights an already-rendered column; it never creates a column. The highlight is data-bound in `FolderComparisonItem.xaml:29` (`DataTrigger` on `IsSelectedColumn`). Selecting a results-tree row whose folder is not currently shown produces no matching column, so nothing is highlighted and nothing is added — exactly the PRD's "rail is the sole membership control".

### Invariant 4 — splitters, h-scroll, per-folder clear, zero-survivor warning, belonging highlight, busy overlay all still work in shown columns
Confirmed — `FolderComparisonItem.*` and `FolderItem.cs` are unchanged in behavior by the rail work; the rail only chooses WHICH `FolderComparisonItem` instances are placed in `PART_Columns` and does in-place column surgery around the retained instances.
- **Splitters:** created by `NewSplitter()` (`FolderComparison.xaml.cs:482-489`, `PreviousAndNext`/`Columns`) and tracked per slot; insert/remove maintain `[folder][splitter]` defs in place (`lines 255-272`, `352-395`, `420-456`).
- **Horizontal scroll:** `PART_Scroll` is a `ScrollViewer HorizontalScrollBarVisibility="Auto"` (`FolderComparison.xaml:109-113`) wrapping the pixel-width `PART_Columns` grid (`line 112`, `HorizontalAlignment="Left"`); columns use pixel `DefaultColumnWidth` (`line 478`) so the total can overflow and the scrollbar appears.
- **Per-folder clear:** `FolderComparisonItem.OnClearFolder` → `engine.DeletionSelection.RemoveAllUnder(root.FullName)` (`FolderComparisonItem.xaml.cs:268-276`), wired to the clear button at `FolderComparisonItem.xaml:60-66`. Unchanged.
- **Zero-survivor warning:** `RecomputeShowWarning` (`FolderComparisonItem.xaml.cs:206-238`) + the warning `TextBlock` (`FolderComparisonItem.xaml:73-79`). Unchanged.
- **Belonging highlight:** `FolderItem.BelongsToCurrentGroup` (`FolderItem.cs:99-102`), re-raised on `CurrentComparisonGroup` change (`lines 483-490`). The container's rebuild-on-group-change leaves this node-level logic untouched.
- **Busy overlay:** `FolderItem.IsScanBusy` (`FolderItem.cs:110`) driven by `BeginScan`/`EndScan`, bound by the overlay `Border` (`FolderComparisonItem.xaml:86-93`). Unchanged. (Note: `RemoveColumn`'s `CancelPendingScan` calls `EndScan` via the scan's `finally`, so removing a busy column does not leak the counter.)

### Contradictions / flags
None. No invariant is violated; no code change was required.

## Manual checks
PRD manual-verification scenarios covered by this step (runtime checks for the maintainer):

- [ ] **Scenario 11** — Confirm rail toggles never change deletion marks or totals; per-folder clear, zero-survivor warning, belonging highlight, and busy overlay still work in shown columns.
  - [ ] Mark some files for deletion, note the deletion count and freed-size totals.
  - [ ] Toggle several rail checkboxes on and off; verify the deletion count and freed-size totals never change.
  - [ ] In a shown column, click the per-folder clear button; verify it clears that folder's marks (and the corresponding results-tree rows).
  - [ ] Mark a folder's last surviving copy; verify the zero-survivor warning line appears in that column.
  - [ ] Select a results-tree row; verify the belonging-row highlight still appears on the matching rows.
  - [ ] Mark a directory as a whole (triggers a subtree scan); verify the busy overlay shows and clears, and that removing that column mid-scan does not leak the overlay.
- [ ] **Scenario 12** — Select a results-tree row whose folder is shown → its column highlights; select one whose folder is not shown → nothing is added and nothing is highlighted.
  - [ ] With the default 5 columns shown, click a results-tree row whose folder IS one of the shown columns; verify that column gets the accent highlight.
  - [ ] Click a results-tree row whose folder is NOT currently shown (e.g. the 6th+ folder of a large group); verify no new column is added and no column is highlighted.
