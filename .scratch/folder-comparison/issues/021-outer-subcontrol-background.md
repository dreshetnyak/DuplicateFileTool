---
id: 021
title: Outer sub-control background when its file is the selected results row
phase: Phase 7 — Current-group binding & outer background
status: done
depends_on: [020]
touches_files:
  - DuplicateFileTool/DuplicatesEngine.cs
  - DuplicateFileTool/MainViewModel.cs
  - DuplicateFileTool/Controls/FolderComparisonItem.xaml
  - DuplicateFileTool/Controls/FolderComparisonItem.xaml.cs
user_stories: [7]
---

> **Implementation note (orchestrator):** The static `DuplicateFile.ItemSelected` event is transient — a column rebuilt after a selection (selecting a file rebuilds the control) would miss it. So persist the selection: add `DuplicatesEngine.SelectedDuplicateFilePath` (nullable string, notifying), set it in `MainViewModel.OnDuplicateFileSelected` on select (next to the `CurrentComparisonGroup` set; set-on-select only, like the group — no clear on deselect to avoid flicker). Each `FolderComparisonItem` computes `IsSelectedColumn` = `SelectedDuplicateFilePath`'s directory equals this column's `Root.FullName` (case-insensitive via `DeletionSelection.Normalize`), recomputed on creation (Root/Engine set) AND on a weak `Engine.PropertyChanged` for `SelectedDuplicateFilePath`. Wrap the sub-control content in a `Border` whose OUTER background/brush lights up when `IsSelectedColumn` — visually distinct from the row red/belonging backgrounds (015) and not conflicting with them. `FolderComparison.xaml.cs` is NOT needed (per-item concern).

## Description
Light up each sub-control's **outer** background (distinct from the row backgrounds of 015) when its corresponding file is the currently selected results-tree row. The highlight moves as the results-tree selection changes. This must never conflict with the red/belonging *row* backgrounds.

Grounding: current results-tree selection is available via the selection signal from 020; the sub-control is `FolderComparisonItem` (016) hosted by `FolderComparison` (018).

## Acceptance criteria
- Selecting a duplicate in the results tree highlights exactly its folder's sub-control outer background.
- Changing the selection moves the highlight to the new file's folder column.
- The outer highlight is visually distinct from and does not interfere with the red/belonging row backgrounds.

## Manual verification
PRD scenario 2: click a duplicate file in the results tree → its folder column gets the distinct outer background; click a duplicate in another folder → the highlight moves.

## Manual verification performed
- `dotnet build DuplicateFileTool/DuplicateFileTool.csproj` → Build succeeded, 0 Warning(s), 0 Error(s).
- Code review of the wiring:
  - `DuplicatesEngine.SelectedDuplicateFilePath` added as a notifying nullable string (ReferenceEquals + ordinal string-equality guard), purely additive.
  - `MainViewModel.OnDuplicateFileSelected` sets it on select only (next to `CurrentComparisonGroup`), never clears on deselect — no flicker.
  - `FolderComparisonItem.IsSelectedColumn` (read-only DP) = selected file's normalized directory equals the column's normalized `Root.FullName` (OrdinalIgnoreCase, long-path-prefix agnostic via `DeletionSelection.Normalize`); null-guarded for Root/Engine/path.
  - Recomputed on Root/Engine set (so a column rebuilt for the just-selected file highlights immediately from the persisted path) and on the existing **weak** `Engine.PropertyChanged` subscription when `PropertyName == SelectedDuplicateFilePath` (so the highlight moves on in-group selection without a rebuild). No new subscription added — leak-free.
  - Outer `Border` (`x:Name="OuterBorder"`) wraps the content Grid; default fully transparent; DataTrigger on `IsSelectedColumn == True` applies a 2px `#FF1BBBFA` accent border + faint `#1A1BBBFA` fill, distinct from the issue-015 light-blue belonging (`#FFE3F0FF`) and light-red zero-survivor (`#FFFFD6D6`) inner row tints.
