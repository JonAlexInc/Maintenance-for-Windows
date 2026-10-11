# Changelog

## 0.0.1 — unreleased preview

UI revision: Windows-style sidebar and overview cards; clearer cleanup actions and selected-file totals; readable history labels and inline report previews. Adds persistent Light/Dark/Follow Windows themes and Blue/Teal/Violet accents.

Initial Windows 11 x64 implementation: overview, conservative user-temp scan, selected cleanup, persistent exclusions, local activity journals, and redacted support reports. Adds fixture-based safety checks and a Windows build workflow. No public release, installer, malware scanning, or Technician edition yet.

File-browser revision: deferred appearance-menu disposal to avoid disposing items during click dispatch. Adds exact paths, folder groups, sortable/resizable columns, text/size/empty-file filters, and copy-path actions. Raises default scan coverage to 200,000 inspected entries, with an explicit up-to-1-million option.


System/overview revision: larger starting window, live CPU and memory readings, per-volume capacity bars, bounded read-only storage category analysis, and a searchable/category-filtered Windows hardware/firmware/device/driver/service inventory. Retains cleanup rows with colored outcomes and Clear results; adds focused totals, adaptive B/KB/MB/GB/TB sizes (1024-based), and list double buffering.


Organized preview: hardware summary cards and expandable System sections with searchable full details; cleanup columns fit content by default. Replaces dual scan buttons with optional Max wait. Adds local-drive review-only scan picker. Overview automatically analyzes local drives concurrently with six folder/type categories; excludes network drives.


Friendly System revision: removes Services/Startup from both display and collection. Uses readable field/component names and units, suppresses placeholders/duplicate fields/low-level mappings in the default overview, and retains advanced hardware properties behind Technical details. Search still covers advanced fields.


Large-scan revision: results are paged in sets of 500, with sorting/filtering prepared off the UI thread. Filters search the complete scan. Drive progress is throttled with elapsed time/current folder; skipped-detail display is bounded to 1,000 entries. Clear results cancels pending view preparation.


Focused cleanup and hardware tabs: main scan locations are temporary files, Downloads, Desktop, and a chosen local subfolder. Personal files remain review-only with size/age/installer review filters and explicit non-safety recommendations. Whole-drive choices removed; adds dedicated paging controls and direct page jump. System uses CPU/motherboard/graphics/memory/storage/mouse/keyboard/network/audio/display/Windows/other tabs with compact cards. Complete gathered inventory exports to JSON; no raw-property tables in the main interface.


### Device identity and speed tests preview
- System now uses local bus-reported peripheral names, clearly labels parent/receiver names, and displays USB vendor/product identifiers. A receiver name does not establish the exact mouse or keyboard model.
- Offline PCI ID lookups identify GPU chip vendor/model and board vendor/subsystem where the database has a match. PCI database attribution and BSD license ship alongside the app. No device IDs are sent online.
- Speed tests sidebar: local disk and arbitrary writable network/mapped folder sequential file write/read tests, selectable 256 MB or 1 GB, cancellation, own-file cleanup, session results. Uses exclusive CreateNew files; never writes raw disks. Cached reads are explicitly labeled. Failed cleanup reports the exact remaining test-file path.
- Internet download targets: Hetzner Ashburn, Hillsboro, Germany, Finland and Singapore; explicit 100 MB single-connection downloads to memory, no upload measurement. Includes connection setup in throughput timing. Third-party target sees client IP; target and data amount displayed before starting. No automatic tests. Five-minute cancellation limit; file cancellation waits for pending OS operations.

Speed-folder fix: local drive selection now prefers an existing per-user temp/data folder on the SAME volume, avoiding protected C:\ writes without elevation. Other drives retain explicit folder selection; access denied gives actionable guidance. Folder picker uses a two-column layout; result cards have explicit equal columns. Cached reads are labeled on cards and session history. Release build plus smoke checks cover same-volume default, generated-file testing/cancellation and all pages/themes.

