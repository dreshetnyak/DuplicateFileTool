# 040 — Rail folder list with checkboxes (clip + tooltip)

- **Status:** awaiting-verification
- **Plan step:** Step 4

## Goal
Populate the expanded rail with every distinct folder of the current group, each a checkbox + clipped path with a full-path tooltip, bound to the same selection model the columns derive from so the list and the columns can never disagree.

## Satisfies
- User stories: **US-9, US-10, US-11, US-12, US-34**
- PRD sections: §"Selection model" (binding), §"Layout & expander" (clip/tooltip)

## Exact files / classes / methods (copied from plan)
- **Change:** `PART_RailList` = an `ItemsControl`/`ListBox` bound to `FolderEntries`; item template = `CheckBox` (`IsChecked`↔`IsSelected`, `IsEnabled`↔entry `IsEnabled`) + a `TextBlock` (`Text=DisplayPath`, `ToolTip=DisplayPath`, `TextTrimming=CharacterEllipsis`) inside the fixed rail width. Add a rail title/header `TextBlock` (localized, issue 080) near the toggle. Checkbox state is bound to the same `IsSelected` the columns derive from, so list and columns **cannot disagree** (US-34).

## Depends-on
- 020, 030

## Manual verification (PRD scenarios)
- **scenario 2** — "Expand the rail → every distinct folder is listed in group order, the first 5 checked, matching both the rendered columns and the results-tree order."
- **scenario 13** (partial) — "Long paths are clipped with a full-path tooltip" (the en/es/ru-strings part is issue 080).
- Note: toggling a checkbox at this step is **expected to NOT yet add/remove a column** — that wiring lands in issue 050. This issue verifies only the static list/order/initial-checked state matching the rendered default 5.

## Assumptions from plan
- (none specific to this step; relies on the rail shell from 030 and the model from 020.)

## Result

### Files changed
- `DuplicateFileTool/Controls/FolderComparison.xaml` — replaced the empty `ContentControl x:Name="PART_RailList"` placeholder (from 030) with a vertical `ScrollViewer` wrapping an `ItemsControl x:Name="PART_RailList"`. `ItemsSource` binds to `FolderEntries` via `RelativeSource AncestorType=UserControl` (the collection lives in the code-behind, not the DataContext). The `ItemTemplate` is a `CheckBox` (`IsChecked` two-way ↔ `IsSelected`, `IsEnabled` ↔ entry `IsEnabled`) whose content is a `TextBlock` (`Text=DisplayPath`, `ToolTip=DisplayPath`, `TextTrimming=CharacterEllipsis`). The rail title `TextBlock` using `Ui_FolderComparison_Rail_Title` already existed from 030 and was left as is.
- `DuplicateFileTool/Controls/FolderComparison.xaml.cs` — visibility promotion (see flag below): `FolderEntries` property `internal` → `public`; `FolderSelectionEntry` class `internal sealed` → `public sealed`. The bound members `DisplayPath`/`NormalizedPath`/`IsSelected`/`IsEnabled` were already `public`.

### Build
`dotnet build DuplicateFileTool/DuplicateFileTool.csproj` — **PASS** (0 errors, 1 warning). The single warning, `CS0414 'FolderComparison._suppressSelection' is assigned but its value is never used`, is pre-existing from issue 020 (the field is consumed by the issue-050 selection-changed handler, not yet present) and is not introduced by this change.

### Flags / contradictions
- **Visibility promotion (as the issue authorized).** Issue 020 left `FolderEntries` and `FolderSelectionEntry` `internal`; WPF data binding to non-public types/members can silently fail at runtime. Both were promoted to `public` so the rail list binds reliably.
- **Faithful adaptation — base class.** `FolderSelectionEntry` derived from the `internal abstract` `NotifyPropertyChanged` base. A `public` class cannot derive from an `internal` base (CS0060). To keep the change scoped entirely to this issue's file (rather than widening the shared `NotifyPropertyChanged` base to public, which is outside this issue), `FolderSelectionEntry` now implements `INotifyPropertyChanged` directly with its own `PropertyChanged` event and private `OnPropertyChanged([CallerMemberName])` — behaviorally identical to the former base. No other class was touched.
- Per the issue, toggling a checkbox does **not** yet add/remove a column (that wiring is issue 050). This issue delivers only the static list.

## Manual checks
- [ ] **Scenario 2** — Select a group spanning more than 5 distinct folders, then expand the rail: every distinct folder is listed in group order, the first 5 checked, matching both the rendered columns and the results-tree order.
- [ ] **Scenario 13 (partial)** — Long folder paths in the rail are clipped (character ellipsis) with the full path shown on hover via tooltip. (The en/es/ru string presence is verified by issue 080.)
- [ ] Note: toggling a rail checkbox is **expected to NOT add/remove a column yet** (issue 050 wires that). This step verifies only the static list, its order, and the initial-checked state matching the rendered default 5.
