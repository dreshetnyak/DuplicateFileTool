# PRD: Folder-comparison control for duplicate groups

Status: ready-for-agent

## Problem Statement

When the user finds a duplicate group, they often want to clean up an entire folder, not just the duplicated files. Two folders may each hold copies of the same files, but one folder is the "complete" set and the other is a partial leftover that also contains unrelated junk. Today the only deletion-selection tools are duplicate-centric: the user marks individual duplicate files, or uses **Auto Select by Path** to mark the duplicates that live under a chosen path. Neither tells the user whether a folder is complete, and neither lets the user act on the *non-duplicate* files sitting in those folders.

As a result, to decide which folder to delete, the user has to open each folder in Explorer, eyeball the contents, and compare them by hand. That is slow, error-prone, and breaks the de-duplication flow. There is also no way to select a non-duplicate file (or a whole folder) for deletion from inside the tool — the deletion pipeline only knows about duplicate-group members.

## Solution

Add a **folder-comparison control** to the Results tab. When the user selects a duplicate group (or any file row inside it) in the results tree, the control shows one sub-control per **distinct folder** that contains files from that group, laid out **side by side** so the folders can be compared at a glance.

Each sub-control has:
- a header showing the folder path, with a button to clear that folder's deletion selection;
- a full, expandable file tree of the folder's contents (files and sub-directories), with a file-type icon, name, size, last-modified date, and a mark-for-deletion button per row;
- a background that highlights the row(s) belonging to the current group's duplicates, and an outer background that lights up when the corresponding file is the selected row in the results tree.

The user can mark any file — duplicate or not — and any folder (which marks everything inside it, recursively). All selections, duplicate and non-duplicate, flow into a single deletion set keyed by file path, so the existing results tree and the new control always agree, the deletion counts/sizes stay correct, and the deletion run removes everything that was marked (including emptied folders). Because deleting a non-duplicate file — or every copy of a group — destroys the only surviving copy, those rows are flagged with a red background and a warning under the tree.

## User Stories