### Disk benchmark suite
- Replaces the cached single-file measurement for local and network folders with Microsoft DiskSpd 2.3 (MIT license bundled). Uses file-only targets, -Sh unbuffered/write-through, 16 MiB random write buffers, and a fully initialized random-data test file. No raw disks, privileges, or automatic tests.
- Four workloads: sequential 1 MiB Q8T1/Q1T1 and random 4 KiB Q32T1/Q1T1; whole suite or individual workload. Select 1–9 passes, 3/5/10/30 seconds per phase, and 64 MiB through 64 GiB files. One-second warm-up before each phase. Means of completed passes with MB/s, GB/s, IOPS and average microsecond latency. CSV exports individual passes and run context.
- Test file size is working-set size, not total bytes written; timed workloads may write many times that amount. Preparation is excluded. Device/server caches may still influence results. Results are not promised to equal CrystalDiskMark. Internet remains a separate bounded download test.
- Native cancellation kills the owned engine process and waits for exit before deleting only the generated file. Local/mapped paths check free space with 256 MiB reserve. UNC capacity/quota failures are reported as I/O errors. A two-hour suite timeout prevents indefinite testing; network I/O may delay cancellation/cleanup.

Benchmark appearance: replaced the grid with four rounded workload cards, large read/write numbers, teal/indigo metric labels, completed-pass captions and active-workload highlighting. Cards scroll at small sizes and support light/dark colors. Setup notes stay enabled/readable while interactive settings are locked during a run. Unit switching remains available during testing. Reduced session-history footprint and emphasized the Run benchmark button. Benchmark engine and measurement settings unchanged.

Network/internet revision: explicit Choose network folder button with UNC entry and mapped-drive selector. Network mode starts with an empty folder instead of retaining the prior local drive. Internet test now uses OVHcloud endpoints (Virginia, Oregon, London, France, Singapore, Sydney), after reproducing premature response termination from Hetzner. Certificate validation remains enabled; no HTTP downgrade. Connection failures include the target hostname and underlying cause. Internet view has one download card and a shorter settings area. Disk result cards retain compact fixed row heights instead of expanding with window height.

Hardware and storage detail revision:
- Inputs group by Windows device container (parent path fallback); composite keyboard mouse interfaces appear with the keyboard, and recognizable virtual input interfaces move to Other hardware. Bluetooth parent-friendly names are used before generic bus descriptors.
- GPU headings combine known PCI board vendor with the detected graphics processor, without guessing an ASUS submodel. Active EDID monitor inventory detects both displays on the validation machine.
- Adds DIMM occupancy and firmware ceiling, richer CPU cache/virtualization and module fields. Exact-model, source-linked catalog cards cover the detected i9-13900KF, Z790-H board and Corsair CMH32GX5M2X7200C34 kit. This is an initial catalog, not universal CPU/board coverage. Manufacturer specifications dated 10 Oct 2026 are separate from live settings; active timings, live PCIe negotiation and active memory-channel mode are not measured. Raw and presented facts remain in export.
- Storage adds Games, Media, Documents, Development, Archives/images and game extras/cache. Steam library metadata and Epic manifests provide custom roots; XboxGames/GOG Games/EA Games layouts and file extensions are heuristics. Categories are exclusive and estimated, never cleanup authorization. Existing partial-analysis and skipped-file limits remain.

Cleanup source expansion and optional elevation:
- Normal launch requests UAC elevation; declining continues as a standard user. --standard-user skips the launch request. The account used by the running process is displayed with the source checklist; alternate admin credentials use that account's profile.
- Temporary-file cleanup is a dedicated dropdown mode with checkbox sources and exact paths. Current-user and Windows Temp default on when available to the access level. Browser disk/code/GPU caches, DirectX/NVIDIA/AMD shader caches, crash dumps and archived WER reports are opt-in. No arbitrary folder becomes eligible through this picker. Browser caches require closing the browser; age still must exceed seven days.
- Windows Update, Delivery Optimization, Windows.old and Recycle Bin remain managed through Windows Storage settings, linked from the source panel. No raw deletion or service stopping for these stores.
- A frozen multi-root cleanup engine handles only the checked sources. Changing a source clears old results. Existing per-file identity, timestamps, hardlink, reparse, exclusion, locking, cancellation and intent-journal checks remain. No directories are removed. Global max-wait applies across sources; multi-root free-space delta is reported unknown.

