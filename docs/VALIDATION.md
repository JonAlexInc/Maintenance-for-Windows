# Local validation — 0.0.1 preview

Performed October 10, 2026 on Windows 11 x64 (build 26300), using a workspace-local .NET SDK 8.0.425.

## Passed

- Release build/publish with warnings treated as errors: zero warnings and errors.
- 20 generated-fixture assertions: age filter, scan non-mutation, fresh-file preservation, sibling boundary, selected deletion, intent-first journal, modified file, replaced identity, locked file, execution-time exclusion, cancellation, journal failure, outside-root selection, junction scan, swapped parent junction, multiple hard links, exclusions persistence, opt-in report paths, truncated journal, corrupt exclusions.
- Desktop smoke run: app launches, scans two disposable generated files, renders Overview/Cleanup/Exclusions/History, and exits normally. No cleanup is permitted in smoke mode.
- Overview and populated Cleanup screenshots inspected locally; header, overview newlines/scrolling, and primary button layout corrected.
- UI revision: all four pages rendered under Light, Dark, and Follow Windows; Dark/Teal cleanup rendered at the minimum window size. Inspected overview, cleanup, exclusions, and history renders; corrected clipped controls and dark disabled-button contrast. Appearance reload and selection-action state checked in smoke mode. These are app-rendered captures, not full native desktop interaction acceptance.

Tests wrote/deleted only generated fixtures in `.test-data`. No real user temporary-file cleanup was performed. The app's overview read the host's system information. Screenshots and fixture paths are local artifacts, excluded from Git.

## Not yet established

- Representative-user comprehension or manual full workflow acceptance.
- A broad Windows 11 build, filesystem, accessibility, DPI, and concurrency test matrix.
- Production readiness, signing, installer behavior, or a supported-runtime release policy.
- Remote GitHub CI execution: workflow exists but no remote repository has been created or pushed.
- Trademark clearance, source/binary licensing, publisher identity, or private security/abuse contacts.

Reproduce the fixture checks with `dotnet run --project tests/Maintenance.Tests -c Release`. The app's `--smoke-test` flag uses generated local fixtures and is solely a developer validation mode.

## File-browser revision

- 22 core checks passed, including a bounded partial scan and a complete 20,001-file scan beyond the former cutoff.
- UI smoke exercises menu-close-before-item-click ordering for both appearance controls, persisted settings, path display, folder grouping, name and numeric-size sorting, size/text filters, and clearing selection when filters change.
- App-rendered Light/Dark captures inspected at normal/minimum window sizes. No full manual native-interaction or million-entry performance acceptance is claimed.


## System and results revision

- Release publish passed with warnings as errors; 22 core checks passed.
- Live local Windows inventory returned over 28,000 properties. System/overview/cleanup renders inspected. UI smoke checked retained simulated cleanup outcomes, disabled repeat cleanup, Clear results, readable size units, and storage measurement against generated files. Smoke did not delete real files.
- Storage analysis is explicit, read-only, bounded to 200,000 entries or 20 seconds per fixed volume. OS means the Windows folder, temp means current-user and Windows temp; logical sizes are not physical disk allocation. Missing, redirected, offline, and inaccessible entries are reported as partial coverage.
- Active RAM timings/channel mode remain unavailable through this standard inventory provider; no values are inferred. Live CPU uses Windows system-time sampling. Interactive flicker improvement uses double buffering; broad hardware/DPI acceptance remains outstanding.


## Organized System and local-drive revision

- 24 core safety checks passed, including timed partial scans and manual scan cancellation without a deadline. Release build passed with warnings as errors.
- UI smoke exercised summary/expanded System views, full-detail search, full-path sizing, local-drive exclusion predicate, all six storage category mappings, read-only drive catalog fixtures and disabled deletion in drive-review mode. All five pages rendered under the three themes.
- Storage analysis now runs automatically and concurrently for ready local fixed/removable volumes. Existing 20-second/200,000-entry measurement bounds remain separate from cleanup Max wait. Whole-drive catalog uses optional Max wait and never grants cleanup eligibility. No real user files were deleted in validation.


Friendly System revision: published with warnings as errors. UI smoke verifies Services/Startup are absent, friendly labels and placeholder suppression, expanded component views, and full-detail search. Light memory-section render inspected. No cleanup-engine changes.


