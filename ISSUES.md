# Issues

Issue IDs are permanent. Status values: `Open`, `In Progress`, `Resolved`, `Won't Fix`.

## I01 — Search follows directory reparse points

- Status: Resolved
- Severity: Critical
- Evidence: `DuplicateFileTool/FilesSearch.cs:51-60,119-138`; `DuplicateFileTool/FileSystem.cs:325-340`
- Problem: Search recursively enters junctions and directory symlinks. A target directory and its alias can expose the same physical file as two duplicates; deleting either path can remove the only file. Cycles and out-of-root scans are also possible.
- Resolution: Reparse-point include roots are skipped, and directory reparse points discovered during enumeration are never entered. Verified with alias, cycle, out-of-root junction, and directory symlink cases.

## I02 — Hash fragment count overflow creates false duplicates

- Status: Resolved
- Severity: Critical
- Evidence: `DuplicateFileTool/FileComparer.cs:56-68`; `DuplicateFileTool/Comparers/ComparableFileHash.cs:71-88,125-131`; `DuplicateFileTool/DuplicatesEngine.cs:672-684,757-790`; `DuplicateFileTool/MainViewModel.cs:350-370`
- Problem: The required fragment count was narrowed from `long` to `int`. At 2,147,483,648 fragments it became negative, so comparison read no bytes and returned a complete match for arbitrary equal-sized files. The first unsupported size is `(int.MaxValue × chunk size) + 1`: 140,735,340,806,146 bytes with the default 65,535-byte chunk, or 1,099,511,627,265 bytes with the minimum 512-byte chunk. This was reproduced with unequal 1 TiB sparse files. XxHash128 collision hardening is a separate probabilistic concern; no collision was demonstrated by this issue.
- Resolution: Files exceeding the exact limit for the configured chunk size are excluded before candidate grouping. The UI shows one aggregate warning with the skipped count and, when representable, the minimum required chunk size. Fragment-count conversion is also checked so bypassing the filter fails instead of producing a false match. Verified both exact boundaries and unequal sparse files; no oversized file was reported as a duplicate.

## I03 — Deletion does not revalidate searched files

- Status: Won't Fix
- Severity: Medium
- Evidence: `DuplicateFileTool/DuplicatesRemover.cs:128-133,168-205`; `DuplicateFileTool/FileSystem.cs:259-271`
- Problem: Confirmed: deletion uses search-time `FileData` and deletes the current object at that path. If a selected file or its surviving copy changes after search, the old duplicate classification can be stale and unique content can be deleted. A full-engine reproduction deleted a same-size unique replacement and reported normal success.
- Resolution: Accepted risk. Search results are a snapshot; users are responsible for rerunning the search after external file changes. A reliable guarantee would require handle-bound content revalidation and deletion plus redesign of the path-based Recycle Bin workflow. Cheap metadata checks remain bypassable and racy, so their limited protection does not justify the added behavior and complexity.

## I04 — Ignored last survivor is retried

- Status: Resolved
- Severity: Medium
- Evidence: `DuplicateFileTool/DuplicatesRemover.cs:80-88,99-163`
- Problem: Confirmed with narrower prerequisites than originally claimed. When every copy was marked, an ignored last survivor remained selected while its collapsed one-file group was removed. Pass 2 then classified the path as a nonduplicate and retried it, allowing the final copy to be deleted. Recycle Bin mode is required for Ignore and is disabled by default.
- Resolution: Each deletion run records normalized duplicate paths attempted in pass 1. Pass 2 excludes those paths even if their live group has collapsed, while still processing genuine nonduplicate selections. Verified with two fully marked long-path copies: ignoring the survivor prompted once, left it selected and on disk, and did not retry it.

## I05 — Auto-select can widen selected folder to its parent

- Status: Resolved
- Severity: Medium
- Evidence: `DuplicateFileTool/Commands/AutoSelectByPathCommand.cs:132-169`
- Problem: Confirmed with narrower impact than originally claimed. A selected directory carrying Archive was treated as a file and replaced with its parent, so duplicate copies in sibling folders could be marked. The survivor guard still retained an unmarked copy, only known duplicates were affected, and deletion required a separate user action.
- Resolution: Path classification now tests the Directory attribute and moves to the parent only for files. Verified with Archive-set and normal directories, Archive-set and Archive-cleared files, sibling folders, and the final-copy safeguard.

## I06 — Installed application cannot persist settings

