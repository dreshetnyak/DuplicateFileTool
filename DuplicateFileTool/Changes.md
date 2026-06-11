# DuplicateFileTool Changes

## Backlog

- New. Add reset configuration to defaults button.
- New. Search paths list add switches that allow to turn off or no the path for the current search.
- New. Add quick filter for the file tree.
- New. Show folders of duplicated groups with folders comparing, should appear below the list of results.
- New. Add selected file preview. Characteristics of images.
- New. If a duplicate file is found in a folder add all files from that folder to the search. That should be an optional behavior.

- Move clear all results button to the right panel, or should we put it to the toolbar?
- Can we add columns to the files list, to indicate mod. date, size, etc?
- Under the files tree add a panel where we can see free space on the disks.

## Work in Progress

## Unreleased

## Released

### DuplicateFileTool 2.2.0: 2026-06-11

- New. The results list is now a tree-table with Name, Size and Last Modified columns. Group rows show the group caption and the duplicated size, file rows show the file size and the culture-formatted last write time, and the marked-for-deletion styling covers the whole row. Columns are resizable and their widths are remembered between runs. Long paths are clipped at the column edge with the full path shown in a tooltip.

### DuplicateFileTool 2.1.0: 2026-06-11

- New. Implemented the "Delete to recycle bin" setting that previously existed but had no effect. Files are moved to the recycle bin like Windows Explorer does. When a file cannot be recycled (the path is too long, the location is a network share without a recycle bin, or the operation fails), the program asks whether to delete the file permanently, leave it in place, or cancel the deletion, with an option to apply the choice to all remaining files in the run. Empty directories are always removed permanently since they contain no data to restore.
- New. Added a search setting that excludes zero size files, enabled by default. Empty files contain no data to compare, so reporting them as duplicates is not meaningful.
- New. The file tree did not reflect changes made on disk after directories were expanded. Now the expanded directories are refreshed automatically after a deletion run completes and when the application window is activated, and can be refreshed manually with F5 while the tree is focused. The expansion and selection state survives the refresh.
- Bug. The file tree initialization subscribed an item-selection handler every time it ran, so repeated initializations would accumulate duplicate handlers. The handler is now subscribed once.
- Improvement. The duplicate candidate selection compared every file against every other file, making its cost grow quadratically with the number of files. Comparers can now supply a grouping strategy (the hash comparer groups by file size in a single pass), reducing the candidates stage from minutes to seconds on large data sets.
- Improvement. The comparison stage processed candidate groups one at a time regardless of where the files reside. Now every physical drive gets its own read lane: drives with a seek penalty (HDDs) and drives whose kind cannot be determined are still read by one group at a time, SSDs admit several groups in parallel, and separate drives are always processed concurrently. A quadratic rescan during duplicate group assembly was removed.
- Improvement. File content fragments are now fingerprinted with XxHash128 instead of MD5, using thread-safe one-shot hashing. The hash result size and the collision safety stay practically the same, but the hashing itself is an order of magnitude faster and no longer caps the read speed of fast SSDs.
- Improvement. The progress text and counters were pushed to the UI for every processed file, flooding the UI thread with updates faster than a human can read them and slowing the search down. The status text and counters are now refreshed at most every 250 ms, the graphic progress bar every 100 ms to stay smooth, and the final values are always shown.
- Bug. The expanded/collapsed state of result groups was tied to the position on the page instead of the group itself, so after a page change unrelated groups appeared collapsed. The state now belongs to each group.
- Bug. The results filter text box grew with the entered text, pushing the paging controls out of view. The filter is now fixed-width and the window cannot shrink below the width the results toolbar needs.

### DuplicateFileTool 2.0.1: 2026-06-04

- Bug. The application crashed with an EntryPointNotFoundException when deleting selected files.
- Bug. File deletion did not apply the long-path prefix, so deleting a file whose path exceeds MAX_PATH could fail.
- New. Added a global unhandled-exception handler.