### NTFS stream discovery and clearer scan layout
- Read-only Alternate data streams scan mode, with local/removable/mapped drive selection and UNC share entry. Filesystem and named-stream support are verified before scanning.
- Stream path, name, logical size and owning-file timestamps; folder streams included. Existing filtering, sorting, paging, cancellation and Max wait apply. Skips are reported.
- All cleanup sources shown in a responsive, non-scrolling checklist grid. Source paths and cautions are available on hover.
- Custom folder selection lives in the scan dropdown; predefined locations have no redundant folder button.
- Folder group headings use a tinted band, accent stripe and bold text.
- Storage analysis continues beyond the former 200,000-entry / 20-second cutoff, shows live totals and unexplained space, and can be stopped. More game library metadata sources are recognized.
- Personal-folder scans now offer opt-in file selection and Recycle selected, with confirmation, revalidation, exclusions and journaled outcomes. Named streams remain read-only.
- Optional checkboxes in highlighted folder headers select only files currently displayed in that group; filters and pagination never silently select hidden files.
- History uses a resizable side-by-side full-height list and report preview, with distinct activity labels for personal scans, stream scans and recycling.

### Detailed History
- History now sits directly above System, after Speed tests.
- All activity, Scans, Deleted & recycled, and Skipped & incomplete tabs, with search and sortable file/action columns.
- Exact file paths, known logical sizes, timestamps, reasons, and selected-record details. Pending entries collapse into their final outcomes; incomplete requests stay Unknown.
- Separate permanent-deletion and recycle totals, plus CSV export of the visible tab (includes full paths).
- New records capture per-event time and scan locations. Future journals are retained without the previous 30-record purge.

Modern activity revision: permanent bottom activity strip with scan/test details, animated activity indicator, theme-aware colors and full hover details. Removes overview CPU/system-drive detail lines; marks the Windows volume on its storage card. Replaces remaining native app-data and stream-viewer tabs with rounded navigation, softens cards and grid borders, and applies native surface themes broadly.

Speed revision: Clouvider Amsterdam and GARR Rome alternatives, polished animated gauges, Average labels, and persistent speed-test history including each disk pass and internet direction. Internet directions fail independently. Public-server requests use bounded chunks, pacing and at most two rate-limit retries honoring Retry-After (up to two minutes per cooldown); failures remain explicitly incomplete. Intentional pacing/cooldown is excluded from transfer averages; request overhead remains included. Public services may still refuse tests.

Startup & automation: separate sidebar page with Startup apps, Services and Scheduled tasks tabs; searchable read-only inventory, descriptions/accounts/dependencies/actions/triggers/run information, executable version and Authenticode inspection. Explicit confirmed disable and restoration for supported third-party service/task configurations, with local intent/backup records and revalidation. Windows-protected items and startup-app toggles open built-in Windows tools. Does not stop running work.

Unused apps initial report: configurable 30-day to five-year interval (90-day default), evidence/coverage separation, optional local foreground observation, estimated registered sizes, keep/exclude and Windows app management. No supported inactivity recommendation without adequate coverage; old or missing evidence is not treated as proof of non-use.

Startup publisher revision: Hide Microsoft items filter across all three tabs, visible/hidden counts, sortable Publisher and What it does columns, and publisher/explanation search. Collects local executable company/description metadata; svchost services use their ServiceDll when available. Shared script/command hosts are not assumed to own their payloads. Microsoft task-folder classification is explicitly labeled in details. Unknown publishers remain visible. Common Windows services have plain-English explanations; supplied descriptions and configuration summaries retain their source labels. No disable/restore behavior changed.

Exact duplicate finder: dedicated sidebar page with multiple local/USB NTFS locations, Downloads/Desktop shortcuts, staged content comparison, grouped search and sorting, complete pagination, an explicit kept copy, reviewed fixed-drive recycling, persistent scan/action history and CSV export. Files with named streams and hard links are omitted, along with protected/reparse/offline/read-only/empty files. Cancellation and the 250,000-entry bound report partial coverage. Tested with generated files, including actual Windows Recycle Bin recovery.

