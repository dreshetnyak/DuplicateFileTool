# Duplicate File Tool

A fast, Windows desktop application for finding and removing duplicate files. It scans the folders you choose, identifies files whose contents are truly identical, and lets you review and delete the redundant copies, reclaiming disk space.

![Platform](https://img.shields.io/badge/platform-Windows-blue)
![.NET](https://img.shields.io/badge/.NET-10-512BD4)
![License](https://img.shields.io/badge/license-GPLv3-green)
![Version](https://img.shields.io/badge/version-2.4.0-informational)

## How it works

The engine runs a four-stage pipeline:

1. **Search** — recursively enumerate the selected directories via Win32 P/Invoke, grouped by physical drive for parallelism.
2. **Candidates** — pre-filter the files into candidate groups (by equal size) so only plausible matches are compared.
3. **Compare** — hash and compare the candidates, emitting confirmed duplicate groups as they are found.
4. **Remove** — delete the files you marked and optionally remove the now-empty folders.
