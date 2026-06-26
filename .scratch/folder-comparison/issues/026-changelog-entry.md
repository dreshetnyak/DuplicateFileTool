---
id: 026
title: Changelog entry (Work in Progress → Unreleased gating)
phase: Phase 9 — Cross-cutting
status: done
depends_on: [009, 019, 021, 022, 023, 024]
touches_files:
  - DuplicateFileTool/Changes.md
user_stories: []
---

## Description
Add a `New.` bullet to `DuplicateFileTool/Changes.md` describing the folder-comparison control. Per the project's gating convention (and memory): place it under **Work in Progress** while building; promote to **Unreleased** only after the maintainer (Dennis) confirms the change works; on release, move it into a dated `### DuplicateFileTool <version>: <date>` block under **Released**.

Grounding: CLAUDE.md "Versioning" — changelog sections are `## Backlog`, `## Work in Progress`, `## Unreleased`, `## Released`; entries prefixed `New.`/`Bug.`/`Fix.`/`Improvement.`.

## Acceptance criteria
- A `New.` entry describing the folder-comparison control exists in `Changes.md` under the correct section per the gating convention (Work in Progress until confirmed).

## Manual verification
Open `Changes.md` → the `New.` folder-comparison entry is present under Work in Progress (or Unreleased after Dennis confirms), formatted like the existing entries.

## Manual verification performed
Added a single `New.` bullet describing the folder-comparison panel under **Work in Progress** in `DuplicateFileTool/Changes.md` (commit 91c7256). Placed under Work in Progress (not Unreleased) per the gating convention — promote to Unreleased once Dennis confirms the feature works at runtime, then into a dated Released block with issue 025's version on release.
