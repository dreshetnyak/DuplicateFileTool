---
id: 007
title: Drive deletion from the unified set (duplicates + non-duplicates)
phase: Phase 2 — Deletion pipeline
status: done
depends_on: [001, 003, 006]
touches_files:
  - DuplicateFileTool/DuplicatesRemover.cs
  - DuplicateFileTool/DuplicatesEngine.cs
  - DuplicateFileTool/DeletionSelection.cs
  - DuplicateFileTool/FileSystem.cs
user_stories: [24, 26]
---

> **Implementation note (orchestrator) — follow precisely:**
> **Totals coordination (avoid double-decrement):** today `DuplicatesEngine.DeletionStateChanged` owns the THROTTLED per-file decrement of `ToBeDeletedCount`/`ToBeDeletedSize` (and `_duplicatedTotalSize` + progress). Keep that exactly. To keep the set consistent without double-counting and without unthrottled UI churn, the remover must remove each deleted path from the set **silently**: add `DeletionSelection.RemoveSilent(string path)` that removes the file entry and adjusts the internal size total but raises NO `Changed` event and does NOT evict ancestor directories (the directories set must survive for issue 008 to force-remove selected folders). DeletionStateChanged stays the single source of the totals decrement.
> **Non-duplicate pass needs FileData:** the set stores path→size only. Add `FileData FileSystem.GetFileData(string path)` (single-file `FindFirstFile` on `MakeLongPath(path)`, build `new FileData(dirPath, findData)`) so the non-duplicate pass deletes with real attributes (readonly handling, recycle, long path). Do NOT change the set's value type.
> **Set enumeration:** add `IReadOnlyCollection<string> DeletionSelection.GetFilePaths()` (snapshot of file keys under the lock).
> **Two passes:** keep the existing group-walk (pass 1) for duplicates; extract the per-file delete (recycle check / delete / fallback / sticky / `DeletionStateChanged` delta / empty-dir cleanup / `RemoveSilent`) into a shared helper. Pass 1 calls it per marked duplicate (and still removes the `DuplicateFile` from the group + collapses emptied groups). Pass 2 deletes every set file path that is NOT a duplicate (use an `isDuplicate` predicate passed from the engine, which has `IsDuplicate` from issue 002), via `FileSystem.GetFileData(path)` → the shared helper.
> **Signatures:** extend `DuplicatesRemover.RemoveDuplicates` with `DeletionSelection selection, Func<string,bool> isDuplicate`; set `TotalFilesForDeletionCount = selection.Count`. The engine's `RemoveDuplicates` (line ~787) keeps its public signature and passes `DeletionSelection` + `IsDuplicate` to the remover. `DeleteMarkedFilesCommand` is therefore UNCHANGED (its Enabled gate + post-run refresh are issue 009).

## Description
Make the deletion run delete **every path in the unified set**, not only files reached by walking `DuplicateGroups`. Keep the existing group-walk for duplicate paths (so group/file collections update and emptied groups collapse), and **add a second pass** that deletes set paths not belonging to any group (non-duplicates). Reuse the existing recycle / permanent / fallback / sticky logic for both passes. Remove each successfully deleted path from the set. Set `TotalFilesForDeletionCount` from the set count.

Grounding (`DuplicateFileTool/DuplicatesRemover.cs`):
- `TotalFilesForDeletionCount` summed from marked dups (line 73).
- Per-file delete loop with recycle/fallback/sticky (133–231); removes deleted file from group (line 200); removes a group once ≤1 file (105–111).
- `RecycleFailureDecision` enum {Cancel, Ignore, DeletePermanently} (52); sticky `_stickyRecycleDecision` (68, 233–243).
- `DuplicatesEngine.RemoveDuplicates` (526–534) delegates here; `DeleteMarkedFilesCommand.Execute` (40) calls it; `DeletionStateChanged` (DuplicatesEngine.cs 547–565) adjusts totals as files are removed.

## Acceptance criteria
- A run with mixed duplicate + non-duplicate marks deletes **all** of them.
- The progress count covers all marked paths (dup + non-dup).
- Recycle-failure prompt and the sticky "apply to all" decision still work for both passes.
- Totals drop to reflect the deletions; deleted paths leave the set.
- **Dup-only runs behave exactly as before** (group collapse, per-file removal, progress).

## Manual verification
Build, run, find duplicates. PRD scenario 9 (partial — non-dup deletion needs the control from 016+ to mark; until then verify the dup-only path is unchanged and that any set entry added programmatically is deleted). Full mixed-selection run is the end-to-end check once the control exists.

## Manual verification performed
- `dotnet build DuplicateFileTool/DuplicateFileTool.csproj -nologo` → Build succeeded, 0 Warning(s), 0 Error(s).
- Code review of the deletion path:
  - `TotalFilesForDeletionCount = selection.Count` (covers dup + non-dup marked paths).
  - Pass 1 (`DeleteSelectedFilesInGroupsCollection`/`DeleteSelectedFilesInGroup`) still walks groups, removes the deleted `DuplicateFile` from its group via the dispatcher, and collapses groups to ≤1 file → group `RemoveAt` (unchanged from issue 006). A skipped/ignored duplicate (`TryDeleteFile` returns false) stays in its group AND in the set; pass 2 excludes it because `IsDuplicate(path)` is still true.
  - Pass 2 (`DeleteNonDuplicateSetFiles`) iterates `selection.GetFilePaths()`, deletes every path where `!isDuplicate(path)` via `FileSystem.GetFileData` → `TryDeleteFile`. Gone/inaccessible paths (`FileData.Empty`) are logged via the existing error path and skipped; cancellation is honored between files.
  - Double-decrement avoided: `DeletionStateChanged` (unchanged, throttled) owns the displayed-totals decrement; `TryDeleteFile` calls `selection.RemoveSilent` which adjusts only the internal set size and raises no `Changed` event, so set `Count`/`Size` and the engine totals converge on the same post-deletion values.
  - Directories set is untouched by `RemoveSilent` (issue 008 still has `GetSelectedDirectories`).
- End-to-end mixed-selection run deferred to the UI control from issue 016+ (no control yet to mark non-duplicates), per the issue's note.