## Million-record results regression

Synthetic one-million-record UI check passed: 500 live rows per page, Next navigation, and filtering to a match at the end of the complete dataset. UI timer continued during background preparation (20 ticks); measured combined preparation/filter test about 0.75 seconds on this host. This is a result-rendering stress check, not a real full C: traversal or proof that every Windows file operation is interruptible. No user files were deleted.


## Focused cleanup / hardware tabs

Release build and UI smoke passed. Smoke verified known review locations without drive roots, read-only folder mode, full-export raw property preservation, hardware tabs/search, million-record next-page enablement after history/action refresh, and direct jump to final page. Inspected cleanup pager and memory hardware-card renders. No user files deleted. Downloads resolves through Windows Known Folders; folder picker excludes whole-drive roots.


Device/speed preview validation (2026-10-10): Release build and all 24 cleanup safety tests pass. UI smoke exercises all six navigation pages in Light/Dark/Follow Windows. Added real generated 8 MB local sequential write/read with removal, cancellation cleanup, known ASUS PCI subsystem lookup and USB VID/PID parsing. Existing million-record paging and theme checks pass. Public Hetzner test-file index pages checked successfully for all five targets; no full internet benchmark or real SMB share benchmark was run during validation. Network-share permissions, disconnection behavior and physical disk speed require testing in the intended environment. Read measurements may be satisfied by caches. USB identification uses Windows descriptors and raw IDs; no USB vendor database is bundled.

DiskSpd engine source: https://github.com/microsoft/diskspd/releases/tag/v2.3 . Windows x64 binary and upstream MIT license included under Tools. The UI uses unbuffered/write-through (-Sh) explicitly and does not retry with buffered mode if unsupported. CrystalDiskMark reference recording reviewed for size/pass/unit menus. Tests exercise four workload pairs on initialized generated data plus native-process cancellation and file cleanup; UI smoke checks populated unit changes. Real SMB shares and high-DPI physical display acceptance remain user validation items.

Network/internet verification: app completed an actual 100,000,000-byte download from OVHcloud Virginia (1.84 seconds in this environment). All six replacement targets returned HTTP success to HEAD checks. UI smoke confirms explicit network folder/mapped-drive controls and single download card, along with existing benchmark/cancellation checks. No real SMB share test performed. Compact cards no longer stretch vertically with the window; switching test modes clears results from the previous mode.

2026-10-10 source expansion: 31 core safety checks pass, including multiple roots, duplicate/overlap suppression, rejection of candidates from unchecked roots, execution-time changed-file preservation, shared timeout, cancellation, and empty-source rejection. UI smoke still substitutes a generated fixture engine and refuses cleanup execution; it does not scan/delete real temp stores. Interactive UAC acceptance/decline and actual system-source cleanup are not exercised automatically. Hardware projection validated on this machine: one grouped 8BitDo keyboard, separate Logitech virtual input, two EDID monitors, ASUS GeForce RTX 5080 heading. Manufacturer catalog is initially limited to exact models documented in hardware-specs.json.

### NTFS streams revision
39 core checks passed, including named file and directory streams, default-stream exclusion, name/owner/size, preservation, cancellation and Max wait. NTFS fixture testing is local; real USB and SMB targets have not been exercised. Network servers can restrict or truncate enumeration; filesystem capability is reported by the server, not independently audited. Reparse/offline objects and inaccessible entries are skipped. Stream presence is not a malware verdict. No stream deletion is implemented.

Final revision: 45 core checks passed. App smoke checks passed under all three themes, with generated-file recycling verified by finding that fixture in the Windows Recycle Bin. Folder-group selection/deselection and opt-in personal-file action enablement passed. The Windows shell recycling adapter uses recycle-on-delete and no connected-element expansion; no permanent-delete fallback is requested. Personal files are identity/size/time/exclusion checked immediately before the path-based shell operation, with ancestor directory guards retained. Unlike temporary-file deletion, shell recycling is not a single-handle atomic operation against replacement of the leaf file.

A read-only full C: traversal inspected 1,970,565 entries and identified 588,918,245,936 logical game bytes (~548 GiB). 4,944 inaccessible/redirected/offline entries were skipped. This is not allocated-space accounting: hard links, compression, sparse files and inaccessible content can prevent category sums matching physical used capacity. The chart explicitly labels the discrepancy.

