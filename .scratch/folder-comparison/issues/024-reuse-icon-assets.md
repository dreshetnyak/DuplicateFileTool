---
id: 024
title: Reuse existing icon assets for mark/clear buttons
phase: Phase 9 — Cross-cutting
status: done
depends_on: [014, 016]
touches_files:
  - DuplicateFileTool/Controls/FolderTree.xaml
  - DuplicateFileTool/Controls/FolderComparisonItem.xaml
user_stories: []
---

## Description
Ensure the new control reuses existing image assets — **no new PNGs**: the row mark button uses `Delete.png`/`Undo.png` (as the results tree), and the per-folder header clear button uses `Clear.png` (or `Reset.png`). This is largely a verification/constraint task over the XAML authored in 014/016; correct any references that point at a non-existent or new asset.

Grounding: existing `Images/` assets — `Delete.png`, `Undo.png`, `Reset.png`; confirm `Clear.png` exists, else use `Reset.png` (the results-tree Reset button uses `Reset.png`). The results-tree mark buttons use `Delete.png`/`Undo.png` (MainWindow.xaml 75–90).

## Acceptance criteria
- Mark buttons render using `Delete.png`/`Undo.png`; the per-folder clear uses `Clear.png` or `Reset.png`.
- No new image files are added to `Images/`.

## Manual verification
Run the app, open the control → mark buttons and the per-folder clear button render with the existing icons (visually identical to the results tree's). `git status` shows no new files under `Images/`.

## Manual verification performed
Satisfied by issues 014 (39a6ab3) and 016 (01dd998) — no separate code change. Verified: the only image references in the new controls are `/Images/Delete.png`, `/Images/Undo.png` (FolderTree mark buttons) and `/Images/Clear.png` (FolderComparisonItem clear button) — all pre-existing assets. `git status -- DuplicateFileTool/Images/` shows no added files.
