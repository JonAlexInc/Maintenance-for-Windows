# Maintenance for Windows

Understand your computer. Review cleanup before it happens. Keep a clear record.

**0.01 Preview (`v0.0.1`) — local development build, not a production release.**
Windows 11 x64 only. This is independent software, not a Microsoft product.

Developed by **[JonAlexInc](https://github.com/JonAlexInc)**. The consumer app is **free for personal, non-commercial use**. Source is public for inspection and transparency, with reuse rights reserved; this is not an open-source project. See [consumer-use and source terms](LICENSE) and [third-party notices](THIRD-PARTY-NOTICES.md). A commercial Technician edition is future work, not part of this preview.

## Implemented in this preview

- Computer overview: Windows build, processor, installed memory, system-drive storage, collection timestamp.
- Dedicated temporary-file cleanup with a checklist of exact source paths, including user and Windows temp, optional crash reports, shader caches, and browser caches.
- File-by-file preview with full paths, folder grouping, sortable/resizable Name/Full path/Size/Modified/Created/Type columns, and expert skip reasons.
- Filter by name/path/extension, minimum/maximum MiB, or hide empty files. Filter changes clear selection; Select visible applies only to displayed results. Turn grouping off for a global sort. Double-click a row to inspect its complete path, or right-click to copy it.
- Explicitly confirmed permanent deletion of selected candidates, cancellation between files, execution-time checks.
- Exact duplicate finder: compare selected NTFS folders/drives by contents, review grouped copies, keep one and explicitly recycle others. Fixed local drives support recycling; USB is review-only. Search, paging, full-path export and local history are included.
- Persistent file/folder exclusions.
- Local activity history with scan and file-action tabs and previewable plain-text report export.
- Reports omit paths, user name, and machine name by default. Including paths is opt-in.
- Windows-style sidebar with overview cards, selection totals, and inline history preview.
- Light, Dark, and Follow Windows appearance, plus Blue, Teal, and Violet accents. Choices are saved locally.

Core local review requires no account, subscription, or telemetry service. Optional internet speed tests contact their displayed test servers. Startup requests optional administrator access; declining continues as a standard user with protected sources unavailable. Windows-managed cleanup (updates, Delivery Optimization, previous Windows, and Recycle Bin) opens Windows Settings. Startup inventory and supported confirmed third-party service/task changes are available with recorded previous settings; malware detection and technician licensing are **not implemented**.

## Run and build

Install a .NET 8 SDK on Windows 11 x64, then from this folder:

```powershell
dotnet run --project src/Maintenance.App -c Release
dotnet run --project tests/Maintenance.Tests -c Release
dotnet publish src/Maintenance.App -c Release -r win-x64 --self-contained false -o artifacts/preview
```

The published app requires the .NET 8 Windows Desktop Runtime (x64). Launch `MaintenanceForWindows.exe`; accept the optional Windows administrator prompt for protected sources, or decline to continue as a standard user. The local SDK under `.tools/`, if present, is a development convenience and is excluded from Git. No installer or signed release is provided yet.

## Cleanup policy

Cleanup scans only checked sources in Temporary-file cleanup mode. Current-account and Windows temporary folders are selected by default when permitted; diagnostic and cache sources are opt-in. Personal folders default to review-only; enable file and folder checkboxes to recycle selected files on fixed local drives. Candidates must have creation and last-write times at least seven days old. Age alone does not prove a file is disposable: review the list and close other apps first.

The engine rejects redirected directories, reparse points, system/read-only/offline files, and files with multiple hard links. It holds ancestor directory handles while resolving paths, opens the target without write/delete sharing, rechecks file identity, change time, age, size and exclusions, and deletes through that same handle. It never deletes directories. One Scan action supports an optional Max wait in seconds. With it off, scanning runs until completion or cancellation; timed and cancelled results are labeled partial. Max wait is checked between entries; an in-flight Windows call and rendering can take additional time.

Deletion bypasses the Recycle Bin and is **not undoable by this app**. Cancel stops remaining work, not completed deletions. Logical deleted bytes and observed free-space change are separate; neither is a guaranteed physical-space saving.

Intent is flushed to an activity journal before deletion, followed by the outcome. If logging fails, cleanup stops. An unmatched Pending entry after interruption is unknown, not a successful deletion.

## Local data and privacy

Exclusions and activity journals are stored under `%LOCALAPPDATA%\MaintenanceForWindows`. Journals contain file paths; treat them as private. Scan records contain aggregate counts; cleanup records contain per-file outcomes. New activity files are retained locally without the former 30-run purge; older already-purged records cannot be reconstructed. No encryption beyond Windows account/storage protections is added. Share the default redacted text export rather than raw journals.

## Product direction

The base app is intended to be free for personal use with free expert details. The planned **Maintenance for Windows — Technician Edition** adds professional workflows and a pay-once license per major version. Purchasing 1.x includes released 1.x updates; later major upgrades are optional. Pricing, support duration, publisher identity, and source licensing are undecided.

Future platform names: **Maintenance for Mac** and **Maintenance for Linux**. They are plans, not currently supported platforms.

See [V0.01 scope](docs/V0.01.md), [publication checklist](docs/PUBLISHING.md), [contribution guidance](CONTRIBUTING.md), and [security reporting](SECURITY.md).

## Licensing and trademarks

Free personal use and limited build/run permissions are described in [LICENSE](LICENSE). Broader modification, redistribution and commercial source-reuse rights are reserved. GitHub viewing/forking rights and third-party licenses still apply. See [licensing decisions](docs/LICENSING.md).

Windows is a trademark of the Microsoft group of companies. This project is not affiliated with, endorsed by, or sponsored by Microsoft. Product naming is not a claim of trademark clearance.

## Expanded overview and System

Overview shows live CPU use, used/total memory, and mounted-volume capacity bars. Storage categories are measured automatically and concurrently on ready local fixed/removable volumes; network/mapped drives are excluded. Categories cover Windows/system, applications, app data, personal files, temporary files, and other files. Measurements are bounded and explicitly labeled when partial. System is the last sidebar page, with hardware summary cards and expandable sections showing key fields first, optional full properties, and search across all inventory including motherboard, BIOS, memory modules, physical disks, partitions, devices and drivers. Services and startup inventory are excluded from System. Unsupported active memory timings/channel mode are explicitly unavailable. Windows inventory uses a bundled read-only PowerShell/CIM script and remains local.

Cleanup retains results until Clear results or another scan. Size labels use adaptive B/KB/MB/GB/TB with 1024-based units. Result colors supplement explicit text statuses.


The scan-location picker supports Temporary-file cleanup, Downloads, Desktop, and a chosen local subfolder. Personal-folder scans default to review-only. Enable file and folder checkboxes to send explicitly selected files to the Windows Recycle Bin. Temporary-file cleanup retains its separate eligibility rules and permanent-deletion confirmation. Review filters identify large files, files not modified in 90 days, and installers/archives without claiming they are safe to remove. Whole-drive scanning is no longer offered in the main picker. Columns fit content and headings after each new scan/result set, with horizontal scrolling for long paths.

Large results use 500-row pages. Sorting and filtering apply to the entire scan in the background. Folder totals describe the current page; changing pages clears selection, and Select visible affects only that page. Scan data remains in memory. Drive progress shows inspected entries, elapsed time, and current folder; Windows file calls can still delay cancellation until they return.


System now presents concise hardware cards on component tabs, including mouse and keyboard. Search finds hardware by model, brand, and displayed specification. Export full inventory saves all gathered properties as JSON, including identifiers; Services and Startup remain excluded. Paging provides Previous, Next, and direct page selection.


## Alternate data streams
Select **Alternate data streams (NTFS)** in Scan location, then **Choose drive**. Local/USB NTFS volumes and NTFS network shares are supported when Windows can verify named-stream support. Enter a UNC share when a mapped letter is unavailable in the elevated session. The scan is read-only and includes named streams on files and folders. Full stream paths identify the owning file plus `:stream:$DATA`; timestamps describe the owner. Search, size filters, sorting, pagination, cancellation and Max wait remain available. Inaccessible, redirected and offline items are reported as skipped. Ordinary download-origin metadata can be present; this scan does not classify malware or delete streams.

Storage analysis now runs to completion or cancellation, with live category totals and a gray remainder for used space unexplained by measured logical sizes. Game libraries use Steam/Epic metadata, GOG/Ubisoft installation records and known game folder layouts. Protected installs can remain unmeasured, and unknown game locations can remain in other categories. Cleanup sources use an expanding checkbox grid; hover a source for exact paths and cautions.