Detailed History: 51 core checks passed. Added coverage for pending/final deduplication, unmatched pending classified as Unknown, skipped-file size retention, scan locations and timestamps, truncated journal coverage, and retention beyond 30 records. Legacy files are parsed without migration; unknown legacy sizes and locations remain explicitly unrecorded. UI fixtures are simulated in the smoke test store only.

### Cleanup review expansion — October 10, 2026
78 core safety checks passed. New checks cover restore-point revalidation/newest/last-point protection, VM format coverage, raw-image opt-in, read-only VM discovery and cancellation. The benchmark smoke collector was corrected to collect results synchronously: the previous test created asynchronous progress callbacks on the worker thread and could assert before all eight samples arrived. Benchmark execution itself and own-file cleanup passed after the correction; smoke exceptions now produce a failure log instead of an interactive JIT dialog.

New UI fixture checks exercise Settings, display-unit conversion, Windows release data/uptime, the 64 KiB ADS text/hex/download-origin viewer, browser cache-root boundaries, restore-point selection, update-storage measurements, virtual-machine results, and conservative app-data review. Screenshots are app-rendered captures, not full physical-desktop/DPI acceptance. Browser credentials/history/bookmark fixtures stayed outside cache scan roots. Ordinary user caches and restore points were not deleted during validation. System Restore live enumeration was denied under the non-admin test account; actual elevated enumeration/removal still requires interactive acceptance. Removal uses SRRemoveRestorePoint, never blanket VSS deletion; success requires a fresh query to confirm absence. Update/installation mode never deletes files and links to Windows tools.

Read-only F: classification inspected 959,242 entries, skipping 2,637 inaccessible/redirected/offline entries. It attributed 317,821,719,685 logical bytes to development contexts and 1,317,557,423,341 to virtual-machine/WSL material. These are logical sizes, not allocated-space accounting or proof every skipped file was classified. Live Windows inventory reported release 26H2, build 26300.9457, Home edition, activated retail license; update/channel fields remain local configuration evidence, not online health verification.

The delivered demo bundles .NET and app data/tools in a self-contained single EXE. Manufacturer specifications remain a small verified exact-model catalog. App-remnant ownership is deliberately uncertain absent stronger evidence; registered installed apps are separate from possible caches and usage is not guessed. Richer remnant attribution and exact duplicates are tracked in ROADMAP.md.
Capacity / selection revision: Release build succeeded without warnings. Rendered light/dark capacity gauges at empty, tiny, midrange, near-full and full usage. Rounded option controls preserve CheckBox keyboard/accessibility semantics while showing accent-selected surfaces; full labels use measured preferred sizes. Fixture UI check clicked the rendered folder checkbox hit target and selected both visible files, verified selection-off clears checks and disables cleanup, and verified re-enabling. Teal/Violet accent propagation checked on option controls, file rows/folder bands and table styles; app-rendered captures inspected. No user files cleaned. File-list checkboxes remain for bulk selection; option menus, filters, source selectors, navigation and tabs use rounded surfaces.

2026-10-10: Unified category/capacity gauge (gray unmeasured used space, hatched free space), grouped scan chooser, and DMTF restore-point timestamps. 82 core checks passed; selection smoke and rendered light/dark capacity fixtures passed. Restore dates tested with UTC offsets and malformed input; no live restore points removed. Elevated inventory is not verified by these fixture tests.

2026-10-10: System native tab frame replaced by wrapping themed navigation and plain content panels. Windows uptime moved into a fixed-height card. System layout smoke passed with live read-only hardware inventory; navigation bounds stay unchanged and uptime survives rerender. Dark-theme screenshot inspected.

2026-10-10: Extended benchmark fixtures ran real DiskSpd mixed 70/30 and 50/50 I/O, 64K random and sequential workloads (64 MiB generated file, one-second verification intervals). All eight read/write results had throughput and ordered P95/P99 values; test file removed. Production sustained mode uses 60 seconds per direction/pass; a full duration run was not performed. Verified palette serialization/default reset, fixed equal card heights, themed History, and a five-digit download value screenshot. New generated icon packaged at seven Windows sizes. No user files or restore points were removed.

