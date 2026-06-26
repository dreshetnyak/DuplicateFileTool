# Folder-comparison control — issue index

Issues derived from `../implementation-plan.md` (which is grounded in `../PRD.md`, Status: ready-for-agent). Each `NNN-*.md` is self-contained: YAML front-matter (`id`, `title`, `phase`, `status`, `depends_on`, `touches_files`, `user_stories`) + Description / Acceptance criteria / Manual verification.

**Foundational rule:** issue **001** (the path-keyed deletion-selection set) is the seam almost everything else depends on. It runs **alone first**, must be `done` and confirmed to build before any UI or pipeline issue starts.

**Execution mode (per current instruction):** sequential, single working tree, one sub-agent per issue, in dependency order. The wave map below is a *dependency guide only*; the flattened queue under it is the actual execution order.

## Issue table

| id | title | phase | depends_on | status |
|----|-------|-------|------------|--------|
| 001 | Path-keyed deletion-selection set | P1 Engine selection model | — | **done** (a36fc29) |
| 002 | Group-membership index + zero-survivor query | P1 Engine selection model | 001 | **done** (c902f26) |
| 003 | `IsMarkedForDeletion` as view over set | P1 Engine selection model | 001 | **done** (fbfe0a9) |
| 004 | Route totals through set; retire delta plumbing | P1 Engine selection model | 001, 003 | **done** (369303d) |
| 005 | Global Reset clears whole set | P1 Engine selection model | 001, 004 | **done** (9ea5901) |
| 006 | Remove all-marked-group auto-unmark guard | P2 Deletion pipeline | 003 | **done** (f18c6ad) |
| 007 | Delete from unified set (dup + non-dup) | P2 Deletion pipeline | 001, 003, 006 | **done** (45952b0) |
| 008 | Remove emptied folders | P2 Deletion pipeline | 007, 012 | **done** (19b24f2) |
| 009 | Delete-enabled gate (post-run refresh → 020) | P2 Deletion pipeline | 004, 007, 018 | **done** (d0466af) |
| 010 | `FolderItem` VM (lazy load, icons, sort, reparse skip) | P3 FolderItem VM | — | **done** (5deedfe) |
| 011 | `FolderItem` set-backed mark + binary folder state | P3 FolderItem VM | 001, 010 | todo |
| 012 | Eager background subtree enumeration on folder mark | P3 FolderItem VM | 001, 010, 011 | **done** (4fd25ab) |
| 013 | Belonging + zero-survivor flags on `FolderItem` | P3 FolderItem VM | 002, 010 | **done** (70b0420) |
| 014 | Render `FolderItem` in `TreeListView` (columns + mark button) | P4 Folder tree rendering | 010, 011 | **done** (39a6ab3) |
| 015 | Row backgrounds — red zero-survivor, belonging, precedence | P4 Folder tree rendering | 013, 014 | **done** (12c4240) |
| 016 | Folder sub-control — header + tree + warning line | P5 Folder sub-control | 013, 014, 015 | **done** (01dd998) |
| 017 | Per-folder clear button (recursive) | P5 Folder sub-control | 001, 012, 016 | **done** (in 01dd998) |
| 018 | `FolderComparison` container — distinct folders, splitters, h-scroll | P6 Container & placement | 013, 016 | **done** (3b77e7a) |
| 019 | Place control in Results tab (temporary bottom region) | P6 Container & placement | 018 | **done** (a155324) |
| 020 | `CurrentGroup` driven by results-tree selection | P7 Current-group binding | 002, 018 | **done** (885232f) |
| 021 | Outer sub-control background for selected results row | P7 Current-group binding | 020 | **done** (385cdb4) |
| 022 | Busy indication during eager subtree scan | P8 Busy indication | 012, 018 | **done** (86c38ba) |
| 023 | Localize new strings (en/es/ru) | P9 Cross-cutting | 016, 018 | **done** (ee3669b) |
| 024 | Reuse existing icon assets | P9 Cross-cutting | 014, 016 | **done** (in 39a6ab3/01dd998) |
| 025 | Version bump (4 tracked places) → 2.4.0 | P9 Cross-cutting | 009, 019, 021, 022, 023, 024 | **on hold** (awaiting runtime verification) |
| 026 | Changelog entry (WIP → Unreleased gating) | P9 Cross-cutting | 009, 019, 021, 022, 023, 024 | **done** (91c7256, WIP section) |

