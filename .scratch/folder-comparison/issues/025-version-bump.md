---
id: 025
title: Version bump in all four tracked places
phase: Phase 9 — Cross-cutting
status: todo
depends_on: [009, 019, 021, 022, 023, 024]
touches_files:
  - DuplicateFileTool/DuplicateFileTool.csproj
  - DuplicateFileTool/Properties/AssemblyInfo.cs
  - DuplicateFileToolInstaller/Package.wxs
  - README.md
user_stories: []
---

> **ON HOLD (maintainer decision 2026-06-24):** all other issues are done and the build is clean, but the feature has only been build/code-review verified — not run. Per the maintainer, version stays at the current **2.3.0** and the changelog entry stays in **Work in Progress** until the 10 manual scenarios are verified at runtime. On confirmation: bump all four places to **2.4.0** and promote the changelog (WIP → dated `### DuplicateFileTool 2.4.0: <date>` Released block, issue 026).

## Description
Bump the version in all **four** tracked places, kept in sync (per CLAUDE.md "Versioning"):
1. `DuplicateFileTool/DuplicateFileTool.csproj` — `<ApplicationVersion>`
2. `DuplicateFileTool/Properties/AssemblyInfo.cs` — `[assembly: AssemblyVersion(...)]` (runtime source of truth)
3. `DuplicateFileToolInstaller/Package.wxs` — the `<Package Version="..." ...>` attribute
4. `README.md` — the shields.io version badge

Current version is 2.2.0 (per git history); this feature ships as a `New.` minor bump — confirm the exact target version with the maintainer before editing. Do this only when the feature is otherwise complete.

## Acceptance criteria
- All four versions match the chosen target.
- `ConfigManager.GetAppName()` (reads `AssemblyVersion`, formats `Major.Minor.Build`) shows the new version in the app's title/about.

## Manual verification
Build and run → the displayed version reflects the bump. Diff the four files → identical version. Build the installer project → its package version matches.
