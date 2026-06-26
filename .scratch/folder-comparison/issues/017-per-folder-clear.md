---
id: 017
title: Per-folder clear button wiring (recursive)
phase: Phase 5 — Folder sub-control
status: done
depends_on: [001, 012, 016]
touches_files:
  - DuplicateFileTool/Controls/FolderComparisonItem.xaml.cs
user_stories: [21, 22]
---

## Description
Wire the sub-control header's clear button to remove **every** unified-set entry under that folder's path (recursive). Because the set is unified, this also clears those rows in the results tree. Other folders are untouched; totals drop accordingly.

Grounding: unified set from 001 (needs a "remove all under prefix" operation, or iterate set entries under the folder path). Header button defined in 016.

## Acceptance criteria
- Clicking clear empties that folder's marks in **both** the folder tree and the results tree.
- Other folders' marks are untouched.
- Totals drop by exactly the cleared files' count/size.

## Manual verification
PRD scenario 6: mark files in two folder columns, click clear on one → that column's marks (and the same files in the results tree) clear; the other column is unchanged; totals reflect only the remaining marks.

## Manual verification performed
Satisfied by issue 016 (commit 01dd998) — no separate code change required. `FolderComparisonItem.OnClearFolder` calls `Engine.DeletionSelection.RemoveAllUnder(Root.FullName)`, which:
- removes every marked FILE entry under the folder prefix, firing one `Removed` event each → totals decrement and the SAME files clear in the results tree (unified set, weak per-file handlers) and in the folder tree;
- removes the folder's (and sub-folders') entries from the explicitly-selected-directories set;
- is prefix-scoped, so only this folder's marks clear — other folder columns are untouched.
All three acceptance criteria hold. A human verifies via PRD scenario 6 once the container (018) hosts multiple columns.
