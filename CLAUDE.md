# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

DuplicateFileTool is a WPF desktop application (.NET 10, Windows) for finding and deleting duplicate files. It uses MVVM architecture and Win32 P/Invoke for high-performance filesystem enumeration.

## Build & Run Commands

```powershell
# Build the solution
dotnet build DuplicateFileTool.sln

# Run the application
dotnet run --project DuplicateFileTool/DuplicateFileTool.csproj

# Publish as a self-contained single-file executable (win-x64, outputs to DuplicateFileTool/bin/Publish/)
dotnet publish DuplicateFileTool/DuplicateFileTool.csproj /p:PublishProfile=FolderProfile

# Build the WiX installer (incomplete migration — see Installer section)
dotnet build DuplicateFileToolInstaller/DuplicateFileToolInstaller.wixproj
```

There are no test projects in this solution.

## Versioning

The project follows semantic versioning (since 2.0.0). The version number is stored in **three** places that must be kept in sync when releasing:

1. `DuplicateFileTool/DuplicateFileTool.csproj` — `<ApplicationVersion>` (e.g. `2.0.1.%2a`)
2. `DuplicateFileTool/Properties/AssemblyInfo.cs` — `[assembly: AssemblyVersion("2.0.1")]` (this is the runtime source of truth)
3. `DuplicateFileToolInstaller/Package.wxs` — the `<Package Version="2.0.1" ...>` attribute (drives installer upgrade logic)

`Configuration/ConfigManager.GetAppName()` reads the assembly version at runtime and formats it as `Major.Minor.Build` for display (the patch number lives in the Build component). The human-readable changelog is `DuplicateFileTool/Changes.md` (Markdown) — completed-but-unreleased changes go as bullets under the `## Unreleased` section; on release, move them into a new `### DuplicateFileTool <version>: <date>` block under the `## Released` section. The file also holds the feature `## Backlog` and a `## Work in Progress` section. Entries are bullets prefixed with `New.` / `Bug.` / `Fix.` / `Improvement.`

## Solution Structure

- **DuplicateFileTool/** — main WPF application (the only project that matters day-to-day)
- **DuplicateFileToolInstaller/** — WiX v4 installer, unfinished migration from WiX v3; currently has an `OutputName` bug set to `IDTechTimeSync` instead of `DuplicateFileTool`

## Core Architecture

### Duplicate-Finding Pipeline

The engine runs files through a four-stage pipeline, all orchestrated by `DuplicatesEngine.cs`:

1. **`FilesSearch`** — recursively enumerates directories via Win32 `FindFirstFile`/`FindNextFile` P/Invoke (not .NET `Directory.Enumerate*`). Groups work by physical drive for parallel processing. Produces `FileData` objects.

2. **`DuplicateCandidates`** — filters down to candidate groups using an `ICandidatePredicate`. The current predicate (`ComparableFileHash.CandidatePredicate`) groups files by equal size.

3. **`DuplicatesSearch`** — compares candidates with the active `IFileComparer`, fires `DuplicatesGroupFound` events for confirmed matches. Tracks `MatchResult` scores (0 = complete mismatch, 10000 = complete match).

4. **`DuplicatesRemover`** — deletes marked files asynchronously, optionally to the recycle bin, optionally removes empty parent directories after deletion.

`DuplicatesEngine` exposes observable properties (`DuplicateGroups`, counts, sizes, errors) consumed directly by the ViewModel.

### File Comparison System

The comparison system is pluggable via three interfaces:
- `IFileComparer` — top-level comparer registered with a GUID and human-readable name
- `ICandidatePredicate` — determines which files are worth comparing (pre-filter)
- `IComparableFile` / `IComparableFileFactory` — implements the actual `CompareTo()` logic

The only implemented comparer is `ComparableFileHash` (MD5 chunk hashing, default chunk size 65535 bytes). Adding a new comparer means implementing all three interfaces and registering the comparer in `Configuration`.

### MVVM Layer

**`MainViewModel`** is the single top-level ViewModel. It holds:
- `DuplicatesEngine` — search/deletion orchestrator
- `SearchPaths` — include/exclude path list
- `DuplicateGroupsProxyView` — `ObservableCollectionProxy<DuplicateGroup>` that wraps `DuplicatesEngine.DuplicateGroups` and adds pagination, filtering, and sorting
- `Config` — the root `Configuration` object

**Commands** (`Commands/`) all derive from `CommandBase` which implements `ICommand` + `ICancellable`. Long-running commands (`FindDuplicatesCommand`, `DeleteMarkedFilesCommand`, `AutoSelectByPathCommand`) are async with cancellation support.

**`ObservableCollectionProxy<T>`** (`ObservableCollectionProxy.cs`) wraps `ObservableCollection<T>` to add client-side pagination (`CurrentPage`, `ItemsPerPage`, `TotalPages`), filtering via `IInclusionPredicate<T>`, and sorting via `IComparer<T>`. Results filtering and pagination in the UI flow entirely through this class.

### Configuration System

`Configuration` (root) → `SearchConfiguration`, `ResultsConfiguration`, `ExtensionsConfiguration`, `ProgramConfiguration`

Each setting is a `ConfigurationProperty<T>` with validation rules, a localizable display name/description, and `INotifyPropertyChanged`. The whole graph is serialized/deserialized as JSON to `%APPDATA%` via `FileAppConfig`.

### Win32 / Low-Level Layer

- `DirectoryEnumeration.cs` — `IEnumerator<FileData>` implemented directly over `FindFirstFile`/`FindNextFile`; used instead of `Directory.EnumerateFiles` for performance
- `FileReader.cs` — sequential chunk reader with cached handles, used by `ComparableFileHash`
- `FileSystem.cs` — wrapper for delete, empty-tree detection, and symbolic-link handling
- `Win32.cs` — all P/Invoke declarations and Win32 structs/constants

### Localization

Three cultures: `en` (default), `es`, `ru` via satellite assemblies. Culture is set at startup from `ProgramConfiguration.SelectedCulture` before any UI is constructed. All UI strings go through resource files; `CommandBase` and `DuplicatesEngine` also use localized strings for status messages.

## Key Patterns

- **`NotifyPropertyChanged`** base class (uses `[CallerMemberName]`) — used by nearly every ViewModel and model class; also includes `PropertiesChangeTracker<T>` for chaining nested property-change notifications.
- **`ConfigurationProperty<T>`** — every persisted setting is wrapped in this type; gives uniform validation, binding, and serialization without boilerplate.
- **`FileData`** is an immutable record populated from Win32 data at enumeration time; it is never mutated after creation.
- `DuplicateGroup` and `DuplicateFile` are the UI-facing wrappers around search results, adding `IsMarkedForDeletion`, `IsSelected`, and computed size properties consumed by the Results view.
