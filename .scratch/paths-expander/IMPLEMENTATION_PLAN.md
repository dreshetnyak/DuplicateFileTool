# Implementation Plan: Folder-selection rail ("paths expander")

Status: draft (planning only — no code yet)
Source PRD: `./PRD.md` (Status: ready-for-agent). Parent: `../folder-comparison/PRD.md`.

This plan modifies the **existing** folder-comparison container. It rides the seams the parent feature already built — the distinct-folder derivation in `Rebuild()` and the `BuildColumns(engine, folderPaths)` slice method that was deliberately factored to accept a folder-list subset.

---

## Grounding: the code as it is today

Verified against the current sources (not assumed):

- **`Controls/FolderComparison.xaml.cs`** — the container. `Rebuild()` (group change / engine set / post-deletion) clears `PART_Columns`, derives distinct folders inline (`GroupBy Normalize(DirPath) → OrderBy key → Select first real path`), then calls `BuildColumns(engine, distinctFolders)`. `BuildColumns` creates one `FolderItem(fd, engine){IsExpanded=true}` root per folder, tracks roots in `_roots`, and lays out a flat `PART_Columns` Grid as `[folder pixel col][Auto splitter col]…` ending on a folder (no trailing splitter). Consts: `MinColumnWidth=200`, `DefaultColumnWidth=300`, `SplitterWidth=5`. `_roots` exists only so the next rebuild can `CancelPendingScan()` each.
- **`Controls/FolderComparison.xaml`** — a `Grid` holding `PART_Scroll` (horizontal `ScrollViewer`) → `PART_Columns` (the code-built Grid), plus `PART_Placeholder` (a centered `TextBlock`). `ShowPlaceholder()` flips `PART_Scroll`↔`PART_Placeholder` visibility.
- **`Controls/FolderComparisonItem.{xaml,xaml.cs}`** — one column: header (path clipped + clear button) / `FolderTree` / warning line / busy overlay. Two DPs: `Root` (a `FolderItem`, drives `DataContext`) and `Engine`. Read-only `ShowWarning` and `IsSelectedColumn` recomputed from weak engine subscriptions. **Self-contained, reusable, no per-instance teardown needed beyond dropping the reference + cancelling its root scan.**
- **`FolderItem.cs`** — node VM. Column root created with `IsExpanded=true` → synchronous `LoadChildren()` (Win32 enumerate + per-child `SHGetFileInfo`) on the UI thread; each directory child ctor starts `StartSizeScan()` (uncapped `Task.Run`, no CTS). `CancelPendingScan()` cancels the mark-scan only. `Level` = depth from root (root=0, never shown). Weak engine/selection subscriptions, so dropped roots are collectable.
- **`DeletionSelection.Normalize(string)`** — `public static`, WPF-independent. Already the case/long-path normalization key used by the inline derivation and every subtree-prefix test. Safe to reuse from a pure helper.
- **`DuplicatesEngine` (`DuplicateGroup` ctor)** — adds `DuplicateFiles` in `OrderBy(df => df.ComparableFile.FileData.FullName)` order. So **"the group's own order" = ascending full-path order**, which is exactly what the results tree displays. First-appearance distinct-folder over that sequence is well-defined.
- **`MainWindow.xaml`** — host at line ~1013: `<controls:FolderComparison Grid.Row="2" Engine="{Binding Duplicates}"/>` inside the **temporary** 3-row wrapper grid (rows 509-513, marked "remove on Results redesign"). The settings expanders (~1187-1224) are plain `<Expander Header=…>` (default `ExpandDirection=Down`) each wrapped in a rounded `Border` (`CornerRadius=10`, `BorderBrush=LightGray`). This rounded-border look is the visual to echo.
- **Resources** — existing `Ui_FolderComparison_*` keys live in **all four** resx: `Resources.resx`, `Resources.en.resx`, `Resources.es.resx`, `Resources.ru.resx` (plus the generated `Resources.Designer.cs`). New keys go in the same four (+ Designer regenerates).
- **No test project** exists; verification is **manual** (maintainer decision, per both PRDs).

