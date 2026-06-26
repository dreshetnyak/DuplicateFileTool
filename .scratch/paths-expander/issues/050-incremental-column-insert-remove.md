# 050 — Incremental column insert/remove (the hard part)

- **Status:** awaiting-verification
- **Plan step:** Step 5

## Goal
Replace "build all columns at once" with incremental insert/remove: checking a folder inserts exactly its column at the correct group-order position; unchecking removes exactly that column + its splitter — **without disturbing the other columns' tree expansion, scroll position, or dragged width**.

## Satisfies
- User stories: **US-13, US-14, US-15, US-16, US-17, US-34**
- PRD sections: §"Incremental column maintenance"

## Exact files / classes / methods (copied from plan)
- **Model:** introduce an ordered `List<ColumnSlot>` of **currently-shown** columns + `Dictionary<string,ColumnSlot>` keyed by `NormalizedPath`, where `ColumnSlot` holds the `FolderComparisonItem`, its root `FolderItem`, and (optionally) its trailing `GridSplitter`. Refactor `BuildColumns` (full build) to populate this model; `_roots` becomes derivable from it (or keep `_roots` in sync).
- **Add (code-behind):**
  - `OnEntrySelectionChanged(entry)` — fired by `FolderSelectionEntry.IsSelected` (skipped while `_suppressSelection`): if now selected → `InsertColumn(folder)`; else → `RemoveColumn(folder)`.
  - `InsertColumn(folder)` — **mirror `BuildColumns`' gone-folder guard**: re-fetch via `FileSystem.GetFileData(folder)` and if `fd.IsEmpty` (folder deleted/moved since the search) do **not** create a slot — instead revert the entry's `IsSelected` back to `false` under the `_suppressSelection` guard (or set `IsEnabled=false`) so the rail checkbox state and the rendered columns **stay in agreement** (US-34). Otherwise create exactly one `FolderComparisonItem` + `FolderItem(...){IsExpanded=true}` root. Compute the **ordered insertion index over the actually-shown slots** (not the raw selected set) from the canonical `GroupFolders.OrderedDistinct` order. Do **in-place `ColumnDefinition` insertion + `Grid.Column` renumber** of retained children (insert `[folder][splitter]` defs at the right index, or append `[splitter][folder]` when adding at the end; first-ever column adds a single folder def, no splitter). Update the slot model and `_roots`. **Never touch other items' instances.**
  - `RemoveColumn(folder)` — find the slot; `root.CancelPendingScan()` (this aborts only an **uncommitted** mark-scan and commits nothing — it never removes existing deletion marks; see issue 090). Then by case: **N≥2, removing a non-last column** → remove its `FolderComparisonItem` + its **trailing** splitter, `RemoveAt` their two `ColumnDefinition`s, decrement `Grid.Column` by 2 on children after them. **N≥2, removing the last column** → remove its item + the **preceding** splitter and their two `ColumnDefinition`s (no renumber needed — they are at the tail). **N==1, removing the only column** → remove just its single folder `ColumnDefinition` + item; there is **no splitter** to remove; route to the issue-070 empty state. Drop from the slot model/`_roots`.
- **Suggested internal staging (still one issue):** (5a) refactor `BuildColumns` to build-and-populate the `ColumnSlot` model + dictionary with `_roots` derived from it — full-build behavior unchanged, independently checkable that the default 5 still render as in 020; then (5b) add `OnEntrySelectionChanged` + `InsertColumn`/`RemoveColumn` in-place surgery on top of that model. The 5a/5b split is a review aid, not a separate issue.

## Depends-on
- 010, 020, 040

## Manual verification (PRD scenarios)
- **scenario 3** — "Check a 6th folder → its column inserts at the correct group-order position; the existing 5 columns keep their expansion, scroll, and width."
- **scenario 4** — "Uncheck a middle folder → only that column (and a splitter) is removed; the others are undisturbed."
- **scenario 5** — "Check only two specific folders (uncheck the rest) → just those two render, side by side."
- **scenario 7** — "Toggle folders rapidly → no leaked columns or handlers; rail checkbox state and rendered columns stay consistent."

