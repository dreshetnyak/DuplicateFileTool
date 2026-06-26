# Implementation Plan: Folder-comparison control for duplicate groups

Companion to `PRD.md` (Status: ready-for-agent). This plan is grounded in the current code; file paths and method names are real as of this writing. **No code is written here.** Treat the PRD's Implementation Decisions as settled constraints; disagreements live only in *Open questions / concerns* at the end.

## Scope (restated)

Add a Results-tab control that, for the duplicate group selected in the results tree, shows one folder-contents tree per **distinct containing folder**, side by side, so the user can compare folders and mark files *or whole folders* — including non-duplicates — for deletion. All marks (results tree, Auto-Select, and the new control) flow through one engine-owned, path-keyed deletion-selection set so the two views always agree and the deletion run removes everything marked, including emptied folders. Files whose deletion would leave zero surviving copies are flagged red with a warning.

## Foundational seam first

The PRD names the engine's path-keyed selection set as the highest-value seam and wants it separable from WPF. **Phase 1 builds that seam and refactors `DuplicateFile.IsMarkedForDeletion` into a view over it before any new UI exists.** Every later phase depends on Phase 1. Phase 2 (deletion pipeline) depends only on Phase 1 and can proceed in parallel with the UI phases (3–9).

---

## Grounding: current behavior (anchors for the refactor)

- **`DuplicateFile` / `DuplicateGroup`** live in `DuplicateFileTool/DuplicatesEngine.cs` (`DuplicateFile` lines 13–63, `DuplicateGroup` 66–176). `DuplicateFile.IsMarkedForDeletion` (30–49) holds a private bool `_isMarkedForDeletion`, raises `PropertyChanged` for itself and `IsMarkForDeletionVisible` (29), and loops siblings to re-raise `IsMarkForDeletionVisible`. `DuplicateFile` is constructed with `(DuplicateGroup parentGroup, MatchResult matchResult)`; `ParentGroup` is **private** (20). `FileData` is exposed (21).
- **Marking routes today** (all set `IsMarkedForDeletion` directly and separately push count/size deltas):
  - `Commands/ToggleDeletionMarkCommand.cs` `Execute` (15–32): toggles the file, fires `DeletionMarkToggle(±1, ±size)`.
  - `Commands/AutoSelectByPathCommand.cs`: marks at line 118, fires `FilesAutoMarkedForDeletion` (119/171). Has its **own** last-copy guard `IsAfterMarkingAtLeastOneLeft` (99–108) — keep it.
  - `Commands/ResetSelectionCommand.cs` `DeselectAll` (22–34): unmarks each marked file, fires `UpdateToDeleteSize(-1, -size)`.