2026-10-10: Updates placeholder page added before History/System. No update network calls or jobs. Automatic-update preference defaults off, persists both toggle states, and is preserved when appearance settings are saved. Personal license displayed by default. Placeholder UI checked in light/dark; persistence smoke passed.

2026-10-10: System cards now use an aligned two-column grid with equal row heights and consistent gutters, falling back to one column below 1000 pixels available width. Live inventory layout smoke passed; Windows tab screenshot inspected with uptime and release card edges aligned. Uptime refresh remains within its card.

2026-10-10: Extended profile now includes all eight workloads, including Quick's original four. Profile-switch smoke verified 4/8 cards, 2/4 grid rows, all-test run selection, every individual test, and repeated switching. All 8 tests picker screenshot inspected. No benchmark engine changes in this revision.

2026-10-10: Custom selection added to both benchmark test pickers. Clickable, keyboard-selectable cards use the chosen theme accent; selected subset drives duration estimate and benchmark execution. UI smoke verified select/deselect, exact subset, disabled start when empty, selection locked while running, and preserved 4/8 profile choices. Screenshot inspected.

2026-10-10: Internet speed mode now defaults to Download + Upload; direction-only modes and 10/25/50/100/250/500 MB or 1 GB per direction. Cloudflare documented endpoints verified with successful live 10 MB download and generated upload. Regional OVH targets remain download-only. Streaming bounded buffers, cancellation, 15-minute limit, sequential directions, no personal file uploads. Dual live gauges and dark UI screenshot inspected. Larger sizes and every regional endpoint not live-tested in this revision. Measurements include request overhead, not a guaranteed line-capacity score. API reference: https://github.com/cloudflare/speedtest .

2026-10-10 modern activity / startup revision:
- Core fixture suite: 82 checks passed. Release builds succeeded without warnings.
- Modern UI smoke: overview detail lines removed, exactly one system-volume badge, remaining native tab frames replaced, activity remains visible across pages and stops when work ends; light/dark captures inspected. Native scroll surfaces now follow the selected theme.
- Live internet: Clouvider Amsterdam and GARR Rome each completed 10 MB download and generated upload, with persistent-history reload assertions. Clouvider rejects 10 MB POST bodies; alternative uploads now use 512 KB chunks. Cloudflare's 1 GB run remained rate-limited after bounded retries: no complete result is claimed. Intentional cooldown/pacing is excluded from reported transfer time. Independent direction failure handling preserves any completed direction. Existing Cloudflare 10 MB validation predates this revision.
- Startup inventory: 553 real read-only rows across services, tasks and startup registrations. UI/protection smoke passed. Disable/restore backend tested ONLY with mocked PowerShell inventory and mutation commands: service startup/restore/delayed start, stale state, changed account, dependencies, Windows protection, task state/definition changes and Microsoft task protection. No host service/task settings changed. Real mutation behavior requires controlled-system acceptance; no claim of live disable/restore testing.
- Unused-app rules: exact 90-day boundary, newer usage, unknown usage, recent install, reinstall chronology, timezone equivalence, path-prefix identity boundary, helper exclusion and conservative recommendation defaults passed. Live inventory found 214 registrations; Security event history unavailable in the standard-user smoke. No adequate continuous inactivity coverage is claimed. Optional foreground observation uses Windows event notifications only while enabled and open, with session/gap records; end-to-end tracking across app updates, portable/packaged identities, launcher launches and multiple sessions is not yet acceptance-tested. No polling-based coverage claim.
- Startup mutation references: Microsoft Set-Service (Windows PowerShell 5.1), Disable-ScheduledTask and Enable-ScheduledTask. Task definitions and service paths/accounts are rechecked before changes; future runs/start modes change, running work is not terminated. Backups and intent precede execution; unknown/pending records are not reported as proof of successful change.

2026-10-10 startup publisher revision: Release build/publish succeeded without warnings. Live read-only inventory smoke loaded 553 rows and publisher metadata; all three tabs rendered. Fixtures verified exact Microsoft publisher matching, lookalike rejection, hiding/unhiding, retention of unknown and third-party entries, publisher search and known-service explanation. Filtered dark screenshot inspected. No service/task settings were changed. Company metadata and task folders are classification evidence, not signature verification or safety verdicts; task authors are not used alone to hide entries.

