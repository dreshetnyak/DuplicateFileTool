# 010 — Pure ordered-distinct-folder helper (highest seam)

- **Status:** awaiting-verification
- **Plan step:** Step 1

## Goal
Replace the container's "order distinct folders by normalized path" derivation with the **group's own order** (first-appearance, case-insensitive, same-folder collapse), factored into a single pure, WPF-independent helper reused by both the rail and the column builder.

## Satisfies
- User stories: **US-3, US-11, US-33**
- PRD sections: §"Distinct-folder ordering (highest seam)"; Testing §"Highest seam kept verifiable"

## Exact files / classes / methods (copied from plan)
- **Add:** `internal static class GroupFolders` (core namespace, e.g. `GroupFolders.cs` next to `DeletionSelection.cs`) with one pure method:
  `static IReadOnlyList<string> OrderedDistinct(IEnumerable<string> directoryPaths)` — iterates in order, dedupes by `DeletionSelection.Normalize(path)` keeping **first appearance**, returns the first real (un-normalized) path seen per key. No sorting. No WPF.
- **Change:** `FolderComparison.Rebuild()` — replace the inline `GroupBy/OrderBy/Select` with `GroupFolders.OrderedDistinct(group.DuplicateFiles.Select(f => f.FileData.DirPath))`. `BuildColumns` unchanged this step.

## Depends-on
- none (first issue)

## Manual verification (PRD scenarios)
- Contributes to **scenario 2** ("Expand the rail → every distinct folder is listed in group order…") and **scenario 5** ("Check only two specific folders → just those two render, side by side") — at this step only the **column order** is observable.
- **scenario 1** ("…exactly the first 5 (group order) render as columns; rail collapsed") — ordering portion only; full scenario needs 020/030.
- Exercise on a **small** group (a handful of distinct folders): the per-group cap that bounds column count does not exist until issue 020, so a huge group still builds all columns here.

## Assumptions from plan
- **Assumption 4 (helper location):** a new `internal static class` in the core namespace, strings-in/strings-out, zero dependency on `DuplicateFile`/WPF, trivially unit-testable later.
- **Assumption 3 (group-order tie):** confirmed — results tree and the helper both consume the `DuplicateGroup`'s full-path-sorted `DuplicateFiles`, so first-appearance distinct folder == results-tree folder order. The only change is dropping the `OrderBy(key)` in `Rebuild()`. *(No confirmation needed.)*

## Result

### Files changed
- **Added** `DuplicateFileTool/GroupFolders.cs` — `internal static class GroupFolders` in the `DuplicateFileTool` namespace with one pure method `static IReadOnlyList<string> OrderedDistinct(IEnumerable<string> directoryPaths)`. Iterates in order, dedupes by `DeletionSelection.Normalize(path)` keeping first appearance, returns the first un-normalized path per key. No sorting, no WPF.
- **Changed** `DuplicateFileTool/Controls/FolderComparison.xaml.cs` — `Rebuild()` now derives `distinctFolders` via `GroupFolders.OrderedDistinct(group.DuplicateFiles.Select(file => file.FileData.DirPath))`, replacing the inline `GroupBy/OrderBy/Select`. `BuildColumns` unchanged. (`System.Linq` `using` retained — the `.Select` projection still needs it.)

### Build
`dotnet build DuplicateFileTool/DuplicateFileTool.csproj` — **PASS** (0 Warnings, 0 Errors).

### Contradictions flagged
None. `DeletionSelection.Normalize` is `public static` and WPF-independent (confirmed in source); `DeletionSelection` lives in the root `DuplicateFileTool` namespace at the project root, so `GroupFolders.cs` was placed there in the same namespace. Pre-resolved rail/column/constant decisions are not in scope for this step (Step 1 touches only the ordering helper and `Rebuild()`).

## Manual checks
- [ ] Select a group spanning more than 5 distinct folders → columns render in the group's own order (results-tree order), not sorted by path. (PRD scenario 1 — ordering portion; per-group cap not yet present, so all columns still build.)
- [ ] Expand-equivalent observation: the rendered column order matches the order the duplicates are listed in the results tree. (PRD scenario 2 — column order only at this step.)
- [ ] Check only two specific folders (conceptually) / a small group with a handful of distinct folders → columns appear in group order, side by side, no reordering. (PRD scenario 5 — ordering portion.)
- [ ] Same-folder duplicates collapse to a single column (one entry per distinct folder), order-stable. (US-33.)
- [ ] Exercise on a small group (a handful of distinct folders) to confirm ordering without overwhelming column counts (full cap arrives in issue 020).
