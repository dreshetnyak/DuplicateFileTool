---
id: 009
title: Delete-enabled gate + post-run cleanup for the unified set
phase: Phase 2 — Deletion pipeline
status: done
depends_on: [004, 007, 018]
touches_files:
  - DuplicateFileTool/Commands/DeleteMarkedFilesCommand.cs
user_stories: [34]
---

> **Implementation note (orchestrator):** Scoped to the Delete-enabled gate. The set is already pruned during the run by issue 007 (`RemoveSilent` per deleted file), so no extra post-run prune is needed. The post-run **folder-control refresh** (and OQ-6 rebuild-from-selected-row) is reassigned to issue 020, which owns `CurrentComparisonGroup` and the `MainViewModel` wiring (`DeleteMarkedFiles.Finished`) — so `MainViewModel.cs` is NOT touched here. This issue's change: `DeleteMarkedFilesCommand`'s post-run `Enabled` recompute now derives from `Duplicates.ToBeDeletedCount != 0` instead of a duplicate-group walk, so Delete stays enabled for non-duplicate-only selections.

## Description
Derive `DeleteMarkedFilesCommand.Enabled` from the set / `ToBeDeletedCount` (so Delete is enabled even when only non-duplicates are marked). After a run, prune deleted paths from the set and refresh the new folder control alongside the existing `RefreshExpandedFileTreeItems`.

Grounding:
- `DeleteMarkedFilesCommand.cs` recomputes `Enabled` by walking groups (line 44) — change to the set/`ToBeDeletedCount`.
- `MainViewModel.cs`: `DeleteMarkedFiles.Finished` → `RefreshExpandedFileTreeItems` (line 306); also refresh the `FolderComparison` control (from 018).
- **OQ-6 — DECIDED:** after a run the control rebuilds from the still-selected results row; if that row was deleted, it falls back to the empty placeholder.

## Acceptance criteria
- Delete is enabled when only non-duplicates are marked (set non-empty).
- After a run, the control and totals reflect what remains; deleted paths are gone from the set.
- The search-page tree still refreshes after a run (existing behavior).

## Manual verification performed
- Build: `dotnet build DuplicateFileTool/DuplicateFileTool.csproj -nologo -clp:ErrorsOnly` → Build succeeded, 0 Warning(s), 0 Error(s).
- `DeleteMarkedFilesCommand` finally block now sets `Enabled = Duplicates.ToBeDeletedCount != 0` (was a duplicate-group walk). Combined with `MainViewModel.OnDuplicatesPropertyChanged` (which already gates `DeleteMarkedFiles.Enabled` on `ToBeDeletedCount`), Delete is enabled whenever anything is marked, including a non-duplicate-only selection.
- Set pruning after a run is handled by issue 007 (`RemoveSilent`). Post-run folder-control refresh + OQ-6 are implemented in issue 020.
- Human check (once 016+/020 allow marking non-duplicates): mark only non-duplicate files → Delete button is enabled; run it; afterward totals reflect what remains.

## Manual verification
PRD scenario 9: run a mixed deletion; afterward the folder control updates, totals reflect remaining marks, and deleted files are gone. Confirm Delete enables with a non-dup-only selection.
