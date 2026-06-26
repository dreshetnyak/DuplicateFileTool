---
id: 008
title: Remove emptied folders after a folder-selection deletion
phase: Phase 2 — Deletion pipeline
status: done
depends_on: [007, 012]
touches_files:
  - DuplicateFileTool/DuplicatesRemover.cs
  - DuplicateFileTool/FileSystem.cs
user_stories: [25]
---

## Description
After deletion, directories that the run emptied are removed (reusing `FileSystem.DeleteDirectoryTreeWithParents`), so a folder the user selected disappears, including emptied sub-folders. Empty directories are removed permanently (no data to recycle).

**OQ-1 — DECIDED (option b):** track explicitly-selected directory paths in a small parallel set, used purely to force removal of those folders regardless of the `RemoveEmptyDirectories` setting; the setting continues to govern the dup-only flow (so existing dup-only behavior is unchanged). The unified file-path set stays file-only.

**OQ-4 — DECIDED:** add a reparse-point guard to `FileSystem.IsDirectoryTreeEmpty`/`DeleteEmptySubDirectories` so empty-dir detection/removal never traverses a junction (story 32, deletion side).

The explicitly-selected-directories set is populated by issue 012 (folder mark); this issue **consumes** it: after the file-deletion passes, force-remove every directory in that set whose tree the run emptied, then run the setting-gated empty-dir cleanup for the dup-only flow.

Grounding: `DuplicatesRemover.cs` empty-dir removal per deleted file's `DirPath`, gated on `removeEmptyDirs` (lines 223–229) via `FileSystem.IsDirectoryTreeEmpty` + `FileSystem.DeleteDirectoryTreeWithParents`; explicitly-selected-directories set lives in `DeletionSelection.cs` (added in 012), read via the engine.

## Acceptance criteria
- Marking and deleting a whole folder removes its files **and** the now-empty folder and emptied sub-folders.
- A folder with some files left over is **not** removed.
- Folder removal does not traverse a directory junction (per OQ-4 resolution).
- **Existing behavior unchanged:** the dup-only empty-dir behavior still honors the `RemoveEmptyDirectories` setting per the OQ-1 resolution.

## Manual verification
PRD scenarios 9 + 10 (deletion side): select a folder in the new control, delete, confirm the folder and emptied sub-folders are gone; confirm a junction inside is not traversed. (End-to-end once the control from 016+ exists.)

## Manual verification performed
- Build: `dotnet build DuplicateFileTool/DuplicateFileTool.csproj -nologo -clp:ErrorsOnly` → Build succeeded, 0 Warning(s), 0 Error(s).
- How a human verifies end-to-end (needs the folder-mark control from 012/016+):
  - With **RemoveEmptyDirectories OFF**: mark a whole folder, run delete → the folder and any sub-folders the run emptied are removed (the new force-removal step runs unconditionally). Dup-only deletions elsewhere leave their now-empty dirs in place (setting still honored).
  - With **RemoveEmptyDirectories ON**: dup-only behavior unchanged — per-file cleanup removes emptied dirs as before; the new step finds the selected folder already gone and skips it (no double work, no errors).
  - Leftover file: leave one file under the marked folder (not selected) → the folder is **kept** (IsDirectoryTreeEmpty returns false).
  - Junction safety: place a directory junction/symlink inside the marked folder → the folder is **kept** (the junction makes the tree non-empty) and the junction is **never traversed** (the reparse entry is never recursed into in either IsDirectoryTreeEmpty or DeleteEmptySubDirectories).
