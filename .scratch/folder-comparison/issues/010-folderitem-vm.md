---
id: 010
title: Create FolderItem node view-model (lazy load, shell icons, sort, reparse skip)
phase: Phase 3 — FolderItem view-model
status: done
depends_on: []
touches_files:
  - DuplicateFileTool/FolderItem.cs
user_stories: [8, 9, 10, 32]
---

## Description
Add a new node view-model (`FolderItem.cs`) for the folder-comparison trees. It mirrors the search-page `FileTreeItem`'s placeholder lazy-load and `FileSystemIcon` open/close icons and dirs-first `SortFileData`, **but without** the search tree's static `ItemSelected` event and `Refresh`/activate behaviors (to avoid cross-impact on the Search page). Expose: `Name`, `Size`, `LastModified`, `IsDirectory`, `Children`, `IsExpanded`. Reparse-point entries (`FileData.IsReparsePoint`) are shown as leaves and **never enumerated** (no expand). All entries are shown including hidden/system and zero-size files.

Grounding:
- `FileTreeItem.cs`: placeholder lazy-load (18–19, 31–40, 97–119); icons `FileSystemIcon.GetImageSource(path, ItemState.Open/Close)` (94–95); enumeration `new DirectoryEnumeration(path)` (104); `SortFileData` dirs-first (151–159); static `ItemSelected` + `Refresh` (121–149) — **leave these out**.
- `FileData.IsReparsePoint` (FileData.cs 54–57). Reuse (read-only): `FileSystemIcon.cs`, `DirectoryEnumeration.cs`, `FileData.cs`.

## Acceptance criteria
- Expanding a directory lists its real contents with correct file-type icons, sizes, dates, directories first.
- A junction/symlink shows as a row but does not expand and is never enumerated.
- Deep trees load on demand (placeholder pattern), not eagerly, for display.
- No dependency on the search tree's `ItemSelected`/`Refresh` (Search page behavior untouched).

## Manual verification
This is a VM with no view yet; verify via 014 once rendered. Build must succeed. (PRD scenarios 1, 10 become observable after 014/018.)

## Manual verification performed
- Build: `dotnet build DuplicateFileTool.csproj -nologo -clp:ErrorsOnly` -> Build succeeded, 0 Warning(s), 0 Error(s).
- Correctness verified by inspection (no view yet): `FolderItem` mirrors `FileTreeItem`'s placeholder lazy-load (shared static `ChildPlaceholder`, `IsExpanded` triggers `LoadChildren`/`RemoveChildren` + `UpdateIcon`), open/close shell icons via `FileSystemIcon.GetImageSource(FullName, ItemState.Open/Close)`, on-demand enumeration via `new DirectoryEnumeration(FullName)`, and dirs-first `SortFileData`. Reparse points expose `IsReparsePoint`/`CanExpand=false`, get no placeholder child, and `LoadChildren` early-returns for them (no recursion into junction targets). The search tree's static `ItemSelected` event and `Refresh`/activate behavior are intentionally absent.
- Once issue 014 renders a TreeView bound to `Children`/`IsExpanded`/`Name`/`Size`/`LastModified`/`Icon`, a human will: expand a directory and confirm real contents appear with correct shell icons, sizes, and last-modified dates, directories listed before files, hidden/system and zero-size entries included; confirm a junction/symlink row shows but has no expander and is never enumerated; and confirm deep trees only load a level when expanded.
