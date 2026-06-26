# PRD: Folder-selection rail ("paths expander") for the folder-comparison control

Status: ready-for-agent

> Follow-up to the folder-comparison feature (`../folder-comparison/PRD.md`). It modifies the existing `FolderComparison` container and rides the seams that feature already built (notably the distinct-folder derivation and the column-list slice the container was factored to accept).

## Problem Statement

When the user selects a duplicate group, the folder-comparison control builds **one folder column per distinct containing folder** of the group's files — and it builds them **all at once**. A duplicate group can hold hundreds of duplicate files spread across hundreds of distinct folders, so the control tries to render hundreds of heavy folder columns simultaneously. Each column eagerly enumerates its folder on the UI thread and kicks off background subtree size-scans, so a large group stalls the UI and hammers the disk, and the user is faced with an unusable wall of columns instead of a focused side-by-side comparison.

The user has no way to say "I only want to compare these folders." They cannot bound the cost, cannot pick which folders to look at, and cannot bring two specific folders next to each other without scrolling past everything in between. The control that exists to make folder comparison easy becomes the slowest, least usable part of a large group.

## Solution

Add a collapsible **folder-selection rail** ("paths expander") on the left edge of the folder-comparison control.

- By default the control renders only the **first 5 distinct folders** of the current group as columns, in the **group's own order** (the order the duplicates are listed in the results tree). This bounds the automatic cost so selecting a large group no longer freezes the app.
- The rail collapses to a slim toggle strip. A toggle button at its **top** expands it horizontally (to the right), pushing the columns over. Expanded, it lists **every distinct folder** of the current group, each with a **checkbox**; long paths are clipped with the full path on hover.
- **Checked folders are the rendered columns.** Checking a folder adds its column at its position in the group order; unchecking removes that column — **incrementally**, without disturbing the other columns (their tree expansion, scroll, and width are preserved).
- The user decides how many folders to compare. There is **no practical cap** for normal use — only a large safety ceiling so a stray click cannot try to render hundreds of columns. The premise is explicit: if the user selects more folders, the user is willing to accept the performance cost.

To compare two specific folders the user simply checks only those two; they appear side by side with nothing in between.

## User Stories