- **Counts/sizes**: `DuplicatesEngine.ToBeDeletedCount`/`ToBeDeletedSize` (DuplicatesEngine.cs 312–329) are mutated by `MainViewModel.OnUpdateToDelete` (MainViewModel.cs 467–471), which is wired to all three command events (291, 294, 302). `OnDuplicatesPropertyChanged` (367–387) flips `ResetSelection.Enabled` and `DeleteMarkedFiles.Enabled` off `ToBeDeletedCount != 0`. The right-panel "Selected for deletion size" textbox binds `Duplicates.ToBeDeletedSize` (MainWindow.xaml 934).
- **Deletion**: `Commands/DeleteMarkedFilesCommand.cs` `Execute` (27–57) calls `Duplicates.RemoveDuplicates(Duplicates.DuplicateGroups, RemoveEmptyDirectories, DeleteToRecycleBin, PromptRecycleFailure, ctx)` (40) and recomputes `Enabled` by walking groups (44). `DuplicatesEngine.RemoveDuplicates` (526–534) delegates to `DuplicatesRemover`. `DuplicatesRemover` (DuplicatesRemover.cs): `TotalFilesForDeletionCount` summed from marked dups (73); **all-marked guard** warns + `UnmarkAll` + skip (96–101, 117–131); per-file delete loop with recycle/fallback/sticky (133–231); removes deleted file from the group collection (200) and removes a group once it has ≤1 file (105–111); empty-dir removal per deleted file's `DirPath`, gated on `removeEmptyDirs` (223–229) via `FileSystem.IsDirectoryTreeEmpty` + `FileSystem.DeleteDirectoryTreeWithParents`. `DeletionStateChanged` (DuplicatesEngine.cs 547–565) adjusts `_toBeDeletedSize`/`_toBeDeletedCount`/`_duplicatedTotalSize` as files are removed. `DeleteMarkedFiles.Finished` calls `RefreshExpandedFileTreeItems` (MainViewModel.cs 306).
- **Selection event for current-group binding**: `DuplicateFile.IsSelected` (50–62) fires the **static** `DuplicateFile.ItemSelected`; `MainViewModel` subscribes (298) → `OnDuplicateFileSelected` (460–465) sets `AutoSelectByPath.Path`. `DuplicateGroup.IsSelected` (123–131) only raises `PropertyChanged` (no cross-cutting event). The results control is a `controls:TreeListView x:Name="ResultsTreeView"` (MainWindow.xaml 894), a `TreeView` subclass (Controls/TreeListView.cs 13–19) the VM holds a reference to (`MainViewModel.ResultsTreeView`, with a `//TODO get rid of`).
- **Search-page tree node** `FileTreeItem` (FileTreeItem.cs): lazy children via placeholder (18–19, 31–40, 97–119), icons via `FileSystemIcon.GetImageSource(path, ItemState.Open/Close)` (94–95), enumeration via `new DirectoryEnumeration(path)` (104), dirs-first sort `SortFileData` (151–159). Shows icon + name only (no size/date). Carries the static `FileTreeItem.ItemSelected` and an activate/F5 `Refresh` (121–149) — both to be **left out** of the new VM.
- **Reparse detection** exists: `FileData.IsReparsePoint` (FileData.cs 54–57) via `Win32.FileAttributes.ReparsePoint`.
- **Multi-column tree** `controls:TreeListView` (Controls/TreeListView.cs) exposes a per-instance `Columns` (`GridViewColumnCollection`) and uses `DuplicateCellTemplateSelector` (30–44) for the results tree's group-vs-file cells. Styled in `Controls/TreeListView.xaml` (merged via `App.xaml`).
- **Results-tab layout** (MainWindow.xaml 506–999): the TabItem content is a single `Grid` with three columns — results `TreeListView` (col 0) | 4px `GridSplitter` (col 1, line 910) | right panel (col 2): an info `GroupBox` (918) over a button stack with `AutoSelectByPath` (row 0, 945), `ResetSelection` (row 1, 962), `DeleteMarkedFiles` (row 3, 979).
- **Resources**: converters and templates are declared in `MainWindow.xaml` Window.Resources (converters 20–23, `MarkedFileTextStyle` strike/gray 49–56, results cell templates ~58–128, `ImageButton` style ~129–150). New templates/styles can go here or in a new `ResourceDictionary` merged in `App.xaml`.

---

## Refactor blast radius (Phase 1 acceptance gate)

Converting `IsMarkedForDeletion` to a set-backed view and removing the remover's auto-unmark guard touches existing behavior. **Every item below must keep working; "existing behavior unchanged" is an explicit acceptance criterion on the Phase 1/2 tasks.**