---

## Underspecified / conflicts → assumptions taken

1. **Expander direction vs "button on top."** A WPF `Expander` with `ExpandDirection="Right"` places its toggle on the *side*, not the top — it cannot give "button on top, content expands right" without a custom `ControlTemplate`. The user's stated intent (request 3) is "same look as the settings expanders, button on top, expands horizontally." **Assumption:** implement the rail as a **rounded `Border` containing a `Grid` of `[Auto toggle row][* list row]`**, where a top `ToggleButton` drives the rail `ColumnDefinition` width between a slim collapsed strip and the fixed expanded width — not a literal `Expander`. This matches the settings *look* (rounded border) and the "button on top, grows right" behavior. Flagged for confirmation. (Fallback: a restyled `Expander ExpandDirection="Right"` with a custom header template — more template-fighting, same result.)

2. **Preserving scroll/expansion of untouched columns on add/remove.** WPF unloads a child removed from a panel; re-adding it can reset inner `ScrollViewer` offset. PRD US-16 / scenarios 3-4 require scroll preserved. **Assumption:** incremental insert/remove is done by **in-place `ColumnDefinition` surgery + `Grid.Column` renumbering on retained children — never removing/re-adding the untouched `FolderComparisonItem` instances** (expansion is data-bound to `FolderItem.IsExpanded` and survives regardless; in-place keeps scroll too). Manual scenario 3/4 is the runtime check; documented fallback below if scroll still resets.

3. **"Group order" tie to the results tree.** Confirmed: results tree and the helper both consume the `DuplicateGroup`'s full-path-sorted `DuplicateFiles`, so first-appearance distinct folder == results-tree folder order. No conflict; the only change is dropping the `OrderBy(key)` currently in `Rebuild()`.

4. **Where the pure helper lives.** PRD wants "a single pure, WPF-independent helper … the highest test seam." **Assumption:** a new `internal static class` in the core namespace (not under `Controls/`), taking strings in / strings out so it has zero dependency on `DuplicateFile`/WPF and is trivially unit-testable later. Signature below.

5. **Rail width / ceiling values.** `5` is spec. `50` (ceiling) and `~250px` (expanded width) are proposed; flagged **confirm-during-implementation**. Collapsed strip width (~24-28px) is cosmetic, confirm during implementation.

6. **Post-deletion refresh path.** OQ-6 (parent issues 009/020) rebuilds by re-setting `CurrentComparisonGroup` from the surviving results row, which already fires `Rebuild()`. **Assumption:** the rail/selection reset needs no new trigger — it rides `Rebuild()`. No change to the deletion command.

---

## Ordered steps

Sequenced so the **pure ordering helper** and the **selection model** land before any layout or incremental-column work, exactly as the PRD's "highest seam first" requires.

### Step 1 — Pure ordered-distinct-folder helper (highest seam)
- **Satisfies:** US-3, US-11, US-33; PRD §"Distinct-folder ordering"; Testing §"Highest seam".
- **Add:** `internal static class GroupFolders` (core namespace, e.g. `GroupFolders.cs` next to `DeletionSelection.cs`) with one pure method:
  `static IReadOnlyList<string> OrderedDistinct(IEnumerable<string> directoryPaths)` — iterates in order, dedupes by `DeletionSelection.Normalize(path)` keeping **first appearance**, returns the first real (un-normalized) path seen per key. No sorting. No WPF.