- Status: Resolved
- Severity: Medium
- Evidence: `DuplicateFileTool/Configuration/SettingsStore.cs:16-354`; `DuplicateFileTool/Configuration/ConfigManager.cs:12-108`; `DuplicateFileTool/App.xaml.cs:28-90`; `DuplicateFileTool/MainWindow.xaml.cs:19-85`
- Problem: Confirmed for a standard, non-elevated per-machine installation: application-level configuration targeted a file beside the executable under Program Files, where Users have read/execute access but not write access. Saving threw an access error, and the window-close handler swallowed it. The 20 values handled by this persistence path—including language, search/results settings, extension-catalog text, and column widths—were lost; comparer-specific settings such as hash chunk size were never part of this path. Elevated and writable portable deployments were unaffected.
- Resolution: Replaced executable-side application configuration with a typed, versioned JSON document at `%LOCALAPPDATA%\DuplicateFileTool\settings.json`. When JSON is absent, known values from a discoverable legacy `.dll.config` or `.exe.config` are loaded and migrated on close. Saves stage the complete document, flush it, and atomically replace the prior file. Malformed JSON is quarantined; unreadable or newer-schema files are preserved and cannot be overwritten. Load/save failures are shown to the user. Verified typed round trips under a non-English culture, all 20 mappings, legacy migration and JSON precedence, corrupt-file recovery, newer-schema and transient-read protection, atomic failure recovery, startup culture, Release build, publish, and installer build. The historical x86-to-x64 installer cleanup deleted the old x86 config before this migration existed, so that already-removed file cannot be recovered.

## I07 — Invalid configuration values are accepted

- Status: Resolved
- Severity: Medium
- Evidence: `DuplicateFileTool/ConfigurationProperty.cs:18-62,99-207`; `DuplicateFileTool/Controls/ConfigGrid.xaml:35-53`; `DuplicateFileTool/Configuration/UserSettings.cs:13-79`; `DuplicateFileTool/ObservableCollectionProxy.cs:27-38`
- Problem: Confirmed, with narrower impact than claimed. Semantically invalid but parseable numbers were stored before validation, while the validity flags had no consumers. `ItemsPerPage=0` persisted and, after restart, the first included result divided by zero and shut down the application. The integer validator also rejected every boxed `long`, including valid Min/Max defaults. Malformed and overflowing `int` text was already rejected by WPF conversion; recovery was straightforward and no file data was at risk, so High severity was overstated.
- Resolution: Configuration values are validated before assignment and retain the last valid value on failure. WPF validation now displays range and conversion errors; the integral validator supports both `int` and `long`. Invalid persisted fields fall back individually to defaults, preserve valid siblings, show one warning, and cause corrected settings to be rewritten. Persisted column widths require positive finite values, and pagination independently rejects nonpositive page sizes. Verified zero, negative, both integer overflows, malformed input, valid `long` values beyond `int.MaxValue`, per-field JSON recovery, width bounds, and the first-result path.

## I08 — Deleting all groups from a later page can crash

- Status: Resolved
- Severity: Medium
- Evidence: `DuplicateFileTool/ObservableCollectionProxy.cs:154-188,202-227,432-469`; `DuplicateFileTool/App.xaml.cs:100-143`
- Problem: Confirmed with narrower prerequisites than claimed. When a bulk deletion removed every filtered group while the user was on page 3 or later, the zero-page restore path retained the old page, selection, and navigation state. Clicking Previous then requested page 2 or later from an empty list, calculated a negative item count, raised `OverflowException`, and caused the application to show its fatal-error dialog and shut down. Pages 1 and 2 retained invalid page state but did not trigger this exception; deletion had already completed and no file corruption was demonstrated, so High severity was overstated.
- Resolution: The zero-page bulk restore now performs the established complete target reset: displayed items and selection are cleared, page state becomes `0 / 0`, and collection, count, and navigation notifications are raised. Loading page zero also clears selection. Verified all-empty bulk removal from pages 1, 2, and 3, safe navigation afterward, partial deletion with a surviving anchor, and non-bulk removal of the final item.

## I09 — Filtered results retain removed or stale groups