| Existing behavior | Where | Must still hold after refactor |
|---|---|---|
| Mark/unmark a duplicate from the results tree | `ToggleDeletionMarkCommand.cs`; XAML buttons MainWindow.xaml 75–90 | Toggling updates the row icon (Delete↔Undo), strikethrough text (`MarkedFileTextStyle`), sibling `IsMarkForDeletionVisible`, and the totals — identical UX. |
| Last-copy protection in the results tree | `IsMarkForDeletionVisible` (DuplicatesEngine.cs 29) | Delete button still hidden on the last unmarked copy; results tree alone still cannot fully mark a group. |
| Auto Select by Path | `AutoSelectByPathCommand.cs` | Still marks matching duplicates, keeps its own "leave at least one" guard (99–108), updates totals. |
| Reset selection | `ResetSelectionCommand.cs` | Clears the **entire** unified set now (dup + non-dup + folder); totals → 0; buttons disable. |
| Delete-enabled / Reset-enabled gating | `MainViewModel.OnDuplicatesPropertyChanged` 378–385; `DeleteMarkedFilesCommand` 44 | Enabled iff something is marked — now iff the set is non-empty (`ToBeDeletedCount != 0`), including non-dup-only selections. |
| Totals display | `ToBeDeletedCount/Size`; binding MainWindow.xaml 934 | Reflect set contents exactly; no double counting when the same path is marked from two views. |
| Deletion run (dup-only) | `DuplicatesRemover.cs`; `DuplicatesEngine.RemoveDuplicates` | Same result as today except the all-marked group is now actually deleted (guard removed); group/file collections still update; progress/recycle/fallback unchanged. |
| Post-deletion refresh | `MainViewModel` 306 | Still refreshes the search tree; now also refreshes the new control and prunes deleted paths from the set. |

---

## Phases & tasks

Leaf-task format — **[ID] Title** · *Description* · **Files** · **Depends** · **Acceptance (observable)**. Each task is sized to become one issue.

### Phase 1 — Engine selection model (foundational seam, no UI)
*Satisfies stories: 17, 18 (and underpins 12, 16, 22, 23, 27).*

**[P1-T1] Add a path-keyed deletion-selection set to the engine**
*Introduce an engine-owned selection service keyed by normalized full file path, storing each path's size, exposing add/remove/contains/clear and running count+size totals, and raising a change notification (per-path and/or batched) that views subscribe to.*
- Files: new `DuplicateFileTool/DeletionSelection.cs` (name TBD); `DuplicatesEngine.cs` (own an instance).
- Depends: —
- Acceptance: adding/removing a path updates `Count` and total size; adding an already-present path is a no-op (no double count); `Clear()` empties it; subscribers receive a change signal identifying affected path(s). Pure logic, no WPF references (verifiable in isolation).

**[P1-T2] Build a path → duplicate-group membership index with classification + zero-survivor query**
*From `DuplicatesEngine.DuplicateGroups`, maintain an index answering: is this path a duplicate (member of any group)? which group? would deleting the current selection leave zero surviving copies of this path (non-duplicate marked, or every copy in its group marked)? Keep the index correct as groups are added during search and mutated during deletion.*
- Files: `DuplicatesEngine.cs` (+ helper alongside P1-T1).
- Depends: P1-T1
- Acceptance: for a non-duplicate path, "zero-survivor" is true whenever it is in the set; for a duplicate, true only when all of its group's files are in the set; classification flips correctly as the last sibling is marked/unmarked.

**[P1-T3] Make `DuplicateFile.IsMarkedForDeletion` a view over the set**
*The getter returns set membership for the file's path; the setter adds/removes the path (with its size). The file subscribes to set changes for its path to raise `PropertyChanged` (itself + siblings' `IsMarkForDeletionVisible`), so a mark made from any view updates the results tree. Wire the set reference from the engine through `DuplicateGroup` to each `DuplicateFile` at construction.*
- Files: `DuplicatesEngine.cs` (`DuplicateFile`, `DuplicateGroup` ctor 145–152, group creation at 344).
- Depends: P1-T1
- Acceptance: marking a `DuplicateFile` adds its path to the set and vice-versa; `IsMarkForDeletionVisible` still hides the last-copy button; toggling from elsewhere (set) updates the row's icon/strikethrough live. **Existing results-tree marking UX unchanged.**