1. As a de-duplication user, I want the folder-comparison control to render only a handful of folder columns by default, so that selecting a group whose copies span many folders no longer freezes the app or floods me with columns.
2. As a de-duplication user, I want the first 5 distinct folders of the current group shown by default, so that I immediately get a useful comparison without configuring anything.
3. As a de-duplication user, I want the default folders and the column order to follow the group's own file order, so that the columns match the order the duplicates are listed in the results tree.
4. As a de-duplication user, I want a folder-selection rail on the left of the control, so that I can choose which folders are compared.
5. As a de-duplication user, I want the rail collapsed by default to a slim toggle strip, so that it does not steal comparison space until I need it.
6. As a de-duplication user, I want the expander toggle button at the top of the rail, so that opening and closing the folder list is obvious and easy.
7. As a de-duplication user, I want the rail to expand horizontally and push the columns over (not float over them), so that the list and the columns are both fully usable.
8. As a de-duplication user, I want the expanded rail to be a fixed, readable width, so that the layout stays stable and predictable.
9. As a de-duplication user, I want long folder paths in the rail clipped with the full path on hover, so that the list stays tidy but every path is reachable.
10. As a de-duplication user, when the rail is expanded I want to see every distinct folder of the current group, each with a checkbox, so that I can pick any of them to compare.
11. As a de-duplication user, I want the folders listed in the group's own order, so that the rail order, the column order, and the results-tree order all agree.
12. As a de-duplication user, I want the first 5 folders pre-checked, so that the default comparison is ready with no action.
13. As a de-duplication user, I want checking a folder to add its column, so that I can bring more folders into the comparison.
14. As a de-duplication user, I want unchecking a folder to remove its column, so that I can drop folders I do not care about.
15. As a de-duplication user, I want a newly checked folder's column inserted at its correct position in the group order, so that the columns stay consistently ordered no matter how I toggle them.
16. As a de-duplication user, I want toggling one folder to leave the other columns untouched — their tree expansion, scroll position, and dragged width preserved — so that I do not lose my place when adding or removing a folder.
17. As a de-duplication user, I want to compare two specific folders by checking only those two, so that they appear side by side with nothing else in the way.
18. As a performance-conscious user, I want no hard cap on how many folders I can select for normal use, so that I can compare as many folders as I am willing to wait for.
19. As a performance-conscious user, I want a large safety ceiling on the number of selected folders, so that a stray click cannot try to render hundreds of columns and freeze the app.
20. As a performance-conscious user, when the ceiling is reached I want further checkboxes disabled with a short explanation, so that I understand why I cannot add more.
21. As a de-duplication user, I want checking or unchecking a folder to only show or hide its column — never to mark files for deletion — so that choosing what to compare is separate from choosing what to delete.
22. As a de-duplication user, I want my existing per-row and per-folder deletion marks unaffected by the rail, so that selection-for-comparison and selection-for-deletion stay independent and nothing is double-counted.
23. As a de-duplication user, when I switch to a different group, I want the rail and the columns reset to that group's default 5, so that each group starts from a clean, useful default.
24. As a de-duplication user, after a deletion run refreshes the control, I want the rail rebuilt for what remains and reset to the default 5, so that the rail reflects the current on-disk reality.
25. As a de-duplication user, when no group is selected, I want the existing placeholder shown and no rail, so that the empty state stays clear.
26. As a de-duplication user, if a group has fewer than 5 distinct folders, I want all of them shown, so that the default still shows everything worth comparing.
27. As a de-duplication user, if I uncheck every folder, I want a clear "no folders selected" message in the columns area, so that I know the empty state is my own doing and how to fix it.
28. As a de-duplication user, I want clicking a duplicate row in the results tree to still highlight its folder's column when that folder is shown, so that the existing selected-column highlight keeps working.
29. As a de-duplication user, I want the rail to be the sole control of which folders are shown, so that browsing rows in the results tree does not unexpectedly add or remove columns.
30. As a de-duplication user, I want the existing per-folder clear button, zero-survivor warnings, belonging-row highlights, and busy overlay to keep working in every shown column, so that the rail does not regress the folder-comparison feature.
31. As a de-duplication user, I want the shown columns to remain resizable with the splitters between them, so that I can still widen the folder I am focused on.
32. As a de-duplication user, I want horizontal scrolling to still appear when my selected columns overflow the viewport, so that every selected folder remains reachable.
33. As a de-duplication user, I want same-folder duplicates to collapse to a single rail entry and a single column, so that one folder never produces redundant duplicate columns.
34. As a de-duplication user, I want the rail's checkbox state to always reflect exactly which folders are currently rendered, so that the list and the columns can never disagree.
35. As a de-duplication user, I want the rail to start collapsed every time, so that the behavior is consistent and I always begin from the compact view.
36. As a localization user, I want the rail title, the toggle tooltip, the ceiling-reached tooltip, and the empty-state message available in English, Spanish, and Russian, so that the feature is localized like the rest of the app.

## Implementation Decisions

### Ownership & placement
- The folder-selection rail is built **into the existing folder-comparison container control**, not as a separate sibling. The container already derives the current group's distinct folders and owns column construction, so the rail's data source and its selection→columns wiring stay internal with no cross-control coordination. The rail shares the container's **temporary placement** and is removed together with it when the Results page is redesigned.

### Layout & expander
- The container becomes a **two-region layout**: a left, horizontally-collapsible rail and the existing horizontally-scrolling folder-columns region.
- The rail expands by **pushing / reflowing** the columns region (dock), never overlaying it. Collapsed, it is a slim toggle strip; expanded, it is a **fixed sensible width** (proposed ~250px) with long paths clipped (full path on hover).
- The rail uses a standard **horizontally-expanding expander** (expand direction to the right) with its **toggle button presented at the top**, reusing the settings expander's visual style where practical.
- The rail's expand/collapse state is **not persisted**: it starts collapsed on every group and every run.

