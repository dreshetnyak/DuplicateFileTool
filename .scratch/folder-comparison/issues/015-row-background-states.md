---
id: 015
title: Row background states — red zero-survivor, belonging highlight, precedence
phase: Phase 4 — Folder tree rendering
status: done
depends_on: [013, 014]
touches_files:
  - DuplicateFileTool/Controls/FolderTree.xaml
user_stories: [6, 27, 29]
---

## Description
Style folder-tree rows by binding to the `FolderItem` flags (013):
- **Zero-survivor** → red row background.
- **Belonging-file** (current group's duplicate in this folder) → a special highlight background.
- When both apply to the same row, **red takes precedence** (belonging highlight suppressed).

These are *row* backgrounds; the "selected in results tree" treatment is a separate *outer* sub-control background (021) and must not conflict.

Grounding: same resource file as 014 (`Controls/FolderTree.xaml`); flags from 013.

## Acceptance criteria
- A marked non-duplicate (or marked duplicate whose whole group is marked) row shows a red background.
- A current-group duplicate row shows the belonging highlight.
- When a row is both, it shows red (highlight suppressed).

## Manual verification
PRD scenario 1 (belonging highlight) and 3/5 (red zero-survivor): select a group, mark a non-duplicate → its row turns red; the group's duplicate rows show the belonging highlight; a row that is both shows red.

## Manual verification performed
No host yet — verified by build + XAML inspection (per issue instructions).
- Added two keyed `SolidColorBrush` resources to `FolderTree.xaml` `UserControl.Resources`: `BelongingHighlightBrush` (#FFE3F0FF, subtle steel-blue) and `ZeroSurvivorDangerBrush` (#FFFFD6D6, light-red danger).
- Added `Style.Triggers` to `FolderTreeItemStyle` (local container style only — results tree uses the global container style and is unaffected):
  - DataTrigger `BelongsToCurrentGroup == True` → `Background = BelongingHighlightBrush` (listed first).
  - DataTrigger `IsZeroSurvivor == True` → `Background = ZeroSurvivorDangerBrush` (listed LAST).
- Red precedence: when both flags are true, the zero-survivor DataTrigger is the last matching trigger, so its `Background` setter wins over the belonging one (WPF last-matching-trigger-wins). Verified by trigger ordering.
- Selection nuance: the global `TreeListViewItem` template binds the row Border `Bd.Background` to `{TemplateBinding Background}` and overrides it to the highlight brush on `IsSelected`. So these tints render on UNSELECTED rows; the selected row still shows the system selection color. Acceptable per issue 015/016 (danger is also carried by red on the other group rows and the issue-016 warning text).
- Directory rows: both flags are always false (per `FolderItem`), so directory rows get no tint.
- Backgrounds update live: `IsZeroSurvivor` / `BelongsToCurrentGroup` raise PropertyChanged (FolderItem `OnDeletionSelectionChanged` / `OnEnginePropertyChanged`), and the OneWay DataTriggers re-evaluate on those notifications.
- Build: `dotnet build DuplicateFileTool/DuplicateFileTool.csproj -nologo -clp:ErrorsOnly` → Build succeeded, 0 Warning(s), 0 Error(s).