1. As a de-duplication user, I want to see the full contents of each folder that holds files from the current duplicate group, so that I can judge whether a folder is a complete set or a partial leftover.
2. As a de-duplication user, I want the folders shown side by side, so that I can compare them directly without opening Explorer.
3. As a de-duplication user, I want the control to update to the group I just selected in the results tree, so that I don't need a separate "open this group" step.
4. As a de-duplication user, I want one sub-control per distinct folder (not one per file), so that two duplicates in the same folder don't produce two identical, redundant trees.
5. As a de-duplication user, when several of the group's duplicates live in one folder, I want all of them highlighted in that folder's tree, so that I can see every duplicate that folder contributes.
6. As a de-duplication user, I want the file(s) that the current group owns in a folder visually marked with a special background, so that I can immediately spot the duplicate among the folder's other files.
7. As a de-duplication user, I want the sub-control whose file is currently selected in the results tree to have a distinct outer background, so that I can tell which folder column corresponds to my results-tree selection.
8. As a de-duplication user, I want a file-type icon, name, size, and last-modified date for every entry in the folder tree, so that I can compare files the way I would in Explorer.
9. As a de-duplication user, I want the folder tree to use the same associated file-type icons as the search-page tree, so that the icons look familiar and meaningful.
10. As a de-duplication user, I want to expand sub-directories inside the folder tree, so that I can inspect the full content tree of the folder.
11. As a de-duplication user, I want a mark-for-deletion button on the left of each row, just like the results tree, so that the interaction feels consistent.
12. As a de-duplication user, I want to mark an individual file (duplicate or not) for deletion from the folder tree, so that I can remove files the duplicate finder doesn't know about.
13. As a de-duplication user, I want to mark an entire folder for deletion, so that every file inside it — including its sub-directories — is marked in one action.
14. As a de-duplication user, I want a folder to show as "marked" only when all of its descendants are marked, so that the folder's state honestly reflects its contents.
15. As a de-duplication user, I want a sub-directory to show as marked exactly when all the files inside it are marked, so that the tree's mark states stay internally consistent.
16. As a de-duplication user, I want to deselect a file or folder the same way I selected it, so that I can correct mistakes.
17. As a de-duplication user, I want my folder-tree marks and the results-tree marks to be the same selection, so that marking a file in one place is reflected in the other and nothing is double-counted.
18. As a de-duplication user, I want the deletion count and freed-size totals to include my non-duplicate and folder selections, so that the numbers reflect what will actually be deleted.
19. As a de-duplication user, I want marking a folder whose contents aren't loaded yet to still account for every file inside it, so that the totals and the eventual deletion are complete.
20. As a de-duplication user, I want a busy indication while a large folder's subtree is being scanned after I mark it, so that I know the app is working and not frozen.
21. As a de-duplication user, I want a per-folder clear button in the header, so that I can wipe that folder's deletion selection without touching the others.
22. As a de-duplication user, I want the per-folder clear to remove every mark under that folder (including duplicates), so that "clear" leaves the folder with nothing selected.
23. As a de-duplication user, I want the global Reset selection button to clear the entire deletion selection (duplicate, non-duplicate, and folder), so that one button truly resets my work.
24. As a de-duplication user, I want to run the deletion and have my non-duplicate and folder selections deleted alongside the duplicate selections, so that one run cleans everything I marked.
25. As a de-duplication user, when I delete a folder I selected, I want the emptied folder and its emptied sub-folders removed too, so that no empty shells are left behind.
26. As a de-duplication user, I want files to still go to the Recycle Bin (or be permanently deleted) per my existing setting, with the same fallback prompt when recycling fails, so that deletion behaves the way it already does.
27. As a cautious user, I want any marked file that would leave zero surviving copies — a non-duplicate, or a duplicate whose whole group is marked — to be shown with a red background, so that I can see at a glance which deletions are unrecoverable.
28. As a cautious user, I want a warning message under a folder's tree when that folder contains such an unrecoverable deletion, so that I'm told in words, not just color.
29. As a cautious user, I want the red warning to take precedence over the belonging-file highlight on the same row, so that the danger signal is never hidden.
30. As a cautious user, I want the results tree itself to keep preventing me from marking the last copy of a group, so that I can't accidentally wipe a group from the familiar view.
31. As a power user, I want the new control to let me override that protection and mark all copies of a group when I really mean to, so that I can delete a whole junk folder even if it holds the only-other copies.
32. As a de-duplication user, I want directory junctions/symlinks shown but not traversed, so that marking a folder can't reach through a junction and delete files elsewhere, and the tree can't loop.
33. As a de-duplication user, I want my selections to survive switching between groups, paging the results, and refreshing a tree, so that I don't lose work as I navigate.
34. As a de-duplication user, I want the control and its totals to update after a deletion run, so that deleted files disappear and the selection no longer references them.
35. As a de-duplication user, with many copies of a group across many folders, I want a horizontal scrollbar so that every folder column remains reachable instead of being squeezed to nothing.
36. As a de-duplication user, I want to resize the folder columns with the splitters between them, so that I can widen the folder I'm focused on.
37. As a de-duplication user, when no group is selected, I want the control to show an empty/placeholder state, so that it isn't confusing or stale.
38. As a de-duplication user, I want long folder paths in the header clipped with the full path available on hover, so that the header stays tidy but the full path is reachable.

## Implementation Decisions