## Parallelization map (waves)

Each wave contains only issues whose `depends_on` are all satisfied by earlier waves **and** whose `touches_files` don't overlap any sibling in the same wave. (Useful if you ever switch to parallel execution; otherwise it's the dependency skeleton.)

| Wave | Issues | Why this wave |
|------|--------|---------------|
| 1 | **001** | Foundational seam, runs alone before anything else. |
| 2 | 002, 010 | Both need only 001 (010 needs nothing); distinct files (`DuplicatesEngine.cs` vs new `FolderItem.cs`). |
| 3 | 003, 011 | 003←001 (engine), 011←001,010 (`FolderItem.cs`); no file overlap. |
| 4 | 004, 006, 012, 014 | 004←003, 006←003, 012←011, 014←011; touch `DuplicatesEngine.cs`+cmds / `DuplicatesRemover.cs` / `FolderItem.cs` / `FolderTree.xaml` resp. — disjoint. |
| 5 | 005, 007, 013 | 005←004, 007←006, 013←002,010; disjoint files. |
| 6 | 008, 015 | 008←007, 015←013,014; disjoint (`DuplicatesRemover.cs`+`FileSystem.cs` vs `FolderTree.xaml`). |
| 7 | 016 | ←013,014,015; first to touch the sub-control files. |
| 8 | 017, 018, 024 | 017←016 (`FolderComparisonItem.xaml.cs`), 018←016 (new `FolderComparison.*`), 024←014,016 (`FolderTree.xaml`+`FolderComparisonItem.xaml`); disjoint. |
| 9 | 009, 019, 023 | 009←004,007,018, 019←018, 023←016,018; disjoint (`DeleteMarkedFilesCommand.cs`+`MainViewModel.cs` / `MainWindow.xaml` / `Resources*.resx`). |
| 10 | 020 | ←002,018; touches `MainViewModel.cs`+`DuplicatesEngine.cs`+`FolderComparison.xaml.cs`. |
| 11 | 021 | ←020; touches `FolderComparison.xaml.cs`+`FolderComparisonItem.xaml`. |
| 12 | 022 | ←012,018; deferred by `MainViewModel.cs`/`FolderComparison.xaml.cs` collisions with 009/020/021. |
| 13 | 025, 026 | Release gate; need the full feature surface done. Disjoint (version files vs `Changes.md`). |

## Flattened execution queue (sequential order)

This respects every `depends_on`; it is the order to work the issues one at a time:

```
001 → 002 → 010 → 003 → 011 → 004 → 006 → 012 → 014 → 005 → 007 → 013
→ 008 → 015 → 016 → 017 → 018 → 024 → 009 → 019 → 023 → 020 → 021 → 022
→ 025 → 026
```

## Open questions — RESOLVED (maintainer chose recommended defaults)

All six are decided and baked into the named issue files:

- **OQ-1** (issue **008**, populated by **012**) — track explicitly-selected directory paths in a small parallel set (sibling to the file-path set) to force their removal; the `RemoveEmptyDirectories` setting still governs the dup-only flow. *This adds 012 → 008 as a dependency.*
- **OQ-4** (issue **008**) — add a reparse-point guard to `FileSystem.IsDirectoryTreeEmpty`/`DeleteEmptySubDirectories` so removal can't traverse a junction.
- **OQ-5** (issue **012**) — cancel a folder's in-flight scan on unmark/group-change; apply set updates atomically per folder.
- **OQ-2** (issue **022**) — lightweight per-column busy/disabled overlay on the scanning sub-control.
- **OQ-3** (issue **018**) — fixed sensible min column width, equal-ish default sizing, no column-width persistence this iteration.
- **OQ-6** (issues **009 / 020**) — after a run, rebuild from the still-selected results row; placeholder if it was deleted.
