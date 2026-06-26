---
id: 022
title: Surface a busy state during eager subtree enumeration
phase: Phase 8 — Busy indication
status: done
depends_on: [012, 018]
touches_files:
  - DuplicateFileTool/FolderItem.cs
  - DuplicateFileTool/Controls/FolderComparisonItem.xaml
  - DuplicateFileTool/Controls/FolderComparisonItem.xaml.cs
user_stories: [20]
---

> **Implementation note (orchestrator):** OQ-2 DECIDED — per-column overlay. The eager scan (012) runs on the marked directory `FolderItem`, which is a SUBFOLDER node inside a column's tree (the column root isn't markable). So the column-level busy state = "any node in this column's subtree is scanning." Implement via an active-scan counter on `FolderItem` propagated to ancestors:
> - In `FolderItem`, add `IsScanBusy` (bool, raises PropertyChanged). Maintain a private `_subtreeScanCount`; add `BeginScan()`/`EndScan()` that increment/decrement `_subtreeScanCount` on THIS node and walk the `Parent` chain doing the same, raising `PropertyChanged(IsScanBusy)` on any node whose count crosses 0↔1. `IsScanBusy => _subtreeScanCount > 0`.
> - In `StartMarkScan` (012): call `BeginScan()` when the scan starts (on the UI thread, in the setter) and `EndScan()` when it finishes/cancels/commits. `EndScan` touches PropertyChanged (UI-bound) → marshal it to the UI thread (it already commits via `Application.Current.Dispatcher`; call `EndScan` there and on the cancel path via the dispatcher too). Keep it balanced (exactly one EndScan per BeginScan, including the cancelled/exception paths).
> - In `FolderComparisonItem.xaml`, add an overlay (a `Border` over the tree area, semi-transparent dim background, `IsHitTestVisible=True` to block input, containing a small centered indeterminate `ProgressBar` — NO text, so no new localized string) whose `Visibility` is bound to the root's `IsScanBusy` (DataContext is the root `FolderItem`, so `{Binding IsScanBusy}` works), collapsed when not busy.
> `FolderComparison.xaml.cs`/`MainViewModel.cs` are NOT needed (per-column, root-driven).

## Description
Show a clear busy indication while the eager subtree enumeration (012) scans a large folder after it is marked, so the user knows the app is working and not frozen.

**OQ-2 — DECIDED:** a lightweight **per-column busy/disabled overlay** on the sub-control being scanned (the column shows a busy state and ignores input until its scan completes). No dependence on the shared search/delete `ProgressText`/taskbar.

Grounding: scan is triggered in 012 (`FolderItem`); the column belongs to a `FolderComparisonItem` inside `FolderComparison` (018); shared progress fields live on `MainViewModel`/engine.

## Acceptance criteria
- Marking a large folder shows a clear busy cue until the scan completes.
- The UI does not appear frozen during the scan (work stays off the UI thread, per 012).
- The busy cue clears when the scan finishes.

## Manual verification
PRD scenario 4: mark a folder with a large subtree → a busy indication appears while it scans and clears when totals settle; the window stays responsive throughout.

## Manual verification performed
- Build: `dotnet build DuplicateFileTool/DuplicateFileTool.csproj -nologo -clp:ErrorsOnly` → Build succeeded, 0 Warning(s), 0 Error(s).
- Reasoned through Begin/End pairing across success/cancel/exception paths (no host runtime test available):
  - `BeginScan()` is called once, synchronously on the UI thread, in `StartMarkScan` right before `Task.Run`.
  - A single `EndScanOnce()` closure (guarded by a `bool ended`, mutated only on the UI thread) is invoked in the `Task.Run` `finally` via `Application.Current?.Dispatcher.Invoke`, so it runs on the UI thread on EVERY exit path: normal commit, `OperationCanceledException` (cancelled walk), and any other exception. Exactly one EndScan per BeginScan.
  - Immediate-cancel case: even if the prior scan's `CancelPendingScan()` cancels this token instantly, the body still enters `try`/`finally`, so the `finally` still fires `EndScanOnce` once.
  - Re-toggle case: each `StartMarkScan` owns its own `BeginScan`/`EndScanOnce` lifecycle (not done in `CancelPendingScan`), so a new scan cancelling a prior one still leaves each Begin paired with exactly one End.
- `BeginScan`/`EndScan` walk this node + every `Parent` ancestor; PropertyChanged(IsScanBusy) raised only on 0↔1 transitions; `EndScan` guards against a negative count (`if count == 0 continue`).
- Overlay: a `Border` (last child of the content `Grid`, `Grid.Row=0 Grid.RowSpan=3`) with dim `#80F0F0F0`, `IsHitTestVisible=True`, containing a centered 120x14 indeterminate `ProgressBar`, no text. Visibility bound to root `FolderItem.IsScanBusy` via the existing `BooleanToVisibilityConverter` with `ConverterParameter=Collapse`. `OuterBorder` (021), header, tree, and warning are untouched.
