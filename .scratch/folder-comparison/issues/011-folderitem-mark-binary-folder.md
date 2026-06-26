---
id: 011
title: Set-backed file mark + binary folder mark on FolderItem
phase: Phase 3 — FolderItem view-model
status: done
depends_on: [001, 010]
touches_files:
  - DuplicateFileTool/FolderItem.cs
user_stories: [12, 13, 14, 15, 16]
---

## Description
Wire `FolderItem` mark semantics to the unified set (001):
- A **file** `FolderItem`'s `IsMarkedForDeletion` is set membership for its path (get = contains, set = add/remove with size).
- A **directory** `FolderItem` reports marked **iff all of its descendant files are in the set** (binary derived state). Clicking a marked folder unmarks all of its descendants (eager enumeration handled in 012).
- Both update live when the set changes for any contained path (subscribe to set change notifications).

Grounding: unified set from 001; reuse `DuplicateFile`'s mark pattern as a model. The recursive subtree walk for marking is implemented in 012; this issue establishes the get/set + live-update wiring and the binary folder-state computation for already-loaded children.

## Acceptance criteria
- Marking a file from the results tree turns the same file marked in the folder tree (and vice-versa).
- A folder shows marked only once **every** descendant file is in the set; unmarking one descendant clears the folder's marked state.
- A sub-directory shows marked exactly when all the files inside it are marked.
- Deselecting a file/folder is the inverse of selecting it.

## Manual verification
Observable once rendered (014) and current-group wired (020): mark a file in the results tree → same file shows marked in the folder tree; mark all files in a subfolder → the subfolder shows marked. (PRD scenarios 5, 4.)

## Manual verification performed
- Build: `dotnet build DuplicateFileTool/DuplicateFileTool.csproj -nologo -clp:ErrorsOnly` → **Build succeeded. 0 Warning(s), 0 Error(s).**
- No view binds `FolderItem.IsMarkedForDeletion` yet (issue 014 renders the tree) and there is no test project, so behavior is not yet observable at runtime. Code paths were verified by inspection against the proven `DuplicateFile` pattern.
- How a human verifies once 014 renders the tree and 020 wires the current group:
  - File node: tick a file in the results tree → the same file shows marked in the folder tree (and the reverse). The `DeletionSelection` set is the single source of truth, so the two views stay in sync.
  - Expanded directory: mark every file under an expanded subfolder → the subfolder's row shows marked; untick one descendant → the subfolder's marked state clears live.
  - Sub-directory: a subfolder shows marked exactly when all of its loaded files are marked; a subfolder that contains only empty subfolders never becomes marked.
  - Live update is driven by `DeletionSelection.Changed` (weak subscription), so marks made from any view propagate without a manual refresh.
  - Collapsed directory (only the placeholder child) reports unmarked and does not mark its hidden subtree — that is deferred to issue 012.