## Assumptions from plan
- **Assumption 2 — CONFIRM (in-place surgery / scroll preservation):** incremental insert/remove is done by **in-place `ColumnDefinition` surgery + `Grid.Column` renumbering on retained children — never removing/re-adding the untouched `FolderComparisonItem` instances** (expansion is data-bound to `FolderItem.IsExpanded` and survives regardless; in-place keeps scroll too). **Scenario 3/4 is the runtime check.** **Fallback if scroll still resets:** a `RelayoutColumns()` that reuses item instances but re-adds them (accept expansion-preserved/scroll-reset), OR snapshot+restore each retained `ScrollViewer` offset around the relayout.

## Result

**Files changed (1):**
- `DuplicateFileTool/Controls/FolderComparison.xaml.cs` — all changes scoped to this single code-behind file. No XAML, resx, or other source touched.

**What changed:**
- Added a private nested `ColumnSlot` class holding `NormalizedPath`, the `FolderComparisonItem` (`Item`), its root `FolderItem` (`Root`), and an optional trailing `GridSplitter` (`Splitter`).
- Added the slot model: ordered `List<ColumnSlot> _slots` + `Dictionary<string,ColumnSlot> _slotsByPath` (OrdinalIgnoreCase, keyed by `NormalizedPath`). `_roots` is kept in sync with the slots.
- Refactored `BuildColumns` (full build) to construct and populate `ColumnSlot`s + the dictionary + `_roots`, factoring column-definition and splitter creation into shared `NewFolderColumnDefinition()` / `NewSplitter()` helpers reused by the incremental path. Full-build layout behavior is unchanged (default 5 render as before).
- Added per-entry subscription: in `Rebuild`, each new `FolderSelectionEntry` is subscribed to `OnEntryPropertyChanged`; the previous group's entries are unsubscribed first (tracked via `_subscribedEntries`). `Rebuild` also clears the slot model/dictionary alongside `_roots`.
- `OnEntryPropertyChanged` → skips while `_suppressSelection` and only acts on `IsSelected`; routes to `OnEntrySelectionChanged(entry)` → `InsertColumn` (now selected) / `RemoveColumn` (now unselected).
- `InsertColumn(entry)` — mirrors `BuildColumns`' gone-folder guard via `FileSystem.GetFileData`; on `fd.IsEmpty` reverts `entry.IsSelected = false` under the `_suppressSelection` guard (no slot created). Otherwise creates exactly one `FolderComparisonItem` + `FolderItem{IsExpanded=true}`, computes the insertion index OVER THE ACTUALLY-SHOWN SLOTS from the canonical `FolderEntries`/`GroupFolders.OrderedDistinct` order (`IndexInCanonicalOrder` helper), and does in-place `ColumnDefinition` insert + `Grid.Column` renumber of retained children. Three cases handled: first-ever column (single folder def, no splitter); append at end (`[splitter][folder]`, no renumber, previous slot gains the trailing splitter); insert before an existing column (`[folder][splitter]` defs, retained children bumped +2). Never re-adds untouched items.
- `RemoveColumn(entry)` — finds the slot, calls `slot.Root.CancelPendingScan()` (aborts only an uncommitted mark-scan; commits nothing; removes no existing deletion marks). Three cases: N==1 only column (remove single folder def + item, no splitter, columns area left empty — issue 070 routes the empty state); N≥2 non-last (remove item + trailing splitter, `RemoveAt` both defs, decrement `Grid.Column` by 2 on later children); N≥2 last (remove item + the preceding splitter owned by the previous slot, `RemoveAt` both tail defs, no renumber). Drops from `_slots`/`_slotsByPath`/`_roots`.

**Build:** PASS — `dotnet build DuplicateFileTool/DuplicateFileTool.csproj` → Build succeeded, 0 Warning(s), 0 Error(s).

**Contradictions / flags:** none. Assumption 2 (in-place surgery, never re-adding untouched items) followed verbatim; scroll/expansion preservation is the runtime check in scenarios 3/4. The fallback (`RelayoutColumns` / snapshot-restore scroll) was not needed at build time and is left as documented for runtime verification.

## Manual checks
- [ ] **scenario 3** — Check a 6th folder → its column inserts at the correct group-order position; the existing 5 columns keep their expansion, scroll, and width.
- [ ] **scenario 4** — Uncheck a middle folder → only that column (and a splitter) is removed; the others are undisturbed.
- [ ] **scenario 5** — Check only two specific folders (uncheck the rest) → just those two render, side by side.
- [ ] **scenario 7** — Toggle folders rapidly → no leaked columns or handlers; rail checkbox state and rendered columns stay consistent.