- **Change:** `FolderComparison.Rebuild()` — replace the inline `GroupBy/OrderBy/Select` with `GroupFolders.OrderedDistinct(group.DuplicateFiles.Select(f => f.FileData.DirPath))`. `BuildColumns` unchanged this step.
- **Manual verification it enables:** scenario **2** & **11** (rail/column/results-tree order agree — here just column order), scenario **5**/**17** ordering, US-33 (same-folder duplicates → one column, already true, now order-stable). Exercise on a **small** group (a handful of distinct folders) — the per-group cap that bounds column count does not exist until Step 2, so a huge group still builds all columns here.
- **Dependencies:** none. First.

### Step 2 — Selection model + render only the default subset
- **Satisfies:** US-1, US-2, US-12, US-23, US-24, US-26; PRD §"Default selection", §"Selection model", §"Performance posture" (bounds automatic cost). (US-23 group-switch reset and US-24 post-deletion reset are delivered here: `Rebuild()` rebuilds `FolderEntries`+default-5 on every `CurrentComparisonGroup` change, and the post-deletion path re-drives that group — see assumption 6. No new trigger.)
- **Add:** `FolderSelectionEntry : NotifyPropertyChanged` (in the container's file or a sibling) with `string DisplayPath`, `string NormalizedPath`, `bool IsSelected { get; set; }` (raises change), `bool IsEnabled { get; set; }` (for the ceiling, Step 6). In `FolderComparison` code-behind add `ObservableCollection<FolderSelectionEntry> FolderEntries` and an internal `const int DefaultSelectedCount = 5`.
- **Change:** `Rebuild()` — after computing the ordered distinct list: build `FolderEntries` (one entry per folder, in order), mark the **first `min(5, count)`** `IsSelected=true` while `_suppressSelection` is set (so the Step-5 handler doesn't fire during bulk build); **set the guard before the bulk assignment and clear it after in a `try/finally`** so real user toggles are not permanently suppressed once Step 5 lands. Then call `BuildColumns(engine, <selected folder paths in order>)`. `BuildColumns` still does a full build but now from the selected slice — **no rail UI yet**, so it always renders the default 5.
- **Manual verification it enables:** scenario **1** (group >5 folders → exactly first 5 render), scenario **8** (group switch resets to default 5), scenario **9** (post-deletion rebuild resets to default 5), US-26 (<5 → all render).
- **Dependencies:** Step 1.

### Step 3 — Two-region container layout + collapsible rail shell
- **Satisfies:** US-4, US-5, US-6, US-7, US-8, US-35; PRD §"Layout & expander".
- **Change:** `FolderComparison.xaml` — wrap the existing content in an outer 2-column `Grid`: column 0 = the rail (`Width` toggled between collapsed strip `Auto`/~26px and expanded `~250px`), column 1 = the existing `PART_Scroll`/`PART_Placeholder` stack (move them into column 1). Rail = a rounded `Border` (echo settings-expander style) containing `[Auto] top ToggleButton (PART_RailToggle)` over `[*] PART_RailList` (the folder list, Step 4). The toggle's `IsChecked` drives column-0 width (collapsed↔expanded) via a trigger/code-behind; default **unchecked/collapsed every build** (no persistence). Dock/push only — rail is a real grid column, never an overlay. Long-path clipping handled in Step 4's item template.
- **Add consts:** `ExpandedRailWidth ≈ 250` *(confirm)*, `CollapsedRailWidth ≈ 26` *(confirm)*.
- **Manual verification it enables:** scenario **1** (rail present & collapsed by default), the expand/collapse + push behaviour (US-7), button-on-top (US-6), fixed width (US-8), starts collapsed (US-35).
- **Dependencies:** Step 2 (entries exist to list in Step 4); layout itself depends only on the XAML.

### Step 4 — Rail folder list with checkboxes (clip + tooltip)
- **Satisfies:** US-9, US-10, US-11, US-12, US-34; PRD §"Selection model" (binding), §"Layout" (clip/tooltip).
- **Change:** `PART_RailList` = an `ItemsControl`/`ListBox` bound to `FolderEntries`; item template = `CheckBox` (`IsChecked`↔`IsSelected`, `IsEnabled`↔entry `IsEnabled`) + a `TextBlock` (`Text=DisplayPath`, `ToolTip=DisplayPath`, `TextTrimming=CharacterEllipsis`) inside the fixed rail width. Add a rail title/header `TextBlock` (localized, Step 8) near the toggle. Checkbox state is bound to the same `IsSelected` the columns derive from, so list and columns **cannot disagree** (US-34).
- **Manual verification it enables:** scenario **2** (every folder listed, group order, first 5 checked, matches columns & results tree), scenario **13** (long paths clipped + full-path tooltip). Note: toggling a checkbox at this step is **expected to NOT yet add/remove a column** — that wiring lands in Step 5. Step 4 verifies only the static list/order/initial-checked state matching the rendered default 5.
- **Dependencies:** Steps 2, 3.

### Step 5 — Incremental column insert/remove (the hard part)
- **Satisfies:** US-13, US-14, US-15, US-16, US-17, US-34; PRD §"Incremental column maintenance".
- **Model:** introduce an ordered `List<ColumnSlot>` of **currently-shown** columns + `Dictionary<string,ColumnSlot>` keyed by `NormalizedPath`, where `ColumnSlot` holds the `FolderComparisonItem`, its root `FolderItem`, and (optionally) its trailing `GridSplitter`. Refactor `BuildColumns` (full build) to populate this model; `_roots` becomes derivable from it (or keep `_roots` in sync).
- **Add (code-behind):**
  - `OnEntrySelectionChanged(entry)` — fired by `FolderSelectionEntry.IsSelected` (skipped while `_suppressSelection`): if now selected → `InsertColumn(folder)`; else → `RemoveColumn(folder)`.
  - `InsertColumn(folder)` — **mirror `BuildColumns`' gone-folder guard**: re-fetch via `FileSystem.GetFileData(folder)` and if `fd.IsEmpty` (folder deleted/moved since the search) do **not** create a slot — instead revert the entry's `IsSelected` back to `false` under the `_suppressSelection` guard (or set `IsEnabled=false`) so the rail checkbox state and the rendered columns **stay in agreement** (US-34). Otherwise create exactly one `FolderComparisonItem` + `FolderItem(...){IsExpanded=true}` root. Compute the **ordered insertion index over the actually-shown slots** (not the raw selected set) from the canonical `GroupFolders.OrderedDistinct` order. Do **in-place `ColumnDefinition` insertion + `Grid.Column` renumber** of retained children (insert `[folder][splitter]` defs at the right index, or append `[splitter][folder]` when adding at the end; first-ever column adds a single folder def, no splitter). Update the slot model and `_roots`. Never touch other items' instances.
  - `RemoveColumn(folder)` — find the slot; `root.CancelPendingScan()` (this aborts only an **uncommitted** mark-scan and commits nothing — it never removes existing deletion marks; see Step 9). Then by case: **N≥2, removing a non-last column** → remove its `FolderComparisonItem` + its **trailing** splitter, `RemoveAt` their two `ColumnDefinition`s, decrement `Grid.Column` by 2 on children after them. **N≥2, removing the last column** → remove its item + the **preceding** splitter and their two `ColumnDefinition`s (no renumber needed — they are at the tail). **N==1, removing the only column** → remove just its single folder `ColumnDefinition` + item; there is **no splitter** to remove; route to the Step 7 empty state. Drop from the slot model/`_roots`.
- **Fallback (documented):** if manual scenario 3/4 shows scroll resets despite in-place surgery, switch to a `RelayoutColumns()` that reuses item instances but re-adds them, and accept expansion-preserved/scroll-reset, OR snapshot+restore each retained `ScrollViewer` offset around the relayout.
- **Suggested internal staging (still one issue):** (5a) refactor `BuildColumns` to build-and-populate the `ColumnSlot` model + dictionary with `_roots` derived from it — full-build behavior unchanged, independently checkable that the default 5 still render exactly as in Step 2; then (5b) add `OnEntrySelectionChanged` + `InsertColumn`/`RemoveColumn` in-place surgery on top of that model. The 5a/5b split is a review aid, not a separate issue.
- **Manual verification it enables:** scenario **3** (check 6th → inserts at correct position, others keep expansion/scroll/width), **4** (uncheck middle → only that column+splitter removed), **5** (check only two → side by side), **7** (rapid toggle → no leaked columns/handlers, list & columns consistent).
- **Dependencies:** Steps 1, 2, 4.

### Step 6 — Safety ceiling
- **Satisfies:** US-18, US-19, US-20; PRD §"Safety ceiling".
- **Add:** `const int SafetyCeiling = 50` *(confirm)*. After every selection change (and at build), if selected count `== SafetyCeiling` set `IsEnabled=false` on all **unselected** entries; otherwise `IsEnabled=true` on all. Checked entries always stay enabled (can uncheck). No "select all" affordance. Disabled checkboxes show the ceiling tooltip (Step 8): set `ToolTipService.ShowOnDisabled="True"` and the ceiling `ToolTip` on the **same `CheckBox` element** whose `IsEnabled` binds to `entry.IsEnabled` (not on an enabled ancestor), so the tooltip surfaces while the checkbox is disabled.
- **Manual verification it enables:** scenario **6** (at ceiling, unchecked disabled with tooltip; checked still uncheckable).
- **Dependencies:** Steps 2, 4, 5.

### Step 7 — Empty-state and no-group states
- **Satisfies:** US-25, US-27; PRD §"Empty & no-group states".
- **Change:** `FolderComparison.xaml` — add a third visual `PART_EmptySelection` `TextBlock` (centered, in column 1) for "no folders selected". Introduce a small `UpdateColumnsAreaState()` that selects among three states: **no group** → existing `PART_Placeholder`, **rail hidden** (column-0 width 0 / `Collapsed`); **group, 0 selected** → `PART_EmptySelection`, rail visible; **group, ≥1 selected** → `PART_Scroll`, rail visible. Route `Rebuild()`, `InsertColumn`, `RemoveColumn` through it.
- **Manual verification it enables:** scenario **10** (uncheck all → "no folders selected"; re-check restores), scenario **1**/**25** (no group → placeholder, no rail).
- **Dependencies:** Steps 3, 5; string from Step 8.

