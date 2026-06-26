---
id: 006
title: Remove the all-marked-group auto-unmark guard in the remover
phase: Phase 2 — Deletion pipeline
status: done
depends_on: [003]
touches_files:
  - DuplicateFileTool/DuplicatesRemover.cs
user_stories: [31]
---

## Description
Delete the remover branch that warns and auto-unmarks a group when **all** its files are marked, so a fully-marked group is actually deleted. The new control may legitimately produce a fully-marked group (relaxed, asymmetric protection). The results-tree-only last-copy protection still lives in `IsMarkForDeletionVisible` (DuplicatesEngine.cs 29) and is unaffected.

Grounding: `DuplicatesRemover.cs` — the `unmarkedFilesCount < 1` guard (lines 96–101) and `UnmarkAll` (117–131).

## Acceptance criteria
- Deleting a group with **every** file marked removes all of them and drops the group.
- Deleting a group with at least one unmarked copy behaves exactly as today.
- The results tree still hides the mark button on the last unmarked copy (protection unchanged there).

## Manual verification
Build, run, find duplicates. Until the new control exists you can't fully mark a group from the UI — verify regression-only here: a normal dup-only deletion (one copy left unmarked) deletes the marked copies and collapses groups exactly as before. Full-group deletion is verified end-to-end in PRD scenario 5 once 007 + the control land.

## Manual verification performed
- Build: `dotnet build DuplicateFileTool/DuplicateFileTool.csproj -nologo -clp:ErrorsOnly` → Build succeeded, 0 Warning(s), 0 Error(s).
- Grep confirms no remaining `UnmarkAll` reference in the source tree.
- Code-path review: a fully-marked group now falls through to `DeleteSelectedFilesInGroup`, which removes every marked file from the group's `DuplicateFiles`; afterward `duplicateFiles.Count` is 0 (≤1), so the existing group-removal block drops the now-empty group. A group with at least one unmarked copy leaves `Count > 1` and `continue`s exactly as before.
- How a human verifies: run the app, find duplicates, mark all-but-one copy in some groups, delete — marked copies are removed and single-survivor groups collapse, identical to prior behavior. Full-group deletion becomes exercisable once 007 + the folder control exist (PRD scenario 5).