### Selection model — unified, path-keyed (single source of truth)
- Introduce a single deletion-selection set, owned by `DuplicatesEngine`, keyed by **normalized full file path**. This set holds *file* paths only; folders are never stored — a folder's marked state is derived as "all descendant files are in the set."
- `DuplicateFile.IsMarkedForDeletion` becomes a view over this set (true iff the file's path is in the set). Marking from the results tree, from Auto Select by Path, or from the new folder tree all read/write the same set, so the two views can never disagree and no file is counted twice.
- The engine also needs a **path → duplicate-group membership** index so any file can be classified as duplicate (member of some group) or non-duplicate, and so "is every copy of this file's group marked" can be answered.

### Current group binding
- The control reflects the group of the currently selected results-tree row (a group header or any file row inside it). Selection changes rebuild the control. No new "open group" affordance. When nothing is selected, the control shows a placeholder.

### Sub-control mapping — one per distinct folder
- The control renders one sub-control per **distinct containing folder** among the current group's files (derived from each `DuplicateFile`'s `FileData` directory). A folder that holds several of the group's duplicates highlights all of them and exposes a single header/clear button. (Note: this intentionally diverges from the original spec wording of "one item per group file.")

### Layout
- Sub-controls are laid out as a horizontal row of columns separated by draggable vertical splitters; each column hosts its own scrolling folder tree. Many folders produce a horizontal scrollbar at a sensible minimum column width. The control is built so a pager can be added later without rework.

### Folder tree control & node view-model
- Render the folder tree with the existing multi-column `TreeListView` (so it gets Name/Size/Last-Modified columns plus a mark-button column, consistent with the results tree).
- Add a **new dedicated node view-model** (e.g. `FolderItem`) that borrows the lazy-load + shell-icon behavior of the search-page `FileTreeItem` (directory enumeration via the Win32 `DirectoryEnumeration`, icons via `FileSystemIcon`/`SHGetFileInfo` with open/closed folder states) and the mark semantics of `DuplicateFile`, wired to the unified selection set. It is independent of the search tree's selection-event and refresh-on-activate behaviors to avoid cross-impact on the Search page.
- Rows show: mark button, file-type icon, name, size, last-modified. No open/Explorer affordances this iteration.
- Sub-directories are lazily expanded (placeholder pattern). Reparse points (junctions/symlinks) are displayed but never traversed for display, marking, or deletion.

### Folder selection semantics
- Marking a folder recursively marks every file inside it and its sub-directories. Because the model stores file paths, marking a folder whose children aren't loaded yet performs an **eager background subtree enumeration** (Win32 directory enumeration on a background thread), adding each file path to the set and updating the totals live; a busy indication is shown for large trees.
- A folder/sub-directory is shown marked (binary state) exactly when all of its descendant files are in the set. Clicking a marked folder unmarks all of its descendants.

### Deletion pipeline changes (`DuplicatesRemover` / `DeleteMarkedFilesCommand` / `DuplicatesEngine`)
- The remover deletes **every path in the unified selection set**, not just files reached by walking `DuplicateGroups`. After a folder's files are deleted, the folder and any now-empty sub-folders are removed via the existing empty-tree removal (empty directories are removed permanently; files still follow the recycle-bin / permanent rules and the existing recycle-failure fallback prompt with its sticky "apply to all" decision).
- The remover's current guard that warns and auto-unmarks a group when all its files are marked is **removed**, because the new control may legitimately produce a fully-marked group.
- Deletion counts/sizes (`ToBeDeletedCount`, `ToBeDeletedSize`) are derived from set membership deltas so non-duplicate and folder selections are included with no double counting.

### Last-copy protection (relaxed, asymmetric)
- The results tree keeps today's protection: its mark button stays hidden for the last unmarked copy of a group — you cannot fully mark a group from the results tree.
- The new control overrides this: it may mark all copies of a group and any non-duplicate file. Such marks propagate to the unified set, so the results tree will then *show* a group fully marked even though it could not have created that state itself.

### Zero-survivor warning
- Rule: a marked file is "zero-survivor" when deleting the current selection would leave no copy of its content on disk — i.e. it is a marked non-duplicate, or it is a duplicate whose every group copy is marked.
- Zero-survivor rows get a red background and a warning message under that folder's tree. The red background takes precedence over the belonging-file highlight on the same row. The "selected in results tree" treatment is a separate *outer* sub-control background and never conflicts with the row backgrounds.

### Clear / reset
- Each sub-control's header clear button removes every mark under that folder's path (recursive), which — being unified — also clears those files in the results tree.
- The existing global Reset selection button clears the entire unified set (duplicate, non-duplicate, and folder marks).

