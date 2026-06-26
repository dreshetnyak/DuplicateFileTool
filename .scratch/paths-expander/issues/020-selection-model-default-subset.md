# 020 — Selection model + render only the default subset

- **Status:** awaiting-verification
- **Plan step:** Step 2

## Goal
Introduce the observable folder-selection model and make the container render only the **default first-5** selected folders, bounding the automatic cost so selecting a large group no longer freezes the app. No rail UI yet.

## Satisfies
- User stories: **US-1, US-2, US-12, US-23, US-24, US-26**
- PRD sections: §"Default selection", §"Selection model", §"Performance posture" (bounds automatic cost)
- US-23 (group-switch reset) and US-24 (post-deletion reset) are delivered here: `Rebuild()` rebuilds `FolderEntries`+default-5 on every `CurrentComparisonGroup` change, and the post-deletion path re-drives that group (assumption 6). No new trigger.

## Exact files / classes / methods (copied from plan)
- **Add:** `FolderSelectionEntry : NotifyPropertyChanged` (in the container's file or a sibling) with `string DisplayPath`, `string NormalizedPath`, `bool IsSelected { get; set; }` (raises change), `bool IsEnabled { get; set; }` (for the ceiling, issue 060). In `FolderComparison` code-behind add `ObservableCollection<FolderSelectionEntry> FolderEntries` and an internal `const int DefaultSelectedCount = 5`.
- **Change:** `Rebuild()` — after computing the ordered distinct list: build `FolderEntries` (one entry per folder, in order), mark the **first `min(5, count)`** `IsSelected=true` while `_suppressSelection` is set (so the issue-050 handler doesn't fire during bulk build); **set the guard before the bulk assignment and clear it after in a `try/finally`** so real user toggles are not permanently suppressed once 050 lands. Then call `BuildColumns(engine, <selected folder paths in order>)`. `BuildColumns` still does a full build but now from the selected slice — **no rail UI yet**, so it always renders the default 5.

## Depends-on
- 010

## Manual verification (PRD scenarios)
- **scenario 1** — "Select a group spanning more than 5 distinct folders → exactly the first 5 (group order) render as columns" (column-count cap is observable here; rail-collapsed part needs 030).
- **scenario 8** — "Switch to another group → rail list and columns reset to the new group's default 5."
- **scenario 9** — "Run a deletion that empties or changes folders → after the refresh, the rail is rebuilt for what remains and reset to default 5."
- Contributes to **scenario 2** (the rendered default-5 the rail list is compared against).

## Assumptions from plan
- **Assumption 6 (post-deletion refresh path):** the rail/selection reset rides the existing `Rebuild()` — OQ-6 re-sets `CurrentComparisonGroup` from the surviving results row, which already fires `Rebuild()`. No new trigger, no change to the deletion command. *(Confirmed, no action.)*

## Result

### Files changed
- `DuplicateFileTool/Controls/FolderComparison.xaml.cs`
  - Added `using System.Collections.ObjectModel;`.
  - Added `internal const int DefaultSelectedCount = 5;` next to the other column constants.
  - Added `internal ObservableCollection<FolderSelectionEntry> FolderEntries { get; } = [];` and `private bool _suppressSelection;`.
  - Changed `Rebuild()`: after `GroupFolders.OrderedDistinct(...)`, it now rebuilds `FolderEntries` (one entry per folder, in order) inside a `try/finally` that sets `_suppressSelection` before the bulk assignment and clears it after; the first `min(DefaultSelectedCount, count)` entries get `IsSelected = true`. It then calls `BuildColumns(engine, <selected DisplayPaths in order>)` so only the default-5 slice renders.
  - Added the sibling type `internal sealed class FolderSelectionEntry : NotifyPropertyChanged` with `string DisplayPath`, `string NormalizedPath`, `bool IsSelected` (raises change), `bool IsEnabled` (defaults `true`, raises change), plus a `(displayPath, normalizedPath)` ctor.

### Build
- `dotnet build DuplicateFileTool/DuplicateFileTool.csproj` → **succeeded** (0 errors, 1 warning).
- Warning CS0414 `'FolderComparison._suppressSelection' is assigned but its value is never used` is **expected by design**: the field is read only by the issue-050 `OnEntrySelectionChanged` handler, which is explicitly out of scope for this issue. The field is added now so the try/finally guard pattern is already in place.

### Contradiction flagged
- The plan/issue says to add `FolderEntries` to the code-behind without specifying accessibility. `FolderComparison` is a `public` partial class but `FolderSelectionEntry` is `internal` (per the issue's "`FolderSelectionEntry : NotifyPropertyChanged`" with no public requirement), so a `public` `FolderEntries` failed to compile (CS0053 inconsistent accessibility). Resolved by making `FolderEntries` `internal`, which also matches the existing `internal Engine` property on the same class. No rail XAML binds it yet (binding wiring is issues 030/040), so `internal` is sufficient.

## Manual checks
- [ ] **scenario 1** — Select a group spanning more than 5 distinct folders → exactly the first 5 (group order) render as columns. (Rail-collapsed part needs issue 030.)
- [ ] **scenario 8** — Switch to another group → rail list and columns reset to the new group's default 5.
- [ ] **scenario 9** — Run a deletion that empties or changes folders → after the refresh, the rail is rebuilt for what remains and reset to default 5.
- [ ] Contributes to **scenario 2** — the rendered default-5 is the set the rail list will be compared against (rail UI lands in issues 030/040).
- [ ] **US-26** — a group with fewer than 5 distinct folders renders all of them.
