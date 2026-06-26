# 070 — Empty-state and no-group states

- **Status:** awaiting-verification
- **Plan step:** Step 7

## Goal
Distinguish three column-area states: no current group → existing placeholder, no rail; group with zero folders selected → a distinct "no folders selected" message, rail visible; group with ≥1 selected → the columns.

## Satisfies
- User stories: **US-25, US-27**
- PRD sections: §"Empty & no-group states"

## Exact files / classes / methods (copied from plan)
- **Change:** `FolderComparison.xaml` — add a third visual `PART_EmptySelection` `TextBlock` (centered, in column 1) for "no folders selected". Introduce a small `UpdateColumnsAreaState()` that selects among three states: **no group** → existing `PART_Placeholder`, **rail hidden** (column-0 width 0 / `Collapsed`); **group, 0 selected** → `PART_EmptySelection`, rail visible; **group, ≥1 selected** → `PART_Scroll`, rail visible. Route `Rebuild()`, `InsertColumn`, `RemoveColumn` through it.

## Depends-on
- 030, 050; the empty-state string from 080

## Manual verification (PRD scenarios)
- **scenario 10** — "Uncheck every folder → the 'no folders selected' message appears; re-checking restores columns."
- Also completes the no-group half of **scenario 1 / US-25** — "when no group is selected, the existing placeholder shown and no rail."

## Assumptions from plan
- (none specific to this step; consumes the `Ui_FolderComparison_NoFoldersSelected` string from 080.)

## Result

**Files changed:**
- `DuplicateFileTool/Controls/FolderComparison.xaml` — added a third visual `PART_EmptySelection` `TextBlock` (centered, column 1, `Visibility="Collapsed"` by default) bound to `{x:Static resx:Resources.Ui_FolderComparison_NoFoldersSelected}`; added a block comment documenting the three column-area states.
- `DuplicateFileTool/Controls/FolderComparison.xaml.cs` — replaced the two-state `ShowPlaceholder()` with a three-state `UpdateColumnsAreaState()` helper and routed `Rebuild()` (no-group path), `BuildColumns` (built / no-survivor paths), `InsertColumn`, and `RemoveColumn` through it. The helper selects among: no group → `PART_Placeholder`, rail hidden (column-0 width 0, toggle collapsed); group + 0 columns → `PART_EmptySelection`, rail visible; group + ≥1 column → `PART_Scroll`, rail visible. The no-group path in `Rebuild()` now also clears `FolderEntries`.

**Build:** PASS — `dotnet build DuplicateFileTool/DuplicateFileTool.csproj`, 0 warnings, 0 errors.

**Contradictions / notes flagged:**
- The plan describes the no-group rail-hidden state as "column-0 width 0 / `Collapsed`". Implemented as column-0 width 0 **and** the rail toggle `Collapsed`; the rail `Border` lives in column 0, so a zero-width column already removes it from view (no separate `Visibility` toggle on the `Border` was needed).
- `UpdateColumnsAreaState()` sets the group-state rail width from the toggle's current `IsChecked` (expanded vs collapsed) rather than forcing `CollapsedRailWidth`. This is required because the helper is also called from `InsertColumn`/`RemoveColumn`, which fire while the user is interacting with an **expanded** rail — forcing collapsed there would snap the rail shut underneath the user. On a group switch the toggle is not reset (pre-existing behavior unchanged by this issue), so the rail keeps whatever expanded/collapsed state it had; resetting the toggle on group change is out of this issue's scope.

## Manual checks
- [ ] **scenario 10** — Uncheck every folder → the "no folders selected" message (`PART_EmptySelection`) appears; re-checking a folder restores the columns. (Exercises 050's N==1 remove → empty-state path.)
- [ ] **scenario 1 / US-25 (no-group half)** — With no group selected, the existing placeholder (`PART_Placeholder`, "no group selected") is shown and the rail is hidden (column 0 has zero width).
