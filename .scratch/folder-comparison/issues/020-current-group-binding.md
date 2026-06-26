---
id: 020
title: Expose a CurrentGroup driven by results-tree selection
phase: Phase 7 — Current-group binding & outer background
status: done
depends_on: [002, 018]
touches_files:
  - DuplicateFileTool/MainViewModel.cs
  - DuplicateFileTool/DuplicatesEngine.cs
  - DuplicateFileTool/Controls/FolderComparison.xaml.cs
user_stories: [3, 33]
---

## Description
Add a `CurrentGroup` that the `FolderComparison` container (018) binds to, updated when a results row is selected: a `DuplicateFile` → its parent group; a `DuplicateGroup` header → that group. Reuse the existing static `DuplicateFile.ItemSelected`; add an equivalent for group-header selection (or bind via the `ResultsTreeView` selection). Requires exposing the file's group — use the membership index (002) or make `ParentGroup` accessible. Selections in the unified set persist across group switches and results paging because the set is engine-owned. **OQ-6 — DECIDED:** after a deletion run the control rebuilds from the still-selected results row, falling back to the placeholder if that row was deleted (the rebuild logic lives with this `CurrentGroup` mechanism; issue 009 triggers it post-run).

Grounding:
- `DuplicateFile.ItemSelected` static event (DuplicatesEngine.cs 18, fired 59); `MainViewModel` subscribes (298) → `OnDuplicateFileSelected` (460–465) sets `AutoSelectByPath.Path`.
- `DuplicateGroup.IsSelected` (123–131) only raises `PropertyChanged` today — add a selection signal for group headers.
- `DuplicateFile.ParentGroup` is private (line 20).

> **Added scope (from issue 009):** also implement the **post-run folder-control refresh** here. Wire `DeleteMarkedFiles.Finished` (in `MainViewModel`, next to the existing `RefreshExpandedFileTreeItems` call) to refresh the `FolderComparison` control so deleted files disappear from it. Per **OQ-6**: rebuild from the still-selected results row; if that row was deleted, fall back to the placeholder (i.e. set `CurrentComparisonGroup` from the current selection, or null if it no longer exists). Expose a `Refresh()`/re-bind path on `FolderComparison` if needed (re-running its rebuild re-enumerates the folders from disk). *(Satisfies story 34's "control updates after a deletion run".)*

## Acceptance criteria
- Selecting any file row **or** a group header rebuilds the control for that group.
- Selections in the set persist across group switches and across results paging (set is engine-owned).
- No regression to the existing `OnDuplicateFileSelected` → Auto Select by Path wiring.

## Manual verification
PRD scenarios 3, 8: click different files/group headers in the results tree → the control rebuilds for the selected group; switch groups and page the results → previously-made marks remain. (Paging note: see plan; designed to coexist with a future pager.)

## Manual verification performed
- Code review of the implemented wiring:
  - `DuplicateGroup.ItemSelected` (static event) added and fired from the `IsSelected` setter after `OnPropertyChanged()`, mirroring `DuplicateFile`.
  - `OnDuplicateFileSelected` keeps the existing `AutoSelectByPath.Path` behavior (set to the file path on select, "" on deselect) and additionally sets `Duplicates.CurrentComparisonGroup = GetGroupForPath(...)` only on select (never clears on deselect → no within-group rebuild flicker; setter ReferenceEquals-guards same-group reselection).
  - `OnDuplicateGroupSelected` sets `CurrentComparisonGroup = group` when a group header becomes selected; subscribed in the ctor next to `DuplicateFile.ItemSelected`.
  - Post-run `DeleteMarkedFiles.Finished` handler keeps `Ui.Entry.Enabled = true` + `RefreshExpandedFileTreeItems()`, then re-drives `CurrentComparisonGroup` (null → group, or null when the current group was removed) to force the control rebuild / placeholder fallback. Confirmed the handler runs on the UI thread (async-void `Execute` finally resumes on captured UI context; the pre-existing `RefreshExpandedFileTreeItems` in the same handler already relied on this).
- Build: `dotnet build DuplicateFileTool.csproj -nologo -clp:ErrorsOnly` → Build succeeded, 0 Warning(s), 0 Error(s).
