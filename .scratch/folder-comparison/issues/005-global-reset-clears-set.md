---
id: 005
title: Global Reset clears the entire unified set
phase: Phase 1 — Engine selection model
status: done
depends_on: [001, 004]
touches_files:
  - DuplicateFileTool/Commands/ResetSelectionCommand.cs
  - DuplicateFileTool/MainViewModel.cs
user_stories: [23]
---

> **Implementation note (orchestrator):** The current per-file walk over `DuplicateGroups` only clears DUPLICATE marks; it misses non-duplicate and folder marks that live in the set but not in any group. Replace it with a single `DeletionSelection.Clear()` (which fires one `Reset` → engine zeroes totals, all rows refresh, and the directories set clears too). This means `ResetSelectionCommand` needs the `DeletionSelection`: change its ctor to take `DeletionSelection` instead of `ObservableCollection<DuplicateGroup>` and update the construction in `MainViewModel.cs` to pass `Duplicates.DeletionSelection`. Keep the `Enabled` gating as-is (driven by `MainViewModel.OnDuplicatesPropertyChanged` on `ToBeDeletedCount != 0`).

## Description
Change `ResetSelectionCommand` so it clears the **entire unified set** (duplicate + non-duplicate + folder marks) via the set's `Clear()`, rather than walking the duplicate groups and unmarking each. Being unified, this also clears every results-tree mark and (once the new control exists) every folder-tree mark.

Grounding: `ResetSelectionCommand.cs` `DeselectAll` (lines 22–34) currently walks groups, unmarks each marked file, and fires `UpdateToDeleteSize(-1, -size)`.

## Acceptance criteria
- After Reset, totals are 0 and the Reset/Delete buttons disable (gating on `ToBeDeletedCount != 0`).
- Every results-tree mark is cleared.
- Once the folder control exists (later issues), all folder-tree marks clear too.
- **Existing behavior unchanged:** Reset still clears dup-only selections exactly as before, plus now anything else in the set.

## Manual verification
Build, run, find duplicates, mark several files, click Reset — all marks clear, totals → 0, Reset/Delete disable. (PRD scenario 7.)

## Manual verification performed
- Build: `dotnet build DuplicateFileTool/DuplicateFileTool.csproj -nologo -clp:ErrorsOnly` → Build succeeded, 0 Warning(s), 0 Error(s).
- `ResetSelectionCommand` now takes `DeletionSelection` and `Execute` calls `DeletionSelection.Clear()` (single `Reset` → engine zeroes `ToBeDeletedCount`/`ToBeDeletedSize`, every row refreshes, directories set clears). Per-file `DeselectAll` walk and the `DuplicateGroups` dependency removed.
- `MainViewModel` constructs `new ResetSelectionCommand(Duplicates.DeletionSelection)`; `Enabled` gating in `OnDuplicatesPropertyChanged` (on `ToBeDeletedCount != 0`) left untouched.
- Grep confirms the old `ResetSelectionCommand(ObservableCollection<DuplicateGroup>)` ctor has no remaining callers.
- How a human verifies (PRD scenario 7): run, find duplicates, mark a duplicate file and (once available) a non-duplicate / folder mark, click Reset — every mark (duplicate, non-duplicate, folder) clears, the to-be-deleted totals go to 0, and the Reset/Delete buttons disable.