### DuplicateFileTool 2.0.0: 2026-06-04

- The project has been upgraded to .NET 10.
- Converted the project to the modern SDK-style format.
- The code has been refactored to use the new language features and reduce SAST issues.
- Migrated the installer from WiX v3 to v4.
- Changed the project versioning to follow the semantic versioning.
- Bug. The directory enumerator now throws a clear error when the current item is accessed before the enumeration has started.

### DuplicateFileTool 1.2.974: 2022-12-29

- New. Added a filter for the results, now you can enter keywords to filter results and adjust the filter in different ways.
- Bug. Deleting files is extremely slow if the current page is not the first page. Now we switch to the first page before starting deletion.

### DuplicateFileTool 1.1.925: 2022-06-10

- Bug. When deleting duplicated the progress text line would wraps and change the hight in some cases.
- Bug. If deleting a duplicate file and the file is opened by another process the deletion would fail, the error will be added to the errors list, but the error icon would not lit up, also the path of the error would be empty (added to the error message).

### DuplicateFileTool 1.0.915: 2022-04-28

- Bug. Commented out some debug code that was forgotten in the release build.

### DuplicateFileTool 1.0.911: 2022-04-28

- Fix. Improvements and optimizations.

### DuplicateFileTool 1.0.844: 2022-01-18

- New. Created the installer project.

### DuplicateFileTool 1.0.844: 2022-01-10

- Bug. Results page right side bar buttons is enabbled during deletion.

### DuplicateFileTool 1.0.822: 2022-01-09

- New. The icons on the buttons on the results tab should be grayed out when disabled.
- Bug. Clear results button is enabled at start when the results list is empty.
- Bug. After finishing a search the search path list stays read-only.

### DuplicateFileTool 1.0.816: 2022-01-07

- Fix. Changed the way how the controls is enabled/disabled.
- Bug. Settings is enabled while the search is in progress, should be disabled.
- Bug. While an automatic selection is in progress the interface entry was enabled.
- Bug. Auto select by path progress and final text was not localized.
- Fix. Sorting by ascending/descending arrow need to be grayed out when the feature is disabled during search.
- Bug. Results sorting order button tooltip was not set untill it's value changed.

### DuplicateFileTool 1.0.787: 2021-12-15

- Fix. Removed NuGet packages that was actully not used to reduce dependencies and the total size.
- Fix. Optimized some texts.
- Fix. Allow to select multiple rows on the errors page.
- Bug. Error messages on the errors page is not localized.
- Bug. Include/Exclude icon does not change in the search paths list when the value changes if the culture is not english.
- Bug. List of errors does not have the localized table header.
- Bug. Types of the errors on the Errors page is not localized.
- New. The files in the groups is not sorted, need to sort it by path for it to be consistent over all groups.
- Bug. Types of the file size entered is not localized.
- Fix. Select by path in a case if there is a lot of groups is quite slow, program freezes during it. Added selection progress indication, made it async.

### DuplicateFileTool 1.0.753: 2021-12-14

- Bug. Corrected 5 typos in the texts.
- Fix. Changed layout in about the program section.
- New. Added localizations for Spanish and Russian languages, added option to select the language in the configuration.

### DuplicateFileTool 1.0.700: 2021-11-22

- Fix. Some code refactoring.

### DuplicateFileTool 1.0.694: 2021-11-19

- New. Implement right click on file tree to open in file explorer.
- New. Open file on double click in results and in the file tree.
- New. Setting page now contain the settings that is propagated to the configuration file.

### DuplicateFileTool 1.0.520: 2021-11-09

- Bug. When the search is in progress the button to clean search paths list is enabled and functional.

### DuplicateFileTool 1.0.496: 2021-10-26

- Bug. Results page navigation buttons is active diring search, but the navigation is not allowed.
- Bug. When the files in the file tree was sorted by the name the there was a upper/lower case distinction, so the upper case was going first and lower at the end of the list.