## Exact duplicate finder — October 10, 2026

102 core safety checks pass, including 20 duplicate-specific assertions. Fixtures cover equal bytes with different names, same-sized different bytes (including matching first/last samples with a changed middle), overlapping roots, exclusions, protected subtrees, hard links, named streams, changed selected/kept files, foreign and keeper selections, cancelled/limited scans, cancellation before removal, audit failure, retained-copy locking, duplicate request suppression and recycle-adapter failure without permanent fallback.

`--smoke-test --duplicates-check` creates its own fixtures and isolated history. Verified 105 exact groups across three group pages, 405 synthetic copies across three copy pages, full-result search, explicit selection, keeper protection, saved history, theme switching and 1280x900 layout. The smoke check also recycles one generated file with the production Windows shell adapter, finds that exact generated item in the Recycle Bin, verifies its bytes, restores it using the shell, and checks both original keeper and restored payload. No user documents are modified. Screenshots and dated receipt: `artifacts/duplicates-dark.png`, `artifacts/duplicates-light.png`, `artifacts/duplicates-compact.png`, `artifacts/duplicates-check-success.txt`.

The local NTFS implementation is validated on this machine. Physical USB hardware, network volumes and non-NTFS volumes are not validated; the latter two are rejected. Estimates are logical bytes, not physical space. Fixed local shell recycling inherits a short path-based handoff window after the selected file handle closes; locked keeper and parent handles do not make this a hostile-race-proof quarantine. Failures after a shell action but before durable outcome recording remain unknown in history. User decisions about which location to retain remain necessary even for exact contents.

Shell adapter flag reference: https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nf-shobjidl_core-ifileoperation-setoperationflags . Recycle-on-delete is requested; the application has no permanent-delete fallback in this workflow.


## Subfolders, storage coverage, and full UI review — October 10, 2026

105 core checks passed. Added regression checks for recursive read-only directories, cloud-tag classification versus symlinks/junctions, and offline/recall flags. Existing cleanup safety/recycle tests retained. Read-only inspection of the user-selected OneDrive eBay pictures path traversed 36 folders (35 child entries) and Windows returned zero files, with zero scanner omissions. No user data was written or removed. This verifies that folder traversal is no longer blocked; it does not establish local OneDrive file-content matching from this empty subtree. Cloud matches are intentionally review-only; ordinary cleanup guards still reject reparse ancestry.

`--full-ui-check` captures every sidebar page in light/dark at 1760x1120 and 1280x900, all hardware/history/startup tabs, cleanup locations, speed-test modes, Settings, and scrolled short-window states. Outputs: `artifacts/polish-*.png`, `artifacts/full-ui-check-success.txt`, and `artifacts/full-ui-layout-review.txt`. The automated flow-layout action check reported zero outstanding bounds violations; visual inspection separately caught and corrected dock starvation, notes clipping, empty-state behavior, and unnecessary horizontal scrolling. This is local UI/fixture validation, not every possible DPI, device or live benchmark environment. Storage fixtures verify byte totals, missing-directory classification, reason-count reconciliation and example capture. Full C-drive coverage is collected on the user's next Refresh storage; old aggregate skip counts cannot reconstruct individual historical paths.

Cloud attribute references: https://learn.microsoft.com/en-us/windows/win32/fileio/file-attribute-constants and https://learn.microsoft.com/en-us/windows/win32/fileio/reparse-point-tags . Cloud placeholder tags are distinguished from name-surrogate links. Offline/recall-on-open/recall-on-data-access entries are omitted without requesting cloud hydration.

## App data map validation — October 10, 2026

115 safety checks passed, including ten new ownership/map checks: nested content remains unchanged; unknown and name-only ownership stay uncertain; registered and running path associations require folder boundaries; cancellation/entry limits/missing roots are marked partial; junctions are skipped without double counting. The new view has no deletion control or removal API.

Packaged `--smoke-test --app-map-check` verifies two generated folders, search, JSON roundtrip, light/dark at 1760x1120 and 1280x900, then runs a six-second cancellation-bounded read-only live scan through the actual UI workflow. Receipt: artifacts/app-data-map-check-success.txt. This does not validate Store identity, abandoned-app classification or universal app ownership. Process and uninstall evidence is incomplete by design, and folder bytes are logical, not allocated/reclaimable space.

