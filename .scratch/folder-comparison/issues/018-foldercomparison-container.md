---
id: 018
title: FolderComparison container — distinct-folder sub-controls, side-by-side, splitters, horizontal scroll
phase: Phase 6 — Container, layout & placement
status: done
depends_on: [013, 016]
touches_files:
  - DuplicateFileTool/Controls/FolderComparison.xaml
  - DuplicateFileTool/Controls/FolderComparison.xaml.cs
user_stories: [1, 2, 4, 5, 35, 36, 37]
---

## Description
Create the container control (`FolderComparison`) bound to the current group (the binding source arrives in 020). It produces **one sub-control (016) per distinct containing folder** among the group's files (derived from each `DuplicateFile`'s `FileData` directory; same-folder duplicates collapse to one column that highlights all of them). Lay them out as **side-by-side columns** separated by draggable **vertical `GridSplitter`s**, with a sensible **minimum column width** and a **horizontal scrollbar** when columns overflow. Show an empty **placeholder** when no group is selected. Structure it so a pager can be added later without rework.

**OQ-3 — DECIDED:** use a fixed sensible minimum column width; columns default to equal-ish sizing; **no** persistence of folder-tree column widths this iteration (unlike the results tree).

Grounding: distinct folders derived from the group's `DuplicateFile.FileData`; sub-control from 016; belonging highlight relies on flags from 013.

## Acceptance criteria
- Selecting a group with N distinct folders shows N columns; same-folder duplicates collapse to one column highlighting all of them.
- Many folders produce a horizontal scrollbar (columns keep their min width rather than squeezing to nothing).
- Vertical splitters resize adjacent columns.
- No group selected → placeholder/empty state.

## Manual verification
PRD scenarios 1, 2, 5, 35, 36, 37: select groups whose copies span several folders → side-by-side columns, one per distinct folder; many folders scroll horizontally; drag a splitter to resize; deselect → placeholder. (Current-group rebuild is wired in 020.)

## Manual verification performed
- `dotnet build DuplicateFileTool/DuplicateFileTool.csproj -nologo -clp:ErrorsOnly` → Build succeeded, 0 Warning(s), 0 Error(s).
- Code inspection: with no host (019) and `CurrentComparisonGroup` null, only the placeholder path is exercisable. `Rebuild()` short-circuits to `ShowPlaceholder()` when `Engine`/group is null, so the control renders the centered placeholder and an empty, collapsed scroll viewer.
- Logic reviewed against acceptance: distinct-folder grouping (`DeletionSelection.Normalize(DirPath)`, OrdinalIgnoreCase, stable order, same-folder collapse), pixel ColumnDefinitions + Auto splitter columns inside a horizontal-only ScrollViewer (overflow → horizontal scrollbar, MinWidth=200 floor), GridSplitter PreviousAndNext between columns, gone-folder skip via `fd.IsEmpty`, zero-survivor → placeholder, weak engine subscription, and `CancelPendingScan()` on old roots before each rebuild.