**[P1-T4] Route totals through the set; retire the per-command delta plumbing**
*Make `ToBeDeletedCount`/`ToBeDeletedSize` derive from the set (recompute or maintain in the set on change). Have `ToggleDeletionMarkCommand`, `AutoSelectByPathCommand`, and `ResetSelectionCommand` mutate the set rather than emit `UpdateToDelete*` events; collapse `MainViewModel.OnUpdateToDelete` accordingly.*
- Files: `Commands/ToggleDeletionMarkCommand.cs`, `Commands/AutoSelectByPathCommand.cs`, `Commands/ResetSelectionCommand.cs`, `MainViewModel.cs` (291, 294, 302, 467–471), `DuplicatesEngine.cs` (312–329).
- Depends: P1-T1, P1-T3
- Acceptance: totals match set contents exactly after any mark/unmark/auto-select/reset; the same path marked via two routes counts once; `ResetSelection.Enabled`/`DeleteMarkedFiles.Enabled` still gate on `ToBeDeletedCount != 0`.

**[P1-T5] Global Reset clears the whole set**
*`ResetSelectionCommand` clears the entire unified set (dup + non-dup + folder), not just a group walk.*
- Files: `Commands/ResetSelectionCommand.cs` (22–34).
- Depends: P1-T1, P1-T4
- Acceptance: after Reset, totals are 0, every results-tree mark is cleared, and (once the control exists) all folder-tree marks clear too. *(Satisfies story 23.)*

### Phase 2 — Deletion pipeline (depends on Phase 1; parallel to UI)
*Satisfies stories: 24, 25, 26, 30, 31, 34.*

**[P2-T1] Remove the all-marked-group auto-unmark guard**
*Delete the `unmarkedFilesCount < 1` branch and `UnmarkAll`, so a fully-marked group is actually deleted (the new control may legitimately produce one). Results-tree-only protection still lives in `IsMarkForDeletionVisible`.*
- Files: `DuplicatesRemover.cs` (96–101, 117–131).
- Depends: P1-T3
- Acceptance: deleting a group with every file marked removes all of them and drops the group; deleting with one unmarked copy behaves as today.

**[P2-T2] Drive deletion from the unified set (duplicates + non-duplicates)**
*Keep the group-walk for duplicate paths (so group/file collections update and emptied groups collapse), and add a second pass that deletes set paths not belonging to any group. Reuse the existing recycle/permanent/fallback/sticky logic for both. Remove each successfully deleted path from the set. Set `TotalFilesForDeletionCount` from the set count.*
- Files: `DuplicatesRemover.cs` (70–231, esp. 73, 133–231), `DuplicatesEngine.RemoveDuplicates` (526–534), `DeleteMarkedFilesCommand.cs` (40).
- Depends: P1-T1, P1-T3, P2-T1
- Acceptance: a run with mixed duplicate + non-duplicate marks deletes all of them; progress count covers all; recycle-failure prompt + "apply to all" still work; totals drop to reflect deletions. **Dup-only runs behave as before.**

**[P2-T3] Remove emptied folders (folder-selection deletion)**
*After deletion, directories the run emptied are removed (reusing `FileSystem.DeleteDirectoryTreeWithParents`), so a selected folder disappears. See Open Question OQ-1 on how this interacts with the existing `RemoveEmptyDirectories` setting and reparse safety.*
- Files: `DuplicatesRemover.cs` (223–229), `FileSystem.cs` (`IsDirectoryTreeEmpty`, `DeleteEmptySubDirectories`, `DeleteDirectoryTreeWithParents`).
- Depends: P2-T2
- Acceptance: marking and deleting a whole folder removes its files and the now-empty folder and emptied subfolders; a folder with some files left is not removed.