- Status: Resolved
- Severity: Medium
- Evidence: `DuplicateFileTool/ObservableCollectionProxy.cs:95-116,152-202,444-478`; `DuplicateFileTool/ResultsGroupInclusionPredicate.cs:29-43`; `DuplicateFileTool/DuplicateGroupComparer.cs:40-94`
- Problem: Confirmed. With nonempty result keywords, child files changed before a collapsed group was removed from the source. The removal handler re-ran the predicate against that mutated group; because filtered groups require two accepted files, the predicate returned false and the handler retained a zero- or one-file ghost in the proxy. Groups that survived deletion were not re-filtered, and mutable Size and sometimes Name/Path sort keys were not re-sorted. The default Number sort was unaffected. Filter changes repaired the view, and no crash or unintended file mutation was demonstrated, so Medium severity remains appropriate.
- Resolution: Source removals now use prior proxy membership rather than re-running the mutable predicate. When a bulk deletion finishes, the proxy rebuilds once from the live source, applies the current filter and comparer, recalculates pagination, clears an excluded selection, and restores the captured page anchor. Verified Include and Exclude filters, collapsed and surviving groups, Size/Name/Path/Number ordering, pagination and selection, no-filter deletion, zero-result reset, and surviving-anchor restoration.

## I10 — Search path ancestry uses raw string prefixes

- Status: Resolved
- Severity: Medium
- Evidence: `DuplicateFileTool/PathComparison.cs:1-25`; `DuplicateFileTool/FilesSearch.cs:18-148`; `DuplicateFileTool/FileSearchInclusionPredicate.cs:8-24`
- Problem: Confirmed. Raw prefix tests treated sibling paths such as `C:\foo` and `C:\foobar` as ancestor and descendant, silently dropping valid include roots or over-applying exclusions. Starting include roots and files were not checked against exclusions, so a root beneath an excluded parent leaked its direct files while its subdirectories were skipped. The ticket also missed case/separator normalization, reference-based duplicate pruning, case-sensitive drive matching, and the same prefix defect in default OS-folder exclusion. No false duplicate classification, automatic deletion, or data loss was demonstrated, so Medium severity remains appropriate.
- Resolution: Search paths are normalized once, deduplicated case-insensitively, and compared by equality or directory-segment boundaries. Exclusions take precedence and are applied to starting roots and every enumerated file or directory. Drive roots are matched case-insensitively, and OS-folder exclusion uses the same path comparison. Verified sibling and nested roots, root/file exclusions, case and mixed/trailing separators, duplicate rules, volume-root and UNC comparisons, reparse-point behavior, and OS-prefix siblings.

## I11 — Cancelled folder scan can leave permanent busy overlay

- Status: Resolved
- Severity: Low
- Evidence: `DuplicateFileTool/FolderItem.cs:259-326`
- Problem: Confirmed, but narrower than claimed. If a scan was cancelled after being queued with its token but before its delegate started, the task became cancelled without entering the delegate or its `finally`. Its busy count remained elevated, its current column stayed input-blocked, and its CTS was not disposed. Cancellation after the delegate started was already balanced; removing and recreating the column, switching groups, or restarting the application recovered the UI. No partial selection, file deletion, or data damage occurred, so Medium severity was overstated.
- Resolution: Folder scans are queued without using their cancellation token as the task-scheduling token. Cancellation remains enforced at the start of traversal, throughout enumeration, and immediately before commit, ensuring even a pre-cancelled worker enters the existing `finally`, balances its busy count, and disposes its CTS. Verified forced pre-start cancellation under thread-pool starvation, restart after cancellation, normal completion, and cancellation after worker entry.

## I12 — Persisted extension catalog is inert

- Status: Resolved
- Severity: Low
- Evidence: `DuplicateFileTool/Configuration/ExtensionsConfiguration.cs:16-24,120-215`; `DuplicateFileTool/Configuration/UserSettings.cs:75-79`; `DuplicateFileTool/AddOrRemoveExtensionsViewModel.cs:72-83`
- Problem: Confirmed, but persistence itself worked. The runtime catalog was built from defaults before the persisted catalog string was assigned, and later string changes never rebuilt it. Consequently, valid loaded and live-edited catalogs remained visible and saveable but the Add/Remove Extensions categories continued using defaults. Direct search-extension entry and filtering still worked, and no crash, incorrect duplicate classification, file mutation, or data loss occurred, so Medium severity was overstated.
- Resolution: The validated catalog string is now the single mutable source. Every accepted load, edit, or reset is fully parsed before replacing the contents of a stable read-only runtime catalog. Invalid live values retain the previous catalog and show validation feedback; invalid persisted catalogs reset to the default through the existing warning-and-rewrite path. Verified persistence and restart, live edits, reset, empty and malformed catalogs, type classification, and category selection in the Add/Remove dialog.

