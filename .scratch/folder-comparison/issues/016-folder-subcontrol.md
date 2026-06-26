---
id: 016
title: Folder sub-control — header (path + clear) + tree + warning line
phase: Phase 5 — Folder sub-control
status: done
depends_on: [013, 014, 015]
touches_files:
  - DuplicateFileTool/Controls/FolderComparisonItem.xaml
  - DuplicateFileTool/Controls/FolderComparisonItem.xaml.cs
user_stories: [28, 38]
---

## Description
Create a sub-control (`FolderComparisonItem`) that composes, top to bottom: a **header** (folder path, clipped with a full-path tooltip; a **clear** button reusing `Clear.png`/`Reset.png`), the P4 folder tree (014/015), and a **warning text line** under the tree, shown only when the tree contains any zero-survivor marked file. The header path clipping must keep the column tidy while exposing the full path on hover.

Grounding: hosts the `FolderTree` rendering (014/015) bound to a `FolderItem` root; warning visibility derives from any descendant zero-survivor flag (013). Per-folder clear wiring is 017.

## Acceptance criteria
- Header shows the folder path; when clipped, the tooltip reveals the full path.
- The warning line appears only while a zero-survivor file is marked in that tree, and disappears when none remain.
- The tree renders with the columns/backgrounds from 014/015.

## Manual verification
PRD scenarios 1, 3: once hosted (018), each folder column shows its path header (tooltip on hover) and the tree; marking a non-duplicate shows the red row **and** the warning line under that tree.

## Manual verification performed
- Created `DuplicateFileTool/Controls/FolderComparisonItem.xaml` + `.xaml.cs` — header (path TextBlock with `TextTrimming="CharacterEllipsis"` + full-path `ToolTip` + Clear image button) / `FolderTree` / warning line.
- Build (no host yet, 018): `dotnet build DuplicateFileTool/DuplicateFileTool.csproj -nologo -clp:ErrorsOnly --no-incremental` → **Build succeeded, 0 Warning(s), 0 Error(s)**.
- Verified the XAML compiled: `obj/{Debug,Release}/Controls/FolderComparisonItem.baml` + `.g.cs` generated, so both files are in the build.
- Code inspection: `Root`/`Engine` DPs drive `DataContext = Root` (header `{Binding FullName}`, tree `{Binding Children}`); `ShowWarning` read-only DP recomputed on `DeletionSelection.Changed`, on `Engine.PropertyChanged`(`CurrentComparisonGroup`), and once on Root/Engine set; weak subscriptions via `WeakEventManager` (with detach on engine re-set); null-guarded throughout; Clear button → `Engine.DeletionSelection.RemoveAllUnder(Root.FullName)`.
- No runtime/visual verification possible until the host (018) instantiates the control.