**[P2-T4] Delete-enabled gate + post-run cleanup for the unified set**
*`DeleteMarkedFilesCommand.Enabled` derives from the set / `ToBeDeletedCount`. After a run, prune deleted paths from the set and refresh the new control alongside the existing `RefreshExpandedFileTreeItems`.*
- Files: `DeleteMarkedFilesCommand.cs` (44), `MainViewModel.cs` (304–306).
- Depends: P1-T4, P2-T2, (P6 for control refresh)
- Acceptance: Delete is enabled when only non-duplicates are marked; after a run the control and totals reflect what remains. *(Satisfies story 34.)*

### Phase 3 — `FolderItem` view-model (depends on Phase 1)
*Satisfies stories: 8, 9, 10, 12, 13, 14, 15, 16, 19, 32.*

**[P3-T1] Create `FolderItem` node VM with lazy load, shell icons, sort, reparse skip**
*New VM mirroring `FileTreeItem`'s placeholder lazy-load and `FileSystemIcon` open/close icons and dirs-first `SortFileData`, but without the search tree's `ItemSelected`/`Refresh` behaviors. Expose Name, Size, LastModified, IsDirectory, Children, IsExpanded. Reparse-point entries (`FileData.IsReparsePoint`) are shown as leaves and never enumerated.*
- Files: new `DuplicateFileTool/FolderItem.cs`; reuse `FileSystemIcon.cs`, `DirectoryEnumeration.cs`, `FileData.cs`.
- Depends: — (UI-agnostic; can start with Phase 1)
- Acceptance: expanding a directory lists its real contents with correct icons, sizes, dates, dirs-first; a junction shows but does not expand; deep trees load on demand.

**[P3-T2] Set-backed file mark + binary folder mark (all-descendants)**
*A file `FolderItem`'s `IsMarkedForDeletion` is set membership for its path; a directory `FolderItem` reports marked iff all its descendant files are in the set. Both update live when the set changes for any contained path.*
- Files: `FolderItem.cs`; engine set (P1-T1).
- Depends: P1-T1, P3-T1
- Acceptance: marking a file from the results tree turns the same file marked in the folder tree; a folder shows marked only once every descendant is marked; unmarking one descendant clears the folder's marked state.

**[P3-T3] Eager background subtree enumeration on folder mark/unmark**
*Marking a directory walks its subtree (`DirectoryEnumeration`, background thread, skipping reparse points) and adds every file path (with size) to the set; unmarking removes them. Totals update live. Surfacing of the busy state is OQ-2.*
- Files: `FolderItem.cs`; engine set.
- Depends: P1-T1, P3-T1, P3-T2
- Acceptance: marking a collapsed folder marks all descendants and raises totals by the subtree's file count/size even though the tree wasn't expanded; unmark reverses exactly; UI stays responsive during a large scan.

