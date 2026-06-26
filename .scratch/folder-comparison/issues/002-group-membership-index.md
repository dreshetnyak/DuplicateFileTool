---
id: 002
title: Build path → duplicate-group membership index with classification + zero-survivor query
phase: Phase 1 — Engine selection model
depends_on: [001]
touches_files:
  - DuplicateFileTool/DuplicatesEngine.cs
user_stories: [17, 27]
status: done
---

## Description
From `DuplicatesEngine.DuplicateGroups`, maintain an index that answers:
1. Is this path a **duplicate** (a member of any group)? Which group?
2. Would deleting the current selection leave **zero surviving copies** of this path — i.e. it is a marked non-duplicate, OR it is a duplicate whose every group copy is in the selection set?

The index must stay correct as groups are **added** during search and **mutated** during deletion. Keep it pure logic alongside the selection set (001), no WPF references.

Grounding: groups are created in `DuplicatesEngine.cs` (group creation at line 344); `DuplicateGroup` (lines 66–176) holds `DuplicateFiles`; `DuplicateFile.ParentGroup` is currently private (line 20). This index is what later lets a `FolderItem` and the remover classify any path without walking the whole group collection.

## Acceptance criteria
- For a non-duplicate path: "zero-survivor" is true whenever the path is in the selection set.
- For a duplicate path: "zero-survivor" is true only when **all** of its group's files are in the set.
- Classification (duplicate vs non-duplicate) and the zero-survivor answer flip correctly as the last sibling of a group is marked/unmarked.
- The index updates when groups are added during a search and when files/groups are removed during deletion.
- **Existing behavior unchanged:** results-tree marking and totals behave as before.

## Manual verification
Build. Find duplicates with a group of ≥3 copies. (Observable later via 013/015's red rows.) For now, confirm the app still finds and lists groups identically and existing marking is unaffected.

## Manual verification performed
- Build: `dotnet build DuplicateFileTool/DuplicateFileTool.csproj -nologo -clp:ErrorsOnly` → **Build succeeded, 0 Warnings, 0 Errors**.
- Scope check: only `DuplicatesEngine.cs` was modified. `DuplicateFile.IsMarkedForDeletion`, `IsMarkForDeletionVisible`, the marking commands, `MainViewModel`, `DuplicatesRemover`, and the UI are untouched; `DuplicateFile.ParentGroup` is left private. The index is additive (new fields + a second `CollectionChanged` subscription that runs alongside the existing one), so existing runtime behavior — group finding, listing, results-tree marking and totals — is unchanged.
- No test project exists, so the following is how a human verifies the new query API at runtime:
  - Run a search that yields a group of ≥3 copies (A, B, C) plus an unrelated standalone file X.
  - `IsDuplicate`/`GetGroupForPath`: A/B/C resolve to the same group instance; X resolves to `null` / not-a-duplicate. Keys are case-insensitive and long-path-prefix tolerant (uses `DeletionSelection.Normalize`).
  - Zero-survivor flip (duplicate): with the live `DeletionSelection`, `WouldLeaveZeroSurvivors(A)` stays `false` while at least one of A/B/C is unmarked, and flips to `true` only once the last sibling is added to the selection; removing any sibling flips it back to `false`. Queried on demand against the live selection, so no stale snapshot.
  - Zero-survivor (non-duplicate): `WouldLeaveZeroSurvivors(X)` equals `DeletionSelection.Contains(X)`.
  - Index correctness across mutations: groups added incrementally during the search are indexed as they arrive (via `DuplicateGroups.CollectionChanged` Add); after a deletion run, files removed from a group (`RemoveAt`) drop out of the index and a group dropped to ≤1 file (group `RemoveAt`) removes its remaining key, so `IsDuplicate`/`GetGroupForPath` return no stale entries. A new search clears everything via the `Reset`, which also unsubscribes every per-group handler so nothing leaks across runs.