### Distinct-folder ordering (highest seam)
- Change the distinct-folder derivation from "order by normalized path" to the **group's own order**: iterate the group's duplicate files (already ordered by full path at group construction) and take the distinct containing folders in **first-appearance order**, case-insensitive, with same-folder duplicates collapsing to one. This makes the rail order, the column order, and the results-tree order agree.
- Factor this into a **single pure, WPF-independent helper** that yields the ordered distinct-folder list, used by **both** the rail list and the column builder. This is the highest test seam.

### Default selection
- On (re)build, the **first 5 folders** in that order are selected (rendered as columns). If the group has fewer than 5 distinct folders, **all** are selected.

### Selection model
- The rail binds to a lightweight, observable collection of folder entries (path + is-selected), held in the container's code-behind (consistent with the control's existing imperative style — it has no dedicated view-model). Toggling an entry's selection drives column add/remove.
- The collection is **rebuilt on each group change and after a post-deletion refresh**, resetting to the default 5. Selection is per-group transient; it does not persist across group switches or runs.

### Incremental column maintenance
- Replace the "build all columns at once" behavior with **incremental insert and remove**:
  - Checking a folder inserts **exactly its column** (and the splitter needed between columns) at the folder's position in the group order among currently-shown columns.
  - Unchecking a folder removes **exactly that column and its adjacent splitter**, cancels any in-flight scan on its root, and drops it from the tracked roots.
  - Other columns are **not torn down**, so their tree expansion, scroll position, and dragged widths are preserved.
- The container tracks a mapping from folder to its created column so insertion and removal target the correct one, and so checkbox state and rendered columns can never disagree.

### Safety ceiling
- A large constant ceiling (proposed **50**) on the number of simultaneously selected folders. At the ceiling, **unchecked** checkboxes are disabled and show a short "limit reached" tooltip; **checked** ones can still be unchecked. There is **no "select all"** affordance.

### Comparison vs deletion independence
- Rail selection only controls which folder columns are rendered. It **never reads or writes the deletion-selection set**. The per-row mark button, per-folder clear, and global reset are unchanged, and rail toggles never change deletion counts or totals.

### Results-tree coupling (unchanged, not extended)
- Selecting a results-tree row continues to drive the current comparison group and the selected-column highlight on **already-rendered** columns. It does **not** auto-add a folder to the rail. If the selected file's folder is not currently rendered, no column is shown for it and nothing is highlighted — acceptable, because the rail is the sole membership control.

### Empty & no-group states
- **Zero folders selected:** the columns region shows a distinct centered **"no folders selected"** message (new localized string), separate from the existing "no group selected" placeholder.
- **No current comparison group:** the control shows the existing placeholder and **no rail**.

### Localization
- New strings — rail title/header, toggle tooltip, ceiling-reached tooltip, empty-state message — added for **en / es / ru**, following the existing folder-comparison string family.

### Performance posture (deliberately bounded scope)
- The per-column behavior is **unchanged**: folder columns still eagerly load on the UI thread and still start background subtree size-scans. The default-of-5 bounds the **automatic** cost; selecting more is an explicit user choice ("the user owns the cost").
- The deeper per-column performance work — asynchronous / off-UI-thread folder load, a cancellation token for the size-scan, and de-duplicating / concurrency-capping / drive-aware size-scan walks, plus async/cached shell icons — is **explicitly deferred** to a separate work item (see Out of Scope).

## Testing Decisions

