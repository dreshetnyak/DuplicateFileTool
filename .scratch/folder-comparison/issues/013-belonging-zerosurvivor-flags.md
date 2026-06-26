---
id: 013
title: Belonging-file and zero-survivor flags on FolderItem
phase: Phase 3 — FolderItem view-model
status: done
depends_on: [002, 010]
touches_files:
  - DuplicateFileTool/FolderItem.cs
  - DuplicateFileTool/DuplicatesEngine.cs
user_stories: [6, 27]
---

> **Implementation note (orchestrator):** The flags need the engine's classification (`WouldLeaveZeroSurvivors`, `GetGroupForPath` from issue 002) and a "current group" reference. Add `DuplicatesEngine.CurrentComparisonGroup` (nullable `DuplicateGroup`, settable, raises `PropertyChanged` via the `NotifyPropertyChanged` base) as the current-group holder — issue 020 will DRIVE it from results-tree selection; 013 only adds it (initially never set, so belonging is false until 020 wires it). Thread the engine into `FolderItem`: change its ctor to take `DuplicatesEngine engine` instead of the bare `DeletionSelection` (keep an internal `DeletionSelection` property sourced from `engine.DeletionSelection` so the existing 011/012 code is unchanged), and pass the engine to children in `LoadChildren`. Flags (file nodes only; directories return false):
> - `IsZeroSurvivor` → `engine.WouldLeaveZeroSurvivors(FullName)`.
> - `BelongsToCurrentGroup` → `engine.CurrentComparisonGroup != null && ReferenceEquals(engine.GetGroupForPath(FullName), engine.CurrentComparisonGroup)`.
> Recompute (extend the existing weak `OnDeletionSelectionChanged`): on any selection change (Added/Removed/Reset), re-raise `IsZeroSurvivor` for file nodes (a duplicate's zero-survivor can flip when ANY group sibling — possibly in another folder/tree — is marked, so don't restrict to own-path here). Also subscribe weakly to `engine.PropertyChanged` and, when `CurrentComparisonGroup` changes, raise `BelongsToCurrentGroup` (and `IsZeroSurvivor`). Keep it leak-free (weak handlers, mirroring the existing pattern).

## Description
Expose two per-row flags on `FolderItem` that the view (015) binds to:
- **Belongs to the current group** — this file is one of the current group's duplicates living in this folder.
- **Zero-survivor** — queried from the engine index (002): true for a marked non-duplicate, or a marked duplicate whose entire group is marked.

Both flags update when the selection set changes or the current group changes.

Grounding: engine membership/classification index from 002; current group binding arrives in 020 — design the flags to recompute from an injected "current group" reference.

## Acceptance criteria
- The current group's duplicate rows report belonging = true; unrelated rows report false.
- A marked non-duplicate reports zero-survivor = true; a marked duplicate whose whole group is marked reports true; a marked duplicate with a surviving copy reports false.
- Flags update when selection or current group changes.

## Manual verification
Observable once rendered (015) and current-group wired (020). Verify via PRD scenarios 1 (belonging highlight) and 3/5 (red zero-survivor rows).

## Manual verification performed
No UI binds these flags yet (issue 015 will) and there is no test project, so verification is by code review and a clean build:

- `dotnet build DuplicateFileTool/DuplicateFileTool.csproj` → Build succeeded, 0 warnings, 0 errors (full `--no-incremental` rebuild also clean).
- `BelongsToCurrentGroup`: file node returns true only when `Engine.CurrentComparisonGroup` is non-null and `Engine.GetGroupForPath(FullName)` reference-equals it (verified against engine `GetGroupForPath`/`CurrentComparisonGroup` semantics); directory nodes short-circuit to false; with no current group set (initial state) it is false for every node.
- `IsZeroSurvivor`: file node delegates to `Engine.WouldLeaveZeroSurvivors(FullName)` — which returns true for a marked non-duplicate, true for a marked duplicate whose whole group is marked, and false for a marked duplicate with a surviving copy (confirmed by reading that engine method); directory nodes short-circuit to false.
- Live recompute on selection change: `OnDeletionSelectionChanged` re-raises `IsZeroSurvivor` for FILE nodes on every change (Added/Removed/Reset) before the existing own-path/subtree gate, so a group sibling marked in another folder/tree still flips this node's verdict.
- Live recompute on current-group change: weak `OnEnginePropertyChanged` re-raises `BelongsToCurrentGroup` and `IsZeroSurvivor` when `CurrentComparisonGroup` changes.
- Leak-free: both subscriptions (engine `PropertyChanged`, `DeletionSelection.Changed`) use `WeakEventManager`, mirroring the existing pattern, so rebuilt folder trees do not pin stale nodes.
- Confirmed no other caller constructs `FolderItem` with the old `DeletionSelection` ctor (`grep new FolderItem(` → only the internal `LoadChildren` call), so the signature change does not break the build.