### DuplicateFileTool 1.0.492: 2021-10-21

- New. Added a dialog that can add or remove search extensions in a bulk based on the extensions type.
- New. Search extensions list now has the column indicating the extension type.
- New. Now the Search and the cancel search buttons icons is gray when the buttons is not active.
- Bug. Disk labels was missing in the files tree.
- Bug. It was impossible to add search extensions manually to the list.
- Bug. Exception was thrown when trying to edit a search extension type by double clicking on it.
- New. Results page navigation icons replaced.
- New. Results page navigation added the buttons for the first and the last pages.
- Fix. Changed the row height in the search estensions list.
- Fix. On the results page reduced the height of the toolbar.
- Fix. Sort order now goes as text from the resources instead of as enum id.
- Fix. Search paths include/exclude text should be in the resources.
- Fix. Search paths table headers was hardcoded, now taken from the resources.
- New. Search paths list add icons to include/exclude.

### DuplicateFileTool 1.0.436: 2021-10-08

- New. Add a timestamp column to the errors list.
- New. Add a context menu to clear the errors list.
- New. Add button to clear search paths list.
- New. Add button to clear extensions list.

### DuplicateFileTool 1.0.395: 2021-10-07

- Fix. Disabled debug code.

### DuplicateFileTool 1.0.394: 2021-09-20

- Minor optimization to finalize the merge of two commits.

### DuplicateFileTool 1.0.392: 2021-09-20

- Bug. When auto selecting a folder if there is another folder with a different name that starts the same as selected the other folder also gets selected.
- Bug. After deletion the empty directories has not been removed despite the settings.

### DuplicateFileTool 1.0.368: 2021-09-20

- Bug. After deletion the empty directories has not been removed despite the settings.
- Minor code optimizations.

### DuplicateFileTool 1.0.364: 2021-09-17

- Bug. Results configuration was not saved to the app config.
- Bug. Loading configuration was throwing exceptions for some types.
- Bug. Results configuration was not propagated to configuration.
- Bug. When loading config we set HasChanged was set for all properties.
- Bug. If a search path was deleted from the list and it is equal to the one that is selected the selected path is not re-enabled.
- Bug. When switching to a new results page the page is not scrolled to the top.
- Fix. Set the minimum width for the sort order combo box, so it doesn’t change when different values is selected.
- Bug. If clean all results is performed that does not clean the status bar messages.
- Bug. Auto select by path results in exception if no path is selected.
- Bug. After selecting files in all groups by using AutoSelectByPath when the files is located on different disks the delete selected files and clear selection buttons becomes disabled.
- Bug. Auto select by path is enabled when there is no selected path.
- Bug. Unhandled exception was occurring on deletion in cases when there was a deletion error.
- Bug. Unable to delete read-only files resulting in access denied error.
- Bug. When deleting files the status bar message does not show that file that is been deleted.
- Bug. When deleting files the progress text and percentage was not updated.
- Bug. When comparing files some of the progress messages has full file names some short file names. The names was displayed twice.

### DuplicateFileTool 1.0.268: 2021-08-12

- Working on the implementation of the filtering and paging class.

### DuplicateFileTool 1.0.261: 2021-08-04

- Results sorting menu is now disabled during duplicates search.
- Minor refactoring, getting ready to implement sorting.

### DuplicateFileTool 1.0.259: 2021-08-03

- Added tabs icons.
- Corrected the application title name.
- Reset errors when search restarts.
- Added settings tab with about program info.
- Search size entry is now validated, non-digits not allowed.
- Clear selection now is disabled when nothing is selected.
- Results tab buttons is now disabled during a search.
- Delete selected files now is disabled when no files is selected.
- Select by path is now disabled when the results window is empty.
- Moved Minimum/Maximum labels into the resources.
- Bug. Duplicates Group number is not incremented, it is always zero.
- Auto select by path was not browsing to the path of the currently selected file.
- Added clear results button.
- Added sorting menu, but not yet wired it.