- **No test project exists in the solution and none is added** (maintainer decision); verification is **manual**, consistent with the parent folder-comparison PRD.
- A good test exercises **observable, user-facing behavior** — which folders render as columns, in what order, what the rail shows, and what happens on toggle / group-switch / post-deletion / empty — not internal wiring.
- **Highest seam kept verifiable:** the **ordered-distinct-folder derivation** (group → ordered distinct folder list in first-appearance order) is a pure, WPF-independent helper, so it could be unit-tested later without the UI and is reused by both the rail and the column builder. The rail's selection→render mapping is the next seam but is UI-bound.
- **Prior art:** there are no automated tests; the parent folder-comparison PRD's manual verification scenarios are the model.
- **Manual verification scenarios:**
  1. Select a group spanning more than 5 distinct folders → exactly the first 5 (group order) render as columns; rail collapsed.
  2. Expand the rail → every distinct folder is listed in group order, the first 5 checked, matching both the rendered columns and the results-tree order.
  3. Check a 6th folder → its column inserts at the correct group-order position; the existing 5 columns keep their expansion, scroll, and width.
  4. Uncheck a middle folder → only that column (and a splitter) is removed; the others are undisturbed.
  5. Check only two specific folders (uncheck the rest) → just those two render, side by side.
  6. Reach the safety ceiling → further checkboxes are disabled with a tooltip; already-checked folders can still be unchecked.
  7. Toggle folders rapidly → no leaked columns or handlers; rail checkbox state and rendered columns stay consistent.
  8. Switch to another group → rail list and columns reset to the new group's default 5.
  9. Run a deletion that empties or changes folders → after the refresh, the rail is rebuilt for what remains and reset to default 5.
  10. Uncheck every folder → the "no folders selected" message appears; re-checking restores columns.
  11. Confirm rail toggles never change deletion marks or totals; per-folder clear, zero-survivor warning, belonging highlight, and busy overlay still work in shown columns.
  12. Select a results-tree row whose folder is shown → its column highlights; select one whose folder is not shown → nothing is added and nothing is highlighted.
  13. Long paths are clipped with a full-path tooltip; en / es / ru strings are present for all new UI text.

## Out of Scope

- **Reordering** folders or columns — order is fixed to the group's own order.
- **Persisting** the rail's expand state or the folder selection across group switches or app runs (rail resets to collapsed; selection resets to default 5).
- **Per-folder column-width persistence.**
- **The per-column performance fixes** (asynchronous / off-UI-thread folder load, size-scan cancellation token, size-scan de-dup / concurrency cap / drive-awareness, async/cached icons) — deferred to a separate work item.
- **True UI virtualization** of the columns, or a master/detail redesign.
- A **configurable** selection cap or page-size setting.
- A **"select all"** affordance.
- **Auto-adding** the selected results-tree row's folder to the rail.
- The eventual **Results page redesign** — the current bottom-region placement remains temporary.
- **Non-Windows** behavior (the app is Windows-only).
- Any **unit / integration test project.**

## Further Notes

- **Versioning & changelog:** this ships as part of the still-unreleased folder-comparison feature; fold a `New.` line into that feature's **Work-in-Progress** changelog entry rather than introducing a separate version bump. The pending **2.4.0** bump (folder-comparison issue 025) remains the release vehicle. Follow the changelog Work-in-Progress → Unreleased gating convention (promote only after the change is confirmed working at runtime).
- **Tunable constants:** the default count (**5**) is the user's spec; the safety ceiling (**50**) and the expanded rail width (**~250px**) are proposed defaults to confirm during implementation.
- **Throwaway-ish UI:** the rail is co-located with the temporary folder-comparison placement and should be removed together with that region at the Results redesign.
- **Glossary alignment:** *distinct folder* = a distinct containing directory among a group's duplicate files (case-insensitive; same-folder duplicates collapse); *folder column* = one folder sub-control in the comparison; *current comparison group* = the group reflected by the results-tree selection.
- **Dependency note:** builds directly on the folder-comparison container (issue 018), its current-group binding (020), the selected-column highlight (021), and the busy overlay (022); it reuses the container's distinct-folder derivation and the column-list slice the container was deliberately factored to accept.