### Placement (temporary)
- The control occupies a new full-width region at the bottom of the Results tab, below the existing groups tree and right-hand panel, separated by a draggable horizontal splitter. This is explicitly temporary; the Results page is expected to be redesigned later.

## Testing Decisions

There are no test projects in this solution, and none will be added for this iteration (per maintainer decision). Verification is **manual**. A good test here exercises observable, user-facing behavior (what gets marked, what the totals show, what is deleted on disk) rather than internal wiring.

The highest-value seam — to keep in mind for any future automated tests and for structuring the code so it stays verifiable — is the **engine's selection/deletion logic**: the path-keyed selection set (duplicate vs non-duplicate classification, zero-survivor computation, count/size deltas), the folder-subtree enumeration→mark, and the deletion path collection plus empty-directory removal. This logic should be kept separable from WPF so it could later be tested without the UI. There is no prior art (the repo has no tests).

Manual verification scenarios to run before release:
1. Select a group; confirm one sub-control per distinct folder, each showing the folder's full contents with icon/name/size/date, and the group's duplicate(s) highlighted.
2. Confirm the sub-control for the file selected in the results tree gets the distinct outer background; change selection and confirm it moves.
3. Mark a single non-duplicate file from the folder tree; confirm red row + warning under the tree, and that the count/size totals increase.
4. Mark a whole folder (including one with collapsed sub-directories); confirm busy indication, that all descendants become marked, totals update, and the folder shows the binary marked state.
5. Mark every copy of a group from the control; confirm the results tree now shows the group fully marked and the rows turn red with a warning; confirm the results tree still won't let you mark the last copy on its own.
6. Use a per-folder clear button; confirm all marks under that folder clear in both the control and the results tree.
7. Use global Reset; confirm the entire selection (duplicate + non-duplicate + folder) clears.
8. Switch groups, page the results, and refresh a tree; confirm selections persist.
9. Run a deletion with mixed duplicate, non-duplicate, and folder selections; confirm files go to the Recycle Bin (or permanent) per setting, the selected folders and emptied sub-folders are removed, the recycle-failure fallback still works, and the control/totals update afterward.
10. Confirm a directory junction is shown but not entered, and marking its parent folder does not mark files behind the junction.

## Out of Scope

- A pager or other mechanism for very large numbers of folder columns (only horizontal scroll for now; build so a pager can be added later).
- Open-with-default-app and reveal-in-Explorer affordances in the folder tree rows.
- F5 / window-activation auto-refresh of the new folder trees (the search-page tree keeps its own refresh behavior; the new trees refresh after a deletion run only).
- The eventual Results page redesign; current placement is a temporary bottom region.
- A lazy "store the directory, resolve at delete" selection model (rejected in favor of eager enumeration).
- Any unit/integration test project.
- Non-Windows behavior (the app is Windows-only).

## Further Notes

- **Versioning & changelog:** ships as a `New.` entry; bump the version in all four tracked places (csproj `ApplicationVersion`, `AssemblyInfo.cs`, installer `Package.wxs`, README badge) and follow the changelog Work-in-Progress → Unreleased gating convention.
- **Localization:** all new UI strings (folder-path header, zero-survivor warning, mark/clear tooltips, placeholder text) need `en` / `es` / `ru` resource entries.
- **Icon reuse:** reuse existing assets — `Delete.png`/`Undo.png` for the row mark button (as in the results tree) and `Clear.png` (or `Reset.png`) for the per-folder header clear button.
- **Display defaults:** folder trees sort directories first then files (as the search tree does); all entries are shown including hidden/system and zero-size files (the zero-size exclusion applies only to duplicate detection, not to this folder view).
- **Spec divergence:** the original feature text said "one sub-control per group item" and described the layout as both "vertical" and "side by side"; the agreed design is one sub-control per *distinct folder* and a *side-by-side columns with vertical splitters* layout.