## Feedback revision validation — October 10, 2026

126 safety checks pass. New fixtures cover composition reconciliation, conservative file categories, positive installation/binary/reference evidence, marker-only insufficiency, missing inventory, partial scans, shared roots, and qualified Store name matches. Read-only live inventory found null service/task entries; collection now removes empty values at the boundary. Smoke mode rethrows scan errors so a partially assigned report cannot incorrectly pass an integration check.

Packaged --feedback-check verifies cache review/back with original tab/results, one folder chooser, long cell fitting/wrapping, user-resized column preservation, app-data search and export, and a cancellation-bounded live scan. --full-ui-check captures all nine sidebar pages and hardware/history/startup tabs in light/dark at two sizes. The latest full check reports zero action-bound violations. Visual review caught and corrected startup registrations incorrectly grouped as disabled and hardware-card sizing interaction. --duplicates-check passes retained-copy/selection/page tests and generated fixture recycling/restoration. No personal files removed; no startup configuration changes or live benchmarks run.

First-run rule references (path/identity guidance, not abandonment proofs):
- https://code.visualstudio.com/docs/setup/uninstall
- https://code.visualstudio.com/docs/configure/settings
- https://support.discord.com/hc/en-us/articles/115004307527--Windows-Corrupt-Installation
- https://npp-user-manual.org/docs/config-files/

Negative evidence checks are scoped and heuristic. These rules are fixture-tested, not an end-to-end certification of every supported application's uninstall/version/portable lifecycle. The UI preserves those limits and requires explicit file review for existing recoverable cleanup actions.

## Workflow controls revision

Published Windows x64 single-file executable: artifacts/workflow-controls/MaintenanceForWindows.exe. Release build succeeds without warnings; all 126 core safety checks pass.

Packaged full UI check covers all nine pages, hardware/history/startup tabs, all app-review tabs, cleanup and benchmark modes at 1760x1120 and 1280x900 in light/dark themes. Zero action-bound violations. Visual review additionally caught Start search being below the visible portion of the temporary-source list; it now precedes that list.

Packaged feedback checks cover green/red action roles, result context-menu entries, existing/deleted/web target resolution, cancellation of a Windows inventory subprocess, app-data live bounded cancellation with retained rows, preserved cache back navigation, and column sizing. Tests open no user files, remove no personal data, and change no startup settings. Inventory queries that return a single response retain older loaded results on cancellation; file enumeration returns newly collected partial results.

## Universal stop, focused results and history contents

Published executable: artifacts/focused-results/MaintenanceForWindows.exe. Release publish succeeds without warnings.

--stop-all-check passes fan-out to five job tokens, an actual owned PowerShell process termination, retained fixture rows/files, idle-state recovery and universal button placement at 1280x900. This exercises cancellation, not interruption of an unsafe native mutation. Pending Windows calls and I/O can still take time; no completed deletion is rolled back. Helper termination is scoped to processes started through the application's inventory runner.

--focused-results-check passes maximized pop-out, shared filter propagation, available selection/recycle commands, return with rows/filter preserved, standalone grid filtering/restoration, combined duplicate panes, incoming list updates without stale restoration, and All activity searches for Rome and nested journal contents. Visual review verifies the compact toolbar and search row. No personal files removed or system settings changed.

Full UI checks show zero action-bound violations in light/dark at 1760x1120 and 1280x900. Core safety suite: 126 checks passed. Fixture coverage does not claim every pending kernel/native Windows operation stops immediately.

## Compact cleanup filters

Executable: artifacts/compact-filters/MaintenanceForWindows.exe. Release publish succeeds without warnings. Packaged compact-cleanup-check and focused-results-check pass. Generated fixture checks cover default collapse, recovered result height, filter bounds at 1760x1120 and 1280x900 in light/dark, filtering while hidden, active criteria count, full reset, mode switching and shared pop-out filters. Full UI check using the same compiled application DLL covers all nine pages and reports zero clipped actions. Visual review confirms the selection toolbar and wrapped filter panel. No personal files deleted or system settings changed. An earlier output path returned Windows access denied after its first successful launch; the delivered compact-filters executable was verified with two successive packaged launches.