### Step 8 — Localization (en/es/ru)
- **Satisfies:** US-36; PRD §"Localization".
- **Add 4 keys** to `Resources.resx`, `Resources.en.resx`, `Resources.es.resx`, `Resources.ru.resx`, **and hand-add the four matching static accessors to `Resources.Designer.cs`** (this SDK-style project has no `ResXFileCodeGenerator`/`CustomTool`, so `dotnet build` does **not** regenerate the Designer — the existing `Ui_FolderComparison_*` accessors are maintained by hand; without the accessors the `x:Static` references will not compile). Follow the `Ui_FolderComparison_*` family:
  - `Ui_FolderComparison_Rail_Title` (rail header)
  - `Ui_ToolTip_FolderComparison_Rail_Toggle` (toggle tooltip)
  - `Ui_FolderComparison_Rail_LimitReached` (ceiling tooltip)
  - `Ui_FolderComparison_NoFoldersSelected` (empty-state message)
- **Change:** wire the four `{x:Static resx:Resources.…}` references into Steps 3/4/6/7.
- **Manual verification it enables:** scenario **13** (all new strings present in en/es/ru).
- **Dependencies:** referenced by Steps 3, 6, 7 (do alongside; can land last).

### Step 9 — Independence & no-regression pass
- **Satisfies:** US-21, US-22, US-28, US-29, US-30, US-31, US-32; PRD §"Comparison vs deletion independence", §"Results-tree coupling".
- **Change:** none expected — this is a verification + guard step. Confirm the rail only reads/writes `FolderEntries[].IsSelected` and **never** touches `DeletionSelection`; confirm `InsertColumn`/`RemoveColumn` don't alter deletion marks/counts/freed-size — in particular `RemoveColumn`'s `CancelPendingScan()` only aborts an **uncommitted** mark-scan and never removes committed marks, so existing marks **persist whether or not their folder's column is shown**; confirm results-tree selection still drives `CurrentComparisonGroup` + the column highlight on shown columns and does **not** auto-add a folder; confirm splitters, h-scroll, per-folder clear, zero-survivor warning, belonging highlight, busy overlay all still work in shown columns.
- **Manual verification it enables:** scenario **11** (toggles never change deletion marks/totals; clear/warning/highlight/overlay still work), scenario **12** (shown folder row highlights; not-shown folder → nothing added/highlighted).
- **Dependencies:** Steps 1-8.