Subfolder and UI polish revision: duplicate scans now traverse read-only folders and recognized locally available OneDrive/cloud folders while rejecting real redirects and download-required placeholders. Synced copies remain review-only. Separate file/folder counts and helpful empty states explain scans that return no files; clear-results resets status, and unavailable duplicate actions remain disabled. Storage cards offer scan coverage with reason counts and bounded example paths, plus responsive legends. Cleanup and Speed tests retain access to all controls on short windows through whole-page scrolling. Speed setup text/estimated duration resize to their contents. Hardware cards no longer leave a horizontal scrollbar after shrinking; cache sizes/CPU labels are clearer. History uses the recorded activity type. Themed grid corners and neutral headers remove legacy visual remnants.

### App data map
- Added a searchable read-only map of current-account Local, Roaming and LocalLow data under Apps & leftovers.
- Shows measured size, file counts, exact folder paths, coverage and evidence from accessible running executable paths or registered install locations. Name-only matches and unknown ownership are clearly separated.
- Added report export, folder opening, cancellation with partial results, bounded enumeration and activity history.
- Added fixture coverage for recursive sizes, association boundaries, unknown ownership, cancellation, limits, unavailable roots and junction omission.

### Review navigation, evidence and compact visuals
- Result tables fit content after searches and refreshes, wrap long cells with expanded row heights, retain user-resized columns, and offer Fit all columns. File/review lists size against contents and headers. Hardware cards measure wrapped specification rows.
- Simplified the personal-folder menu to one folder chooser. Cache review has a contextual Back to Apps & leftovers action that restores the original tab/results, including App data map.
- App data mapping measures cache candidates, logs/diagnostics, temporary candidates, program files and settings/other data separately. Added bounded first-run evidence collection from current-user Store packages, uninstall records, App Paths, accessible processes, Run keys, services and scheduled task actions.
- Added explained app-specific assessments for Visual Studio Code, Discord/Discord PTB and Notepad++. Supported layouts require marker evidence and completed checks before a qualified Likely leftovers result; shared, partial and unknown cases remain review-only. Cache review is explicit and separate from settings; no whole-app removal recommendation.
- Added compact composition/outcome strips for app data, duplicates, cleanup, history, startup and usage, relative numeric bars in result tables, and CPU-core/DIMM occupancy miniatures. Existing drive category bars and speed graphs remain.
- Usage review defaults to all recorded apps and supplements historical evidence with accessible running processes; missing history remains unknown.

Workflow controls revision: scan actions use a consistent green Start search / Start test button with an adjacent red Cancel control. App-data, hardware, history, duplicate and VM filters live on their own labeled rows with rounded search fields. Cleanup start/cancel and Max wait now precede the source list; app-review tabs share the same action position. Button heights are standardized and narrow layouts wrap instead of clipping controls.

Shared result context menus offer Open item, Open folder and Copy full path for resolvable filesystem results, target the right-clicked row, and preserve the column-fit command. Deleted history entries can still open their existing parent folder. NTFS stream results resolve to the owning file rather than launching stream data.

Cancellation now interrupts app-map ownership evidence, startup inventory, restore-point queries and hardware inventory. Folder scans and app usage keep partial measurements. Single-response Windows inventory queries retain previously loaded results and explicitly explain that no partial response was available.

Universal stop and focused review revision: the bottom activity bar now exposes Stop all from every page. It cancels cleanup/duplicate/VM/stream scans, storage analysis, hardware/startup/restore inventory, speed tests, pending result preparation and app-owned Windows helpers. Foreground usage observation also stops. Pending I/O or an already-issued Windows mutation must settle; completed outcomes and partial results remain. Restore cleanup checks cancellation between removals, and stopped startup changes do not automatically launch another inventory query.

Scan coverage is a compact details link on each drive card.

Expand results opens a maximized focused window with a shared filter, relevant review/selection commands, export and Stop all. It is also available in result context menus. Closing returns the original live control and preserves its results and filter; duplicate groups and copies expand together. New rows arriving while a filtered list is expanded replace its older pool instead of restoring stale results.

All activity now searches values throughout recorded journal entries, including speed-test destinations and nested scan details. Speed activity labels show their recorded destination, direction and outcome.

Compact cleanup controls: result filters collapse by default, with an active-filter count and accent indicator. Selection commands, folder grouping, file selection and expert details share a wrapping toolbar. Expanded search, size, empty-file and review filters share one wrapping panel; Reset filters clears every narrowing criterion. Scan-type changes preserve the expanded/collapsed state.

