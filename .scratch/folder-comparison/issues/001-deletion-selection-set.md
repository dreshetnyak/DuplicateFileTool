---
id: 001
title: Add a path-keyed deletion-selection set to the engine
phase: Phase 1 — Engine selection model
status: done
depends_on: []
touches_files:
  - DuplicateFileTool/DeletionSelection.cs
  - DuplicateFileTool/DuplicatesEngine.cs
user_stories: [17, 18]
---

## Description
Introduce an engine-owned selection service (new file, name e.g. `DeletionSelection.cs`) keyed by **normalized full file path**. Each entry stores the path's size. Expose `Add(path, size)`, `Remove(path)`, `Contains(path)`, `Clear()`, a running `Count`, a running total `Size`, and a change notification (per-path and/or batched) that views subscribe to. `DuplicatesEngine` owns one instance and exposes it.

This is the foundational seam (plan §"Foundational seam first"). It must be **pure logic with no WPF references** so it stays verifiable without the UI. Path normalization must be consistent (case-insensitive on Windows, long-path aware) so the same file marked from two views resolves to one key.

Grounding: `DuplicatesEngine` lives in `DuplicateFileTool/DuplicatesEngine.cs` (engine class lines 179–568). `FileData` exposes the path and size used as the key/value.

## Acceptance criteria
- Adding a path updates `Count` and total `Size`; removing it reverses both exactly.
- Adding an already-present path is a no-op — no double count.
- `Clear()` empties the set; `Count` → 0 and `Size` → 0.
- Subscribers receive a change signal identifying the affected path(s).
- No `System.Windows`/WPF references in the new type.
- **Existing behavior unchanged:** the solution still builds and the current results-tree marking still works (this task only adds the set; wiring comes in 003/004).

## Manual verification
Build the solution. Run the app, find duplicates — existing marking/totals must behave exactly as before (the set is not yet wired into the UI). No PRD scenario is directly observable yet; this is the substrate for all later issues.

## Manual verification performed
- Build: `dotnet build DuplicateFileTool/DuplicateFileTool.csproj -nologo -clp:ErrorsOnly` → `Build succeeded. 0 Warning(s) 0 Error(s)`.
- The set is not yet wired into the UI or any command, so existing results-tree marking/totals behavior is byte-for-byte unchanged (only a new file was added and an unreferenced read-only property was added to the engine).
- Logic inspection (no test project exists): `Add(path,size)` does `_entries.TryAdd` then `_totalSize += size` only on success, so Count/Size rise by exactly one entry/size; a second `Add` of the same (case-insensitively normalized) path returns false and changes nothing (no double count). `Remove` does `_entries.Remove(key, out size)` then `_totalSize -= size`, reversing the add exactly; removing an absent path is a no-op. `Clear()` empties `_entries` and zeroes `_totalSize`, raising one `Reset` signal. Each successful add/remove raises `Changed` with the affected normalized path; `Clear` raises `Changed` with `Reset`/null. A human can confirm by temporarily adding two paths (one twice) and a third, then removing/clearing, and observing Count/Size via the debugger.
