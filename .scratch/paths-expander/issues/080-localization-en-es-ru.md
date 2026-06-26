# 080 — Localization (en/es/ru)

- **Status:** awaiting-verification
- **Plan step:** Step 8

## Goal
Add the four new UI strings (rail title, toggle tooltip, ceiling-reached tooltip, empty-state message) in English, Spanish, and Russian, following the existing folder-comparison string family.

## Satisfies
- User stories: **US-36**
- PRD sections: §"Localization"

## Exact files / classes / methods (copied from plan)
- **Add 4 keys** to `Resources.resx`, `Resources.en.resx`, `Resources.es.resx`, `Resources.ru.resx`, **and hand-add the four matching static accessors to `Resources.Designer.cs`** (this SDK-style project has no `ResXFileCodeGenerator`/`CustomTool`, so `dotnet build` does **not** regenerate the Designer — the existing `Ui_FolderComparison_*` accessors are maintained by hand; without the accessors the `x:Static` references will not compile). Follow the `Ui_FolderComparison_*` family:
  - `Ui_FolderComparison_Rail_Title` (rail header)
  - `Ui_ToolTip_FolderComparison_Rail_Toggle` (toggle tooltip)
  - `Ui_FolderComparison_Rail_LimitReached` (ceiling tooltip)
  - `Ui_FolderComparison_NoFoldersSelected` (empty-state message)
- **Change:** wire the four `{x:Static resx:Resources.…}` references into issues 030/040 (title/toggle), 060 (ceiling), 070 (empty-state).

## Depends-on
- none (leaf). Referenced by 030, 040, 060, 070 — do alongside; can land last. Those steps need the keys to compile.

## Manual verification (PRD scenarios)
- **scenario 13** — "…en / es / ru strings are present for all new UI text." (The long-path-clip portion of scenario 13 is issue 040.)

## Assumptions from plan
- **From the verification pass (now in the plan body):** the `Resources.Designer.cs` accessors must be added **by hand** — `dotnet build` will not regenerate them, and the `x:Static` references won't compile without them.

## Result

**Files changed:**
- `DuplicateFileTool/Properties/Resources.resx` — added 4 `<data>` entries (invariant/default English values).
- `DuplicateFileTool/Properties/Resources.en.resx` — added 4 `<data>` entries (English).
- `DuplicateFileTool/Properties/Resources.es.resx` — added 4 `<data>` entries (Spanish).
- `DuplicateFileTool/Properties/Resources.ru.resx` — added 4 `<data>` entries (Russian).
- `DuplicateFileTool/Properties/Resources.Designer.cs` — hand-added 4 matching `public static string` accessors, mirroring the existing `Ui_FolderComparison_*` pattern (same `ResourceManager.GetString(...)` shape + XML doc comment).

**Keys added (in all four resx + Designer):**

| Key | en | es | ru |
|---|---|---|---|
| `Ui_FolderComparison_Rail_Title` | Folders | Carpetas | Папки |
| `Ui_ToolTip_FolderComparison_Rail_Toggle` | Show or hide the folder list | Mostrar u ocultar la lista de carpetas | Показать или скрыть список папок |
| `Ui_FolderComparison_Rail_LimitReached` | Maximum number of folders selected | Número máximo de carpetas seleccionadas | Выбрано максимальное число папок |
| `Ui_FolderComparison_NoFoldersSelected` | No folders selected | No hay carpetas seleccionadas | Папки не выбраны |

**Build:** PASS — `dotnet build DuplicateFileTool/DuplicateFileTool.csproj` → 0 Errors, 1 Warning. The single warning (`CS0414: 'FolderComparison._suppressSelection' is assigned but its value is never used`) is **pre-existing and unrelated** to this issue (it comes from in-progress code in another step, not from any change here).

**Contradictions flagged:** none. No XAML was wired (correctly deferred to issues 030/040/060/070, which will reference these `x:Static` accessors).

### Correction (post-review): `Ui_FolderComparison_Rail_Title` removed

The rail switched to the standard `Expander ExpandDirection="Right"` (issue 030 correction), then the header title was dropped per user request (vertical rotated title not wanted). `Ui_FolderComparison_Rail_Title` ("Folders"/"Carpetas"/"Папки") is therefore **deleted** from all four resx files and its hand-added accessor removed from `Resources.Designer.cs`. **Three** keys remain in use: `Ui_ToolTip_FolderComparison_Rail_Toggle`, `Ui_FolderComparison_Rail_LimitReached`, `Ui_FolderComparison_NoFoldersSelected`. Build still PASS (0/0).

## Manual checks
- [ ] **PRD scenario 13 (localization portion):** en / es / ru strings are present for all new UI text — i.e. all four new keys (`Ui_FolderComparison_Rail_Title`, `Ui_ToolTip_FolderComparison_Rail_Toggle`, `Ui_FolderComparison_Rail_LimitReached`, `Ui_FolderComparison_NoFoldersSelected`) resolve to localized values when the app runs under each of the three cultures. (The long-path-clip portion of scenario 13 is issue 040.)