## I13 — File reads are globally serialized

- Status: Resolved
- Severity: Low
- Evidence: `DuplicateFileTool/FileReader.cs:14-159`; `DuplicateFileTool/DuplicatesSearch.cs:69-130,283-287`; `DuplicateFileTool/Comparers/ComparableFileHash.cs:159-176`
- Problem: Confirmed, but limited to synchronous comparison-content reads. The process-wide upgradeable cache lock spanned each `ReadFile`, allowing only one application read at a time and suppressing the I/O overlap intended for SSD and separate-drive lanes. Hashing and other comparison work remained parallel, and a single-HDD lane was already limited to one worker. Results and file safety were unaffected, so Medium severity was overstated; the device-dependent slowdown was not benchmarked.
- Resolution: Each reader now serializes its own operations, while cache membership and active-handle leases use a short global lock. Reads run outside that lock; leased handles cannot be evicted, and an opener at the configured handle limit waits for a lease to return. Reopen resumes the saved offset, and seek and disposal use the same protocol. Verified two independent reads overlap, concurrent calls on one reader remain sequential, four concurrent readers complete with a one-handle limit, offsets survive repeated eviction, and disposal cannot close an active handle. Debug and Release builds completed without warnings or errors; physical multi-drive speedup remains unmeasured.

## I14 — Search runs with exclusion-only paths

- Status: Resolved
- Severity: Low
- Evidence: `DuplicateFileTool/MainViewModel.cs:384-425`; `DuplicateFileTool/UiSwitch.cs:87-114`; `DuplicateFileTool/MainWindow.xaml:260-266,544`; `DuplicateFileTool/Commands/FindDuplicatesCommand.cs:19-29`; `DuplicateFileTool/FilesSearch.cs:45-62`; `DuplicateFileTool/DuplicatesEngine.cs:661-674`
- Problem: Confirmed, but the ticket overstated both wording and severity. `SearchPath` raised inclusion-type notifications; search-button eligibility ignored them and accepted any active path. An exclusion-only run therefore cleared derived results before the scanner found zero include roots. Files were never modified, so the impact was recoverable result and scan-time loss rather than data loss.
- Resolution: Search-button eligibility now requires an active include and is recomputed on active/type changes without stacking UI disable requests. Clear Paths has independent collection-based state. A command-layer guard also rejects exclusion-only execution before results can be cleared. Focused state, direct-command, and build verification completed.

## I15 — Failed deletion can clear read-only state

- Status: Resolved
- Severity: Low
- Evidence: `DuplicateFileTool/FileSystem.cs:259-278,318-340`; `DuplicateFileTool/DuplicatesRemover.cs:172-224`
- Problem: Confirmed, but narrower than claimed. A failed recycle or permanent deletion retained the file after clearing its ReadOnly attribute. Recycle Ignore and Cancel were affected only after an attempted recycle; preflight decisions occurred before any attribute change. Runtime reproduction changed `ReadOnly, Archive` to `Archive`, without deleting or altering file contents, so Medium severity was overstated.
- Resolution: Each deletion attempt now records whether it actually cleared the live ReadOnly bit. If deletion fails, it restores only that bit against the file's current attributes before propagating the original error, covering Ignore, Cancel, and failed permanent fallback without overwriting unrelated attribute changes. Verified failed recycle and permanent deletion, successful deletion, writable files, stale search-time attributes, and preservation of other attributes.

## I16 — Gigabyte display uses megabyte divisor

- Status: Resolved
- Severity: Low
- Evidence: `DuplicateFileTool/Converters/DataConversion.cs:5-17`
- Problem: Confirmed, with narrower impact than the raw output suggests. Values at or above the binary-gigabyte threshold were divided by the binary-megabyte divisor, so 1 GiB displayed as `1,024.00 GB` in English culture and all such sizes were overstated by roughly 1,024 times. The defect affected presentation only; search, sorting, comparison, and deletion continued using numeric byte counts. The binary divisors with `KB`/`MB`/`GB` labels are an existing application-wide convention, not a gigabyte-only defect.
- Resolution: The gigabyte branch now divides by `gigabyteSize` using decimal arithmetic, correcting the scale while retaining fractional values such as `1.50 GB`. Verified byte, KiB, MiB, and GiB boundaries plus a fractional-GiB value.