### Step 10 — Changelog + constant confirmation
- **Satisfies:** PRD §"Further Notes" (versioning/changelog), tunable-constants flag.
- **Change (a) — changelog now:** fold a single `New.` bullet about the rail into the **existing folder-comparison Work-in-Progress** entry in `DuplicateFileTool/Changes.md` (do **not** add a separate version bump; 2.4.0 / parent issue 025 remains the release vehicle). This WIP line lands now; it promotes to **Unreleased** only after Dennis confirms it works at runtime, per the gating convention.
- **Change (b) — constants:** confirm final values for `SafetyCeiling` (50) and `ExpandedRailWidth` (~250px) with Dennis during implementation.
- **Dependencies:** Steps 1-9; promotion WIP→Unreleased and **any commit happen only after runtime verification and an explicit request.**

---

## Tunable constants (confirm during implementation)

| Constant | Proposed | Status |
|---|---|---|
| `DefaultSelectedCount` | 5 | user spec (fixed) |
| `SafetyCeiling` | 50 | **confirm** |
| `ExpandedRailWidth` | ~250px | **confirm** |
| `CollapsedRailWidth` | ~26px | confirm (cosmetic) |

---

## Out of scope (honored — do NOT implement)

Reordering folders/columns; persisting rail expand-state or selection across groups/runs; per-folder column-width persistence; the per-column performance fixes (async/off-UI-thread folder load, size-scan CTS, size-scan de-dup/concurrency/drive-awareness, async/cached icons); true column virtualization / master-detail; a configurable cap or page-size setting; a "select all" affordance; auto-adding the selected results-tree row's folder; the eventual Results-page redesign; non-Windows behaviour; **any unit/integration test project**.

