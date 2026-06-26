# 060 — Safety ceiling

- **Status:** awaiting-verification
- **Plan step:** Step 6

## Goal
Add a large safety ceiling on the number of simultaneously selected folders so a stray click cannot try to render hundreds of columns; at the ceiling, unchecked checkboxes are disabled with a short tooltip while checked ones can still be unchecked.

## Satisfies
- User stories: **US-18, US-19, US-20**
- PRD sections: §"Safety ceiling"

## Exact files / classes / methods (copied from plan)
- **Add:** `const int SafetyCeiling = 50` *(confirm)*. After every selection change (and at build), if selected count `== SafetyCeiling` set `IsEnabled=false` on all **unselected** entries; otherwise `IsEnabled=true` on all. Checked entries always stay enabled (can uncheck). No "select all" affordance. Disabled checkboxes show the ceiling tooltip (issue 080): set `ToolTipService.ShowOnDisabled="True"` and the ceiling `ToolTip` on the **same `CheckBox` element** whose `IsEnabled` binds to `entry.IsEnabled` (not on an enabled ancestor), so the tooltip surfaces while the checkbox is disabled.

## Depends-on
- 020, 040, 050

## Manual verification (PRD scenarios)
- **scenario 6** — "Reach the safety ceiling → further checkboxes are disabled with a tooltip; already-checked folders can still be unchecked."

## Assumptions from plan
- **Assumption 5 — CONFIRM (ceiling value):** `SafetyCeiling = 50` is a proposed default to confirm during implementation. (`DefaultSelectedCount = 5` is the user's fixed spec.)

## Result
- **Decision applied:** `SafetyCeiling = 50` (pre-confirmed; used exactly 50).
- **Files changed:**
  - `DuplicateFileTool/Controls/FolderComparison.xaml.cs` — added `internal const int SafetyCeiling = 50;`; added private `EnforceSafetyCeiling()` (at the ceiling, `IsEnabled=false` on all unselected entries, otherwise `IsEnabled=true` on all; selected entries always stay enabled); called it after the add/remove in `OnEntrySelectionChanged(...)` and after `BuildColumns(...)` at the end of `Rebuild()`. The pass flips only `IsEnabled`, never `IsSelected`, so it cannot re-trigger column insert/remove (no extra guard needed since `OnEntryPropertyChanged` only reacts to `IsSelected`).
  - `DuplicateFileTool/Controls/FolderComparison.xaml` — on the rail item-template `CheckBox` (the same element whose `IsEnabled` binds to `entry.IsEnabled`) added `ToolTipService.ShowOnDisabled="True"` and `ToolTip="{x:Static resx:Resources.Ui_FolderComparison_Rail_LimitReached}"` so the ceiling tooltip surfaces while the checkbox is disabled.
- **Build:** `dotnet build DuplicateFileTool/DuplicateFileTool.csproj` — **PASS** (0 warnings, 0 errors).
- **Contradictions flagged:** none. (`Ui_FolderComparison_Rail_LimitReached` confirmed present in all four resx + Designer per issue 080.)

## Manual checks
- [ ] **scenario 6** — Reach the safety ceiling (select 50 folders) → further (unchecked) checkboxes are disabled and show the "limit reached" tooltip on hover; already-checked folders can still be unchecked, and unchecking one re-enables the rest.