**[P3-T4] Belonging-file and zero-survivor flags on `FolderItem`**
*Expose per-row flags the view binds to: "belongs to the current group" (this file is one of the current group's duplicates in this folder) and "zero-survivor" (queried from the engine, P1-T2).*
- Files: `FolderItem.cs`; engine index (P1-T2).
- Depends: P1-T2, P3-T1
- Acceptance: the current group's duplicate rows report belonging=true; a marked non-duplicate, and a marked duplicate whose whole group is marked, report zero-survivor=true; flags update when selection or current group changes.

### Phase 4 — Folder tree rendering (depends on Phase 3)
*Satisfies stories: 6, 8, 11, 27, 29.*

**[P4-T1] Render `FolderItem` in a `TreeListView` with mark/icon/name/size/date columns**
*Per-instance `TreeListView` whose `Columns` give Name/Size/Last-Modified plus a left mark-button column; hierarchical template binds `Children`; reuse the Delete/Undo mark button pattern and `MarkedFileTextStyle` strikethrough.*
- Files: `MainWindow.xaml` or new `ResourceDictionary`; `Controls/TreeListView.*` (reuse); a folder-tree mark command (or reuse the set via P3-T2 setter).
- Depends: P3-T1, P3-T2
- Acceptance: each row shows icon, name, size, last-modified, and a mark button matching the results tree; clicking marks/unmarks; marked rows show strikethrough.

**[P4-T2] Row background states: red zero-survivor, belonging highlight, precedence**
*Style rows: zero-survivor → red background; belonging-file → special highlight; red takes precedence on a shared row. Use bindings to P3-T4 flags.*
- Files: same resources as P4-T1.
- Depends: P3-T4, P4-T1
- Acceptance: a marked non-duplicate row is red; a current-group duplicate row is highlighted; when both apply, the row is red (highlight suppressed). *(Satisfies stories 6, 27, 29.)*

### Phase 5 — Folder sub-control (depends on Phase 4)
*Satisfies stories: 21, 22, 28, 38.*

**[P5-T1] Folder sub-control: header (path + clear button) + tree + warning line**
*A control composing a header (folder path, clipped with full-path tooltip; a clear button reusing `Clear.png`/`Reset.png`) over the P4 tree, with a warning text line under the tree shown when the tree contains any zero-survivor marked file.*
- Files: new `DuplicateFileTool/Controls/FolderComparisonItem.xaml(.cs)` (name TBD); `Images/Clear.png`.
- Depends: P4-T1, P4-T2, P3-T4
- Acceptance: header shows the folder path (tooltip reveals full path when clipped); the warning appears only while a zero-survivor file is marked in that tree.

**[P5-T2] Per-folder clear button wiring (recursive)**
*The clear button removes every set entry under that folder's path (recursive), which — being unified — also clears those rows in the results tree.*
- Files: sub-control code-behind/VM; engine set (P1-T1).
- Depends: P1-T1, P5-T1
- Acceptance: clicking clear empties that folder's marks in both the folder tree and the results tree; other folders untouched; totals drop accordingly. *(Satisfies stories 21, 22.)*

### Phase 6 — Container, layout & placement (depends on Phase 5)
*Satisfies stories: 1, 2, 4, 5, 35, 36, 37.*

**[P6-T1] `FolderComparison` container: distinct-folder sub-controls, side-by-side, splitters, horizontal scroll**
*Bind to the current group; produce one sub-control per distinct containing folder among the group's files; lay them out as side-by-side columns with draggable vertical `GridSplitter`s, a minimum column width, and a horizontal scrollbar when they overflow. Empty placeholder when no group is selected. Structured so a pager can be added later (OQ-3).*
- Files: new `DuplicateFileTool/Controls/FolderComparison.xaml(.cs)`.
- Depends: P5-T1, P3-T4
- Acceptance: selecting a group with N distinct folders shows N columns; same-folder duplicates collapse to one column highlighting all of them; many folders scroll horizontally; splitters resize; no selection → placeholder.

**[P6-T2] Place the control in the Results tab (temporary full-width bottom region)**
*Wrap the Results TabItem's content `Grid` (MainWindow.xaml ~506) in an enclosing grid with rows [existing content *] / [horizontal `GridSplitter`] / [`FolderComparison`], so it spans the full width below the groups tree and right panel.*
- Files: `MainWindow.xaml` (499–1000).
- Depends: P6-T1
- Acceptance: the control appears full-width at the bottom of the Results tab with a draggable horizontal splitter; existing results UI unaffected. *(Temporary per PRD; easy to remove.)*

### Phase 7 — Current-group binding & selected-row outer background (depends on Phases 1 & 6)
*Satisfies stories: 3, 7, 33.*

**[P7-T1] Expose a `CurrentGroup` driven by results-tree selection**
*Add a `CurrentGroup` the container binds to, updated when a results row is selected: a `DuplicateFile` → its group; a `DuplicateGroup` header → that group. Reuse the existing static `DuplicateFile.ItemSelected`; add an equivalent for group-header selection (or bind via `ResultsTreeView` selection). Requires exposing the file's group (P1-T2 index or making `ParentGroup` accessible).*
- Files: `MainViewModel.cs` (298, 460–465), `DuplicatesEngine.cs` (`DuplicateGroup`/`DuplicateFile`), `FolderComparison`.
- Depends: P1-T2, P6-T1
- Acceptance: selecting any file or a group header rebuilds the control for that group; selections in the set persist across group switches and results paging (set is engine-owned). *(Satisfies stories 3, 33.)*

**[P7-T2] Outer sub-control background when its file is the selected results row**
*Each sub-control's outer background lights up (distinct from row backgrounds) when its corresponding file is the currently selected results-tree row; it moves as selection changes.*
- Files: `FolderComparison`/`FolderComparisonItem`.
- Depends: P7-T1
- Acceptance: selecting a duplicate in the results tree highlights exactly its folder's sub-control outer background; changing selection moves the highlight. *(Satisfies story 7.)*

### Phase 8 — Busy indication (depends on Phase 3/6)
*Satisfies story: 20.*

**[P8-T1] Surface a busy state during eager subtree enumeration**
*Show a busy indication while P3-T3 scans a large subtree. Mechanism is OQ-2 (reuse `ProgressText`/taskbar vs a per-column overlay/disabled state).*
- Files: `FolderItem.cs`/`FolderComparison`/`MainViewModel.cs`.
- Depends: P3-T3, P6-T1
- Acceptance: marking a large folder shows a clear busy cue until the scan completes; the UI does not appear frozen.

### Phase 9 — Cross-cutting (do not drop)
*Not user-facing stories, but release-blocking.*

**[P9-T1] Localize every new string (en/es/ru)**
*Add resource entries for the folder-path header, the zero-survivor warning, mark/clear tooltips, and the empty placeholder, in all three cultures.*
- Files: the `Properties` resource files for `en`/`es`/`ru`.
- Depends: P5-T1, P6-T1
- Acceptance: switching culture shows translated strings for all new UI; no hard-coded literals.

**[P9-T2] Reuse existing icon assets**
*Mark button uses `Delete.png`/`Undo.png` (as results tree); per-folder clear uses `Clear.png` (or `Reset.png`). No new image files.*
- Files: `Images/` (existing), the new XAML.
- Depends: P4-T1, P5-T1
- Acceptance: buttons render with existing assets; no new PNGs added.

**[P9-T3] Version bump in all four tracked places**
*Bump csproj `<ApplicationVersion>`, `Properties/AssemblyInfo.cs` `AssemblyVersion`, installer `Package.wxs` `Version`, and the README shields.io badge, kept in sync.*
- Files: `DuplicateFileTool/DuplicateFileTool.csproj`, `DuplicateFileTool/Properties/AssemblyInfo.cs`, `DuplicateFileToolInstaller/Package.wxs`, `README.md`.
- Depends: feature complete
- Acceptance: the four versions match; the app's displayed version reflects the bump.

**[P9-T4] Changelog entry (WIP → Unreleased gating)**
*Add a `New.` bullet to `DuplicateFileTool/Changes.md` under **Work in Progress**; promote to **Unreleased** only after the change is confirmed working; into a dated release block on release.*
- Files: `DuplicateFileTool/Changes.md`.
- Depends: feature complete
- Acceptance: a `New.` entry describing the folder-comparison control exists in the correct section per the gating convention.

---

## Manual verification → phase mapping

The PRD's 10 scenarios and the phase that first makes each passable:

| # | Scenario | Becomes testable after |
|---|---|---|
| 1 | One sub-control per distinct folder, contents + duplicate highlight | P6-T1 (+ P4-T2 for highlight) |
| 2 | Selected-results-row drives the outer sub-control background | P7-T2 |
| 3 | Mark a non-duplicate → red row + warning + totals rise | P4-T2 + P5-T1 (relies on P1-T4) |
| 4 | Mark a collapsed folder → busy, all descendants marked, totals, binary state | P3-T3 + P8-T1 |
| 5 | Mark every copy of a group from the control; results tree shows full-marked; results tree still blocks last copy | P3-T2/P3-T4 + P2-T1 (relies on P1-T3) |
| 6 | Per-folder clear clears in both views | P5-T2 |
| 7 | Global Reset clears everything | P1-T5 |
| 8 | Selections persist across group switch / paging / refresh | P7-T1 |
| 9 | Mixed dup + non-dup + folder deletion incl. empty-folder removal, recycle fallback, post-run refresh | P2-T2 + P2-T3 + P2-T4 |
| 10 | Junction shown but not entered; marking parent doesn't reach behind it | P3-T1 + P3-T3 (+ OQ-1 for deletion side) |

---

## Out of scope (do not pull back in)

- No pager for many folder columns — horizontal scroll only (build so a pager can be added later).
- No open-with-default-app / reveal-in-Explorer in the folder tree rows.
- No F5 / window-activation auto-refresh of the new folder trees (they refresh after a deletion run only; the search-page tree keeps its own behavior).
- No final Results-page redesign — the bottom region is temporary.
- No lazy "store the directory, resolve at delete" model — eager enumeration only.
- No unit/integration test project.
- Windows-only; no cross-platform behavior.

---

## Open questions / concerns

**OQ-1 — Empty-folder removal vs the `RemoveEmptyDirectories` setting, given folders aren't stored.** The model stores only file paths, so at delete time the remover cannot distinguish "user explicitly selected this folder" from "user marked all files individually." PRD Q6 wants an explicitly-selected folder removed *always*. Today empty-dir removal is gated on the `RemoveEmptyDirectories` config (DuplicatesRemover.cs 224). Two resolutions: **(a)** remove any directory our run empties, regardless of the setting (simplest; but changes the dup-only flow — an emptied dir would be removed even with the setting off); **(b)** track explicitly-selected directory paths in a small parallel set purely to force their removal, leaving the setting to govern the dup-only flow (keeps existing behavior; slightly extends the model). **Recommend (b).** Needs a decision before P2-T3.

**OQ-2 — How "busy indication" surfaces.** There is no generic busy/spinner primitive; the app uses `ProgressText`/`ProgressPercentage`/`TaskbarProgress` and `Ui` switches. Options: reuse the existing progress text/taskbar (consistent, but it's shared with search/delete), a per-column overlay/disabled state on the sub-control being scanned, or a mouse-wait cursor. **Recommend** a lightweight per-column busy/disabled overlay plus optional progress text. Decide before P8-T1.

**OQ-3 — Splitter behavior & minimum column width.** Exact min column width, default column sizing (equal vs content), and whether folder-tree column widths (Name/Size/Modified) are persisted like the results tree's (`ResultsConfiguration` + MainWindow.xaml.cs load/save) are unspecified. **Recommend** a fixed sensible min width, no persistence this iteration. Decide before P6-T1.

**OQ-4 — Reparse safety in empty-dir detection/removal.** `FileSystem.IsDirectoryTreeEmpty` and `DeleteEmptySubDirectories` recurse with `DirectoryEnumeration` and currently have no reparse-point guard, so folder removal could traverse a junction even though the display/marking tree won't. To honor story 32 on the deletion side, these may need a reparse guard. Confirm and, if needed, add to P2-T3 scope.

**OQ-5 — Concurrent eager scans & cancellation.** Marking several large folders quickly, or switching groups mid-scan, raises questions about cancelling in-flight enumerations and avoiding partial set updates. Not addressed by the PRD. **Recommend** cancel a folder's in-flight scan if it is unmarked or the group changes, applying set updates atomically per folder. Decide before P3-T3.

**OQ-6 — "Reflect after deletion" with the temporary placement.** The control rebuilds from `CurrentGroup`; after a run the current group may shrink or vanish. Confirm the intended post-run state (keep showing the (now-smaller) group vs clear to placeholder). **Recommend** rebuild from the still-selected results row, or placeholder if it was deleted. Relevant to P2-T4/P7-T1.
