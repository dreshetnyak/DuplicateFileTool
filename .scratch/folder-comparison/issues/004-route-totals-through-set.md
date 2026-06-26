---
id: 004
title: Route deletion totals through the set; retire per-command delta plumbing
phase: Phase 1 — Engine selection model
status: done
depends_on: [001, 003]
touches_files:
  - DuplicateFileTool/DeletionSelection.cs
  - DuplicateFileTool/DuplicatesEngine.cs
  - DuplicateFileTool/MainViewModel.cs
  - DuplicateFileTool/Commands/ToggleDeletionMarkCommand.cs
  - DuplicateFileTool/Commands/AutoSelectByPathCommand.cs
  - DuplicateFileTool/Commands/ResetSelectionCommand.cs
user_stories: [17, 18]
---

> **Implementation note (orchestrator):** Use the DELTA approach, NOT a `ToBeDeletedCount = set.Count` recompute. The deletion run decrements totals directly via `DuplicatesEngine.DeletionStateChanged` (line ~778); the remover does not sync the set until issue 007. A recompute-from-set would freeze the deletion progress counter (regression). Instead: extend `DeletionSelection.Changed` event args with the affected size; the engine maintains `ToBeDeletedCount`/`ToBeDeletedSize` via +/- deltas on Added/Removed and =0 on Reset; **leave `DeletionStateChanged` untouched**. Also have `DuplicatesEngine.Clear()` (search start, line ~642) call `DeletionSelection.Clear()` so a new search starts with an empty selection (this also wipes any stale post-deletion entries before issue 007 lands).

## Description
Make `DuplicatesEngine.ToBeDeletedCount`/`ToBeDeletedSize` derive from the unified set (recompute from, or be maintained by, the set on change). Change the three marking commands to mutate the **set** instead of emitting `UpdateToDelete*` events, and collapse `MainViewModel.OnUpdateToDelete` accordingly. Keep `AutoSelectByPathCommand`'s own "leave at least one" guard (lines 99–108) — only its totals plumbing changes.

Grounding:
- `ToBeDeletedCount`/`ToBeDeletedSize` setters: `DuplicatesEngine.cs` 312–329.
- `MainViewModel.OnUpdateToDelete` (467–471) is wired to `ToggleDeletionMark.DeletionMarkToggle` (291), `AutoSelectByPath.FilesAutoMarkedForDeletion` (294), `ResetSelection.UpdateToDeleteSize` (302).
- `ToggleDeletionMarkCommand.cs` `Execute` (15–32) fires `DeletionMarkToggle(±1, ±size)`; `AutoSelectByPathCommand.cs` fires `FilesAutoMarkedForDeletion` (119/171); `ResetSelectionCommand.cs` `DeselectAll` (22–34) fires `UpdateToDeleteSize`.
- `OnDuplicatesPropertyChanged` (367–387) flips `ResetSelection.Enabled`/`DeleteMarkedFiles.Enabled` off `ToBeDeletedCount != 0`.

## Acceptance criteria
- Totals (`ToBeDeletedCount`/`ToBeDeletedSize`) match set contents exactly after any mark/unmark/auto-select/reset.
- The **same path marked via two routes counts once** (no double counting).
- `ResetSelection.Enabled`/`DeleteMarkedFiles.Enabled` still gate on `ToBeDeletedCount != 0`.
- The right-panel "Selected for deletion size" textbox (bound to `Duplicates.ToBeDeletedSize`, MainWindow.xaml 934) updates correctly.
- **Existing behavior unchanged:** results-tree marking, Auto Select by Path, and the totals display behave as before for dup-only selections.

## Manual verification
Build, run, find duplicates. PRD scenario 5/8 (partial): mark/unmark in the results tree and via Auto Select by Path — count and freed-size totals track exactly; marking the same file from both routes does not double the totals.

## Manual verification performed
- Build: `dotnet build DuplicateFileTool/DuplicateFileTool.csproj -nologo -clp:ErrorsOnly --no-incremental` → Build succeeded, 0 Warning(s), 0 Error(s).
- Grep of the project for the retired identifiers (`DeletionMarkToggle`, `FilesAutoMarkedForDeletion`, `UpdateToDeleteSize`, `OnUpdateToDelete`, `UpdateToDeleteEventArgs`, `UpdateToDeleteEventHandler`, plus the private raisers) → no matches in any `.cs` file.
- How a human verifies:
  - **Marking totals:** find duplicates, mark/unmark a file in the results tree (checkbox). The "Selected for deletion" count and size (right panel, bound to `Duplicates.ToBeDeletedCount`/`ToBeDeletedSize`) step up/down by exactly that file's contribution.
  - **No double counting:** the same file marked from the tree and again via Auto Select by Path counts once (the set rejects the duplicate add; no event/total change on the redundant mark).
  - **Auto-select totals:** enter a path, run Auto Select by Path — totals rise by the marked files' sizes/count, and the last-copy guard still leaves at least one copy per group unmarked.
  - **Reset:** Reset Selection zeroes the count/size; the Reset Selection and Delete Marked Files buttons disable (gated on `ToBeDeletedCount != 0`).
  - **Deletion-run counts unchanged:** run a delete; the count/size tick down per file exactly as before (still driven by the untouched `DeletionStateChanged` decrements).
  - **New search:** start a fresh search — totals begin at 0 and the selection is empty (any stale post-deletion set entries are wiped by `Clear()` → `DeletionSelection.Clear()`).