---

## Manual verification scenarios → covering steps

| # | PRD scenario | Covered by |
|---|---|---|
| 1 | >5 folders → first 5 render, group order, rail collapsed | 1, 2, 3 |
| 2 | Expand rail → all folders listed, group order, first 5 checked, matches columns & results tree | 1, 2, 4 |
| 3 | Check 6th → inserts at correct position; existing 5 keep expansion/scroll/width | 5 |
| 4 | Uncheck middle → only that column+splitter removed; others undisturbed | 5 |
| 5 | Check only two → just those two render, side by side | 1, 5 |
| 6 | Reach ceiling → unchecked disabled + tooltip; checked still uncheckable | 6 |
| 7 | Rapid toggle → no leaked columns/handlers; list & columns consistent | 5 |
| 8 | Switch group → rail list + columns reset to new group's default 5 | 2 |
| 9 | Deletion empties/changes folders → after refresh, rail rebuilt + reset to 5 | 2 |
| 10 | Uncheck every folder → "no folders selected"; re-check restores | 7 |
| 11 | Toggles never change deletion marks/totals; clear/warning/belonging/overlay still work | 9 |
| 12 | Results row whose folder is shown → highlights; not shown → nothing added/highlighted | 9 |
| 13 | Long paths clipped + full-path tooltip; en/es/ru strings present | 4, 8 |
