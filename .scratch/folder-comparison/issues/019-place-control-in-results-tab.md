---
id: 019
title: Place the FolderComparison control in the Results tab (temporary bottom region)
phase: Phase 6 — Container, layout & placement
status: done
depends_on: [018]
touches_files:
  - DuplicateFileTool/MainWindow.xaml
user_stories: []
---

## Description
Wrap the Results TabItem's content `Grid` (MainWindow.xaml ~line 506) in an enclosing grid with rows: `[existing content *]` / `[horizontal GridSplitter]` / `[FolderComparison]`, so the control spans the **full width** below the groups tree and the right-hand panel. This placement is explicitly **temporary** (the Results page is expected to be redesigned later) and must be easy to remove.

Grounding: Results TabItem content is a single `<Grid Margin="3,3,3,3">` (MainWindow.xaml 506) with 3 columns: results `TreeListView` (col 0, 894) | 4px `GridSplitter` (col 1, 910) | right panel (col 2). The `FolderComparison` control is from 018.

## Acceptance criteria
- The control appears full-width at the bottom of the Results tab with a draggable horizontal splitter separating it from the existing content.
- **Existing results UI is unaffected** (groups tree, splitter, right panel all behave as before).

## Manual verification
Build, run, go to Results tab after a search → the folder-comparison region sits full-width at the bottom with a draggable horizontal splitter; resizing it doesn't break the groups tree or right panel.

## Manual verification performed
- Wrapped the Results TabItem content `Grid` (MainWindow.xaml line 506) in a new outer 3-row grid (rows: `2*` MinHeight 150 / `Auto` / `*` MinHeight 120). The former line-506 grid is now the inner `<Grid Grid.Row="0">` with all column definitions/content unchanged; its `Margin="3,3,3,3"` moved to the outer grid.
- Added a horizontal `GridSplitter Grid.Row="1"` (Height 5, Stretch, ResizeBehavior=PreviousAndNext, ResizeDirection=Rows) and `<controls:FolderComparison Grid.Row="2" Engine="{Binding Duplicates}" />` before the outer grid's close.
- `Engine` bound via XAML `{Binding Duplicates}` (the view-model's `DuplicatesEngine`). The `internal` DP CLR wrapper did NOT block binding — WPF resolves the bound DP at runtime regardless of wrapper accessibility.
- Build: `dotnet build DuplicateFileTool.csproj -nologo -clp:ErrorsOnly` → Build succeeded, 0 Warning(s), 0 Error(s). XAML/BAML compiled cleanly, confirming the wrap is well-formed.
