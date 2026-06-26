---
id: 014
title: Render FolderItem in a TreeListView with mark/icon/name/size/date columns
phase: Phase 4 — Folder tree rendering
status: done
depends_on: [010, 011]
touches_files:
  - DuplicateFileTool/Controls/FolderTree.xaml
  - DuplicateFileTool/Controls/FolderTree.xaml.cs
user_stories: [8, 11]
---

> **Implementation note (orchestrator):** Build `FolderTree` as a **UserControl** (`Controls/FolderTree.xaml` + `.cs`), NOT a loose ResourceDictionary. It hosts a `controls:TreeListView` (the existing generic multi-column tree; its styles are already globally merged via `App.xaml` → `Controls/TreeListView.xaml`, so do NOT touch `App.xaml` or `TreeListView.cs`). Expose an `ItemsSource` dependency property (IEnumerable of `FolderItem`) that the inner `TreeListView.ItemsSource` binds to; the host (016) will bind it to the root folder's `Children`.
> - **Columns:** `[mark | Name | Size | Last-Modified]`. Name column uses a `HierarchicalDataTemplate` for `FolderItem` (`ItemsSource={Binding Children}`) with the expand toggle + `Icon` + `Name`; Size right-aligned (`Size` → format like the results tree, e.g. a `BytesLengthToString` converter or reuse the pattern); Last-Modified from `LastModified`.
> - **Mark cell:** mirror the results tree's Delete.png / Undo.png two-button visibility pattern, but with NO last-copy hiding (the folder control may mark anything). Toggle via a **code-behind Click handler** that does `item.IsMarkedForDeletion = !item.IsMarkedForDeletion` — do NOT use a two-way-bound CheckBox: a directory's setter starts an async scan and its getter only flips after the scan commits, so a two-way control would visually revert. No external command needed.
> - **Strikethrough:** the results tree's `MarkedFileTextStyle` lives in `MainWindow.xaml` Window.Resources and is NOT visible here — define a local equivalent in `FolderTree` resources binding `FolderItem.IsMarkedForDeletion`.
> - **Item container style:** set the inner `TreeListView.ItemContainerStyle` to a style `BasedOn` the global `{x:Type controls:TreeListViewItem}` and give it an `x:Key` local to this control, so issue 015 has a dedicated place to add row-background DataTriggers WITHOUT affecting the results tree (which shares the global container style).
> - Converters (`BoolToVisibility`, byte-size): declare local instances in this control's resources (the converter classes live in `DuplicateFileTool.Converters`).

## Description
Render `FolderItem` trees with the existing per-instance multi-column `TreeListView`. Define the columns (a left **mark-button** column + Name / Size / Last-Modified) and a hierarchical template binding `Children`, in a new `ResourceDictionary` (`Controls/FolderTree.xaml`) merged via `App.xaml`. Reuse the results tree's Delete/Undo mark-button pattern and the `MarkedFileTextStyle` strikethrough. The mark button toggles the `FolderItem.IsMarkedForDeletion` set-backed setter (011) — no new command needed, or a thin one if cleaner (keep it inside this issue's files).

Grounding:
- `Controls/TreeListView.cs` (`TreeListView : TreeView`, per-instance `GridViewColumnCollection Columns`, lines 13–19; `DuplicateCellTemplateSelector` 30–44) — reuse; templates currently in `MainWindow.xaml`/`Controls/TreeListView.xaml`.
- Results-tree mark buttons + visibility pattern: MainWindow.xaml 75–90; `MarkedFileTextStyle` strike/gray 49–56; `ImageButton` style ~129–150.
- Icons via `FileSystemIcon` (already on `FolderItem` from 010).

## Acceptance criteria
- Each row shows file-type icon, name, size, last-modified, and a mark button matching the results tree.
- Clicking the mark button marks/unmarks the row's file (set-backed); marked rows show strikethrough.
- Directories show first, then files; expanders work; junctions show but don't expand.
- **Existing behavior unchanged:** the results tree (which also uses `TreeListView`) still renders and behaves as before.

## Manual verification
Build, run; once the control is hosted (018) selecting a group shows trees with icon/name/size/date and a working mark button. Until then, verify the solution builds and the results tree is unaffected. (PRD scenario 1.)

## Manual verification performed
- `dotnet build DuplicateFileTool/DuplicateFileTool.csproj -nologo -clp:ErrorsOnly` → Build succeeded, 0 Warning(s), 0 Error(s).
- No host exists yet, so verification was build + inspection. `MainWindow.xaml` (the results tree) was not modified; `App.xaml`, `TreeListView.cs`, `TreeListView.xaml`, `FolderItem.cs`, and the engine were read-only.
- New files only: `DuplicateFileTool/Controls/FolderTree.xaml` + `FolderTree.xaml.cs`.

Once issue 016/018 hosts `FolderTree` and binds `ItemsSource` to a root folder's `Children`, a human verifies by:
1. Running the app, performing a folder comparison, and opening a group on the folder-comparison view.
2. Confirming each row shows the file-type icon, name, size (right-aligned, human-readable), and last-modified date, with directories listed before files and expanders on directories (junctions render without an expander).
3. Clicking the mark (Delete.png) button on a file marks it immediately (strikethrough/gray, button swaps to Undo.png); clicking on a directory starts the eager scan and the row(s) reflect the marked state when it commits; clicking Undo.png unmarks.
4. Confirming the Search-page results tree still renders and behaves exactly as before (shared global container style untouched).
