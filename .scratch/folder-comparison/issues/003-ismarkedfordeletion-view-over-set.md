---
id: 003
title: Make DuplicateFile.IsMarkedForDeletion a view over the selection set
phase: Phase 1 — Engine selection model
status: done
depends_on: [001]
touches_files:
  - DuplicateFileTool/DuplicatesEngine.cs
user_stories: [16, 17]
---

## Description
Refactor `DuplicateFile.IsMarkedForDeletion` so the **getter returns set membership** for the file's path and the **setter adds/removes** the path (with its size) in the unified set (001). The file subscribes to set changes for its path and, on change, raises `PropertyChanged` for itself and re-raises `IsMarkForDeletionVisible` for its siblings (so a mark made from any view updates the results tree row live). Wire the set reference from the engine through `DuplicateGroup` to each `DuplicateFile` at construction.

Grounding (`DuplicateFileTool/DuplicatesEngine.cs`):
- `DuplicateFile.IsMarkedForDeletion` setter (lines 30–49) currently holds a private `_isMarkedForDeletion`, notifies self + loops siblings to re-raise `IsMarkForDeletionVisible` (line 29).
- `IsMarkForDeletionVisible => !IsMarkedForDeletion && ParentGroup.DuplicateFiles.Count(f => !f.IsMarkedForDeletion) > 1;` (line 29) — the results-tree last-copy protection. **Keep this rule intact.**
- `DuplicateFile` ctor is `(DuplicateGroup parentGroup, MatchResult matchResult)`; `DuplicateGroup` ctor lines 145–152; groups created at line 344.

## Acceptance criteria
- Marking a `DuplicateFile` adds its path to the set; unmarking removes it; and vice-versa (a set change for that path flips the property).
- `IsMarkForDeletionVisible` still hides the mark button on the last unmarked copy of a group — the results tree alone still cannot fully mark a group.
- A mark toggled from elsewhere (directly on the set) updates the row's Delete↔Undo icon and strikethrough text live.
- **Existing results-tree marking UX unchanged:** icon swap, strikethrough (`MarkedFileTextStyle`), sibling visibility, and per-row toggle behave identically to today.

## Manual verification
Build, run, find duplicates. PRD scenario 5 (partial): mark/unmark duplicate rows in the results tree — icon, strikethrough, and last-copy button-hiding behave exactly as before. Totals wiring is verified in 004.

## Manual verification performed

Build: `dotnet build DuplicateFileTool/DuplicateFileTool.csproj -nologo -clp:ErrorsOnly` → **Build succeeded. 0 Warning(s), 0 Error(s).**

There is no test project; the following is how a human verifies the refactor preserved behavior.

**Mark/unmark UX unchanged (results tree):**
1. Run a search that yields duplicate groups. For a group with ≥3 copies, click the mark (Delete) button on one row. The row icon flips Delete→Undo and the text shows strikethrough/gray (`MarkedFileTextStyle`) — identical to before. Click again (Undo) → it reverts.
2. The "Selected for deletion size" total still rises/falls by that file's size on each toggle (driven by the still-present `DeletionMarkToggle` command event — totals plumbing untouched this issue).

**Last-copy protection (`IsMarkForDeletionVisible`):**
3. In a group of N copies, mark copies until only one remains unmarked. The last unmarked copy's mark button disappears (it is hidden, not disabled) — the results tree alone still cannot fully mark a group. Unmark one → the hidden button reappears on the now-second-to-last copy. This rule is byte-for-byte the same expression as before.

**Live update from external set changes:**
4. (Becomes fully exercisable once the folder tree exists, but already testable via Reset / Auto Select.) Use Auto Select by Path to mark several duplicates: every matching results row immediately shows the Delete→Undo icon and strikethrough, and siblings' mark-button visibility recomputes — because each `DuplicateFile` now reacts to `DeletionSelection.Changed` rather than only to its own setter.
5. Reset selection: every marked row clears its strikethrough/icon live (the `Reset` change notifies every file).

**No handler leak across repeated searches:**
6. Run a search, then run several more searches back-to-back (each calls `DuplicateGroups.Clear()` and rebuilds all `DuplicateFile`/`DuplicateGroup` instances). Mark/unmark a row after the last search: exactly one row updates per mark and totals change by exactly one file's size — no duplicated/stale handler firing. The subscription is a weak event (`WeakEventManager<DeletionSelection, DeletionSelectionChangedEventArgs>`), so the discarded view-models from earlier searches are collectible and their handlers stop firing once collected, even though the engine-owned `DeletionSelection` lives on.
