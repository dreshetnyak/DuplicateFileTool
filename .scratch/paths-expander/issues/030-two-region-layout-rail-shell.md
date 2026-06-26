# 030 — Two-region container layout + collapsible rail shell

- **Status:** awaiting-verification
- **Plan step:** Step 3

## Goal
Turn the container into a two-region layout — a left, horizontally-collapsible rail and the existing horizontally-scrolling columns region — with the expander toggle at the **top**, expanding to the right by **pushing** the columns (never overlay), at a fixed width, collapsed by default.

## Satisfies
- User stories: **US-4, US-5, US-6, US-7, US-8, US-35**
- PRD sections: §"Layout & expander"

## Exact files / classes / methods (copied from plan)
- **Change:** `FolderComparison.xaml` — wrap the existing content in an outer 2-column `Grid`: column 0 = the rail (`Width` toggled between collapsed strip `Auto`/~26px and expanded `~250px`), column 1 = the existing `PART_Scroll`/`PART_Placeholder` stack (move them into column 1). Rail = a rounded `Border` (echo settings-expander style) containing `[Auto] top ToggleButton (PART_RailToggle)` over `[*] PART_RailList` (the folder list, issue 040). The toggle's `IsChecked` drives column-0 width (collapsed↔expanded) via a trigger/code-behind; default **unchecked/collapsed every build** (no persistence). Dock/push only — rail is a real grid column, never an overlay. Long-path clipping handled in 040's item template.
- **Add consts:** `ExpandedRailWidth ≈ 250` *(confirm)*, `CollapsedRailWidth ≈ 26` *(confirm)*.

## Depends-on
- 020 (entries exist to list in 040); the layout itself depends only on the XAML.

## Manual verification (PRD scenarios)
- **scenario 1** — completes the "rail present & collapsed by default" half of "exactly the first 5 render as columns; rail collapsed."
- Plus the layout behaviours: expand/collapse + push (US-7), button-on-top (US-6), fixed width (US-8), starts collapsed (US-35).

## Assumptions from plan
- **Assumption 1 — CONFIRM (non-Expander rail):** a WPF `Expander ExpandDirection="Right"` cannot give "button on top, content expands right" without a custom `ControlTemplate`. Implement the rail as a **rounded `Border` with `[Auto toggle row][* list row]`** where a top `ToggleButton` drives the rail `ColumnDefinition` width — matching the settings *look* (rounded border) and the "button on top, grows right" behavior. **Flagged for confirmation.** (Fallback: restyled `Expander ExpandDirection="Right"` with a custom header template — same result, more template-fighting.)
- **Assumption 5 — CONFIRM (rail width):** `ExpandedRailWidth ≈ 250px` and `CollapsedRailWidth ≈ 26px` are proposed defaults to confirm during implementation.

## Result

**Files changed**
- `DuplicateFileTool/Controls/FolderComparison.xaml` — wrapped the existing content in an outer 2-column `Grid`. Column 0 is the rail: a rounded, light-gray-bordered `Border` (`CornerRadius=10`, echoing the settings expanders) holding `[Auto]` a top `ToggleButton` named `PART_RailToggle` (with the rail title `TextBlock` beside it) over `[*]` an empty `PART_RailList` `ContentControl` placeholder (folder list is issue 040). Column 1 holds the moved-in existing `PART_Scroll`/`PART_Columns` + `PART_Placeholder` content. The rail column (`PART_RailColumn`) starts at the collapsed strip width. The toggle tooltip uses `{x:Static resx:Resources.Ui_ToolTip_FolderComparison_Rail_Toggle}`; the title uses `{x:Static resx:Resources.Ui_FolderComparison_Rail_Title}`.
- `DuplicateFileTool/Controls/FolderComparison.xaml.cs` — added `internal const int ExpandedRailWidth = 250;` and `internal const int CollapsedRailWidth = 26;`, plus `OnRailToggleChecked`/`OnRailToggleUnchecked` handlers that drive `PART_RailColumn.Width` between collapsed and expanded. Width is driven from code-behind (per the issue's "trigger or code-behind" allowance) because a `ToggleButton.IsChecked` → `ColumnDefinition.Width` binding/trigger is not cleanly expressible in XAML.

**Build:** PASS — `dotnet build DuplicateFileTool/DuplicateFileTool.csproj` → 0 errors. The only warning (`_suppressSelection` assigned but never used) is pre-existing from issue 020 (consumed in a later issue) and unrelated to this change.

**Decisions applied (per pre-resolved direction):** rail is a custom rounded `Border` + top `ToggleButton`, NOT a WPF `Expander` (Assumption 1, confirmed). `ExpandedRailWidth=250`, `CollapsedRailWidth=26` (Assumption 5, confirmed). Default unchecked/collapsed on every build (no persistence). Rail is a real grid column — dock/push only, never an overlay.

**Contradictions flagged:** none. `FolderEntries`/`FolderSelectionEntry` remain `internal` and are not bound in this issue (that is issue 040), so no visibility change was needed.

### Correction (post-review): Assumption 1 reversed — use the standard WPF Expander

Assumption 1 (custom `Border` + `ToggleButton`, "the standard Expander can't do button-on-top/expand-right") was **wrong**. The stock `Expander ExpandDirection="Right"` already renders the round chevron toggle button at the top (`ExpanderRightHeaderStyle`) and expands its content to the right — exactly the PRD's intent (PRD line 71 explicitly called for the standard expander). The custom path gave a square hamburger button for no real gain.

Reworked to the standard control:
- `FolderComparison.xaml` — rail is now a standard `Expander x:Name="PART_RailExpander" ExpandDirection="Right" IsExpanded="False"` (round top toggle button) wrapped in the rounded `Border x:Name="PART_Rail"`. `Header` = the rail title, `ToolTip` = the toggle tooltip. Content = the folder-list `ScrollViewer`/`ItemsControl` at fixed `Width=250`. Column 0 is now `Width="Auto"` — the Expander sizes itself (slim header strip collapsed; header + 250px list expanded) and the grid pushes column 1.
- `FolderComparison.xaml.cs` — removed `ExpandedRailWidth`/`CollapsedRailWidth` consts and the `OnRailToggle*` width-toggling handlers (the Expander owns expand/collapse now). `Rebuild()` sets `PART_RailExpander.IsExpanded = false` to start collapsed each (re)build (US-35); `UpdateColumnsAreaState()` hides the rail by toggling `PART_Rail.Visibility` (Auto column → 0 width) instead of setting a pixel width.
- Build: PASS (0 warnings, 0 errors).

**Trade-off:** the standard Right expander renders the header **title rotated 90° (vertical)** down the strip. Acceptable/standard; can be templated to horizontal if undesired. **Constants update:** `ExpandedRailWidth=250` is now the XAML content width literal; `CollapsedRailWidth` no longer exists (the standard expander header self-sizes the collapsed strip).

## Manual checks

- [ ] **scenario 1 (rail half)** — Select a group spanning more than 5 distinct folders → the rail is present and collapsed by default (slim toggle strip on the left).
- [ ] **US-7 (expand/collapse + push)** — Click the rail toggle → the rail expands horizontally to the right and pushes the folder-columns region over (never overlays it); click again → it collapses back, columns reflow.
- [ ] **US-6 (button on top)** — The expander toggle button sits at the top of the rail; the rail title text is visible beside it when expanded.
- [ ] **US-8 (fixed width)** — Expanded, the rail is a fixed ~250px width; collapsed it is the ~26px strip; the width does not drift between toggles.
- [ ] **US-35 (starts collapsed)** — On every group selection / app run the rail starts collapsed (no persisted expand state).
