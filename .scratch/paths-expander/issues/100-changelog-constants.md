# 100 — Changelog + constant confirmation

- **Status:** todo
- **Plan step:** Step 10

## Goal
Record the rail as a `New.` line folded into the still-unreleased folder-comparison Work-in-Progress changelog entry (no separate version bump), and confirm the two tunable constants with Dennis.

## Satisfies
- PRD sections: §"Further Notes" (versioning/changelog), tunable-constants flag
- (No PRD manual-verification scenario — this is a release/bookkeeping step.)

## Exact files / classes / methods (copied from plan)
- **Change (a) — changelog now:** fold a single `New.` bullet about the rail into the **existing folder-comparison Work-in-Progress** entry in `DuplicateFileTool/Changes.md` (do **not** add a separate version bump; 2.4.0 / parent issue 025 remains the release vehicle). This WIP line lands now; it promotes to **Unreleased** only after Dennis confirms it works at runtime, per the gating convention.
- **Change (b) — constants:** confirm final values for `SafetyCeiling` (50) and `ExpandedRailWidth` (~250px) with Dennis during implementation.

## Depends-on
- 010, 020, 030, 040, 050, 060, 070, 080, 090

## Manual verification (PRD scenarios)
- none (release/changelog step). Gate: promotion WIP→Unreleased and **any commit happen only after runtime verification and an explicit request.**

## Assumptions from plan
- **Assumption 5 — CONFIRM (constants):** `SafetyCeiling = 50` and `ExpandedRailWidth ≈ 250px` are proposed defaults to confirm; `DefaultSelectedCount = 5` is the user's fixed spec.
