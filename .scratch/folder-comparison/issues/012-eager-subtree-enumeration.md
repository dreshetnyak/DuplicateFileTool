---
id: 012
title: Eager background subtree enumeration on folder mark/unmark
phase: Phase 3 — FolderItem view-model
status: done
depends_on: [001, 010, 011]
touches_files:
  - DuplicateFileTool/FolderItem.cs
  - DuplicateFileTool/DeletionSelection.cs
  - DuplicateFileTool/DuplicatesEngine.cs
user_stories: [19]
---

## Description
When a directory `FolderItem` is marked, walk its subtree on a **background thread** (`DirectoryEnumeration`, skipping reparse points) and add every file path (with size) to the unified set; when unmarked, remove them. Totals update live. The UI must stay responsive during a large scan (busy indication is surfaced separately in 022).

**OQ-5 — DECIDED:** cancel a folder's in-flight scan if it is unmarked or the current group changes; apply the set updates for a folder atomically (no partial half-marked folder left behind on cancel).

**OQ-1 hook (explicitly-selected directories set):** when a directory is marked, also record its normalized path in an engine-owned **explicitly-selected-directories** set (sibling to the file-path set in `DeletionSelection.cs`); remove it on unmark. This set is consumed by issue 008 to force-remove user-selected folders regardless of the `RemoveEmptyDirectories` setting. Ensure the set service's `Clear()` and "remove all under prefix" operations also clear matching directory entries (so issues 005 and 017 cover it for free).

Grounding: `DirectoryEnumeration` (used by `FileTreeItem.LoadChildren`, line 104); reparse skip via `FileData.IsReparsePoint`; unified set + new dir-set from 001/`DeletionSelection.cs`.

## Acceptance criteria
- Marking a **collapsed** folder marks all descendants and raises totals by the subtree's file count/size even though the tree wasn't expanded.
- Unmarking reverses exactly (every previously-added descendant path is removed).
- Reparse points are not traversed during the subtree walk.
- The UI stays responsive during a large scan (work is off the UI thread).
- Unmarking a folder mid-scan (or switching groups) cancels the scan and leaves no partial marks.
- Marking a directory records it in the explicitly-selected-directories set; unmarking removes it.

## Manual verification
PRD scenario 4: mark a collapsed folder containing many files/subfolders → totals jump by the full subtree count/size; the folder shows the binary marked state; unmark → totals return. App remains responsive (busy cue verified in 022).

## Manual verification performed
- `dotnet build DuplicateFileTool/DuplicateFileTool.csproj -nologo -clp:ErrorsOnly` → Build succeeded, 0 Warning(s), 0 Error(s).
- Code-traced the acceptance criteria against the implementation (no test project exists; runtime is WPF/Windows-only):
  - **Mark collapsed folder**: directory setter `value=true` → `StartMarkScan` walks the subtree off-thread via `DirectoryEnumeration` per level and, on completion only, commits on the dispatcher: `DeletionSelection.AddDirectory(FullName)` + `Add(file, size)` per file. Each `Add` fires `Added`, so the engine totals rise by the subtree's file count/size; the getter returns true via `ContainsDirectory(FullName)` even while collapsed.
  - **Unmark reverses exactly**: setter `value=false` → `UnmarkSubtree` → `RemoveAllUnder(FullName)` removes every descendant file entry (one `Removed` event each, restoring totals) and drops the dir + sub-dirs from the directories set (silently). Getter returns false.
  - **Reparse dirs not traversed**: `CollectSubtreeFiles` continues past `item.Attributes.IsReparsePoint` directories without recursing; reparse-point files are collected as normal files.
  - **UI responsive**: walk runs under `Task.Run`; only the atomic commit uses `Dispatcher.Invoke`.
  - **Cancel mid-scan / `CancelPendingScan`**: `token.ThrowIfCancellationRequested()` before commit plus a re-check inside the dispatcher → no partial marks.
  - **Unmark any descendant evicts the dir**: file `Remove` calls `EvictAncestorDirectories`, dropping every ancestor from the directories set so the folder no longer shows marked.
  - **Directory-set ops are silent**: `AddDirectory`/`RemoveDirectory`/`RemoveAllUnder` (dir portion) never raise `Changed`, so the engine never miscounts a directory as a file.
