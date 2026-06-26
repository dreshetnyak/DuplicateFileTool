---
id: 023
title: Localize every new UI string (en/es/ru)
phase: Phase 9 — Cross-cutting
status: done
depends_on: [016, 018]
touches_files:
  - DuplicateFileTool/Properties/Resources.resx
  - DuplicateFileTool/Properties/Resources.en.resx
  - DuplicateFileTool/Properties/Resources.es.resx
  - DuplicateFileTool/Properties/Resources.ru.resx
  - DuplicateFileTool/Properties/Resources.Designer.cs
  - DuplicateFileTool/Controls/FolderComparisonItem.xaml
  - DuplicateFileTool/Controls/FolderComparison.xaml
user_stories: []
---

## Description
Add resource entries for every new UI string in all three cultures (`en` default, `es`, `ru`): the folder-path header, the zero-survivor warning text, mark/clear button tooltips, and the empty/placeholder text. No hard-coded literals in the new XAML/code.

Note: confirm the exact resource file names/paths in `DuplicateFileTool/Properties/` before editing (the three satellite-culture resource files). Adjust `touches_files` to the real names if they differ.

Grounding: project uses `en`/`es`/`ru` satellite assemblies; all UI strings go through resource files (CLAUDE.md "Localization").

## Acceptance criteria
- Switching culture shows translated strings for all new UI (header, warning, tooltips, placeholder).
- No hard-coded user-facing literals in the new control/sub-control/tree.

## Manual verification
Run the app under each culture (via the program's culture setting) → the folder-comparison header, warning, tooltips, and placeholder appear translated in es and ru, English by default.

## Manual verification performed
- Added three keys to all four resx files (`Resources.resx`, `Resources.en.resx`, `Resources.es.resx`, `Resources.ru.resx`) with culture-appropriate values: `Ui_FolderComparison_ZeroSurvivor_Warning`, `Ui_ToolTip_Clear_Folder_Selection`, `Ui_FolderComparison_Placeholder`. Confirmed 3 occurrences per resx and 6 in the designer (doc comment + getter per key).
- Added matching strongly-typed properties to `Resources.Designer.cs` mirroring the existing pattern.
- Swapped the three literals in `Controls/FolderComparisonItem.xaml` (warning text, clear-button tooltip) and `Controls/FolderComparison.xaml` (placeholder) to `{x:Static resx:Resources.*}`; added the `resx` namespace where missing; removed the TODO markers. The mark/unmark tooltips were left on their existing keys.
- All four resx files remain UTF-8 (with BOM); Russian/Spanish text preserved.
- Build: `dotnet build DuplicateFileTool.csproj -nologo -clp:ErrorsOnly` → Build succeeded, 0 Warning(s), 0 Error(s). The successful build confirms the `x:Static` references resolve to the new designer properties.
