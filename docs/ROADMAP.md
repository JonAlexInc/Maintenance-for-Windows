# Future features

## File recovery — proposed build option

Requested October 10, 2026. Accepted for the development plan; **not implemented or validated recovery support**. Research Lane remains research-only. This entry authorizes planning, not raw scans of user drives, tool installation or bundling, purchases, restoration, or changes to user storage.

### Development sequence and gates

1. Finish the current results, filter, cancellation and history polish. Define recovery destination/source identity checks and a shared operation guard before introducing a recovery backend. Keep app-owned undo/recovery for reviewed cleanup as a separate requirement; permanently deleted files have no promised rollback.
2. Build restore assistance first: review available Recycle Bin copies, an app-owned recovery area only if one has actually been implemented, and supported backups/previous versions. Explain which source supplied each copy and record actual restoration outcomes. Absence of an existing copy is not proof that deeper recovery will work.
3. Evaluate a guided Windows File Recovery backend. Before choosing it, verify current documented capabilities, filesystem/mode coverage, installed/version detection, automation behavior, destination restrictions and integration/redistribution terms. Do not assume it is installed or bundle it without cleared terms. Record findings and the chosen integration contract before implementation. Other engines require their own capability and license review.
4. Build filesystem-aware deleted-file recovery for local internal/external HDDs, SSDs, USB drives and SD cards. Begin with separately validated NTFS, FAT32 and exFAT coverage; enable only methods proven by fixtures and subsequent hardware acceptance. Encryption requires authorized unlocked/decrypted access. Unsupported or locked sources must be clearly identified before a scan starts.
5. Add deeper signature-based recovery and disk-image input after the earlier stages pass acceptance. Include the foreign-filesystem expansion below in this same build option, beginning with image-based content scanning for ext2/ext3/ext4 and HFS+. Expand filesystems and devices individually after validation. Deep carving may lose filenames and paths and fail on fragmented files. Network shares and cloud storage require provider/server-specific restore integrations; they do not inherit universal raw recovery support.

Each stage ships only after its own acceptance gate. No universal recovery, guaranteed recovery percentage, or complete-file claim based solely on finding a filename. Physically failing drives need a stop/escalation and image-first route, not repeated deep scans or repair writes. External rescue support for active-system-drive recovery is later work, not current coverage.

### Linux and Mac filesystem expansion — staged targets

Added October 10, 2026 to the existing File recovery build option. All entries below are proposed targets, not implemented or validated coverage. Attached foreign-filesystem drives and disk images may be candidates even when Windows Explorer cannot browse them, provided the selected backend can obtain legitimate read-only raw access. A drive being attached does not establish that its data is accessible or recoverable.

| Target | First acceptance stage | Later evaluation |
| --- | --- | --- |
| Linux ext2/ext3/ext4 | Disposable-image signature/content scanning, validated separately for each filesystem | Filesystem-aware metadata recovery, retained names/paths, fragmented files, and available journal-based methods; ext2 undelete evaluated separately from ext3/ext4 |
| Mac HFS+ | Disposable-image signature/content scanning | Filesystem-aware metadata recovery, retained names/paths, fragmentation and available journal-based methods |
| Linux Btrfs | Per-backend feasibility and image fixtures before any supported label | Compression, fragmentation, multi-device layouts and recovery from snapshots that actually contain the sought data |
| Linux XFS | Per-backend feasibility and image fixtures before any supported label | Metadata and fragmented-file fidelity; do not infer undelete from partition recognition or generic content scanning |
| Mac APFS | Separate backend/container/encryption feasibility and image fixtures before any supported label | Container/volume layouts, compression, fragmentation and snapshots that actually contain the sought data; generic carving does not establish full APFS recovery |

Use a per-source, per-method capability display: **Filesystem recovery supported**, **Content scan supported**, **Unsupported**, or **Not validated**. Partition recognition is a separate informational result. It must not enable or imply undelete, metadata recovery or intact-content recovery. Report filesystem-aware recovery and signature/content carving separately, including whether names/paths and fragmented files can be reconstructed. Existing snapshots are useful only if they contain the sought data; their mere existence establishes no recovery outcome.

Research Lane reports PhotoRec documentation for ext2/ext3/ext4/HFS+ content carving and cross-platform execution, and TestDisk documentation for ext2 undelete. Treat those as candidate-engine research inputs to reverify against the exact backend/version and integration contract. TestDisk's broader partition-detection list is not an undelete support list. Resolve GPL/commercial integration and redistribution terms for every backend before distribution; no engine installation or bundling is authorized by this plan entry.

### Foreign encryption and future platform access

Recover recognizable plaintext only through authorized unlocked/decrypted access. Raw ciphertext is not recovered file content. Linux encrypted volumes require compatible legitimate unlock/decryption tooling; encrypted or unsupported containers remain out of scope until the relevant access path is implemented and validated.

Modern Apple silicon/T2 internal storage has hardware-encryption constraints even with FileVault off. Plan for recovery on original hardware or a Mac-side workflow where required; do not promise that removing storage and attaching it to Windows will work. Evaluate future Mac/Linux companions and external rescue/media environments for access Windows cannot provide. These are proposals, not current app capabilities, and must retain the same source/output protections and truthful capability labels.

TRIM/discard and overwrite limitations apply across operating systems. Switching operating systems cannot recover blocks that are no longer exposed. Keep the existing failing-drive stop/escalation route rather than repeatedly scanning an unstable source.

### Recovery experience

- A dedicated File recovery area asks for the source and loss scenario, then shows the detected device, filesystem, supported methods and method-specific limits. Try existing copies/quick recovery before offering a deep scan.
- Require an output destination on a **different physical device**, verified by device identity rather than drive letters alone. Multiple partitions on one device do not meet this requirement. Reject unresolved destination identity for methods that depend on this protection.
- Show plain-English notices at method selection and immediately before action: best-effort recovery; potentially incomplete or corrupt files; no guarantee; SSD TRIM and overwrites can make data unavailable; continued use can reduce recovery prospects; destination restrictions apply. A detected filename is not proof of intact recoverable contents.
- Preview recovered copies only when safely supported. Never execute recovered binaries as a preview. Distinguish complete/verified, partial and unverified results using actual content verification evidence; explain what verification checked and what it cannot prove.
- Provide cancel and the universal stop control with accurate partial state. Retain findings and saved-copy outcomes without claiming pending files were recovered. History records method, source/destination scope, files actually saved, sizes and verification outcomes; privacy controls govern exported paths.

### Source protection

The engine reads the source without repair writes. Suspend app cleanup and disk benchmarks touching that source for the recovery session. Route downloads, logs, temporary files, scan databases and recovered output away from the affected physical device, including when the normal app-data location is on that device. Fail the preflight if safe working storage cannot be established.

Read-only engine access does not stop Windows or other applications writing to a mounted disk. Active-system-drive recovery must explain that limitation and guide users toward appropriate offline/external rescue methods when available. Do not promise successful rollback, recovery of permanent deletions, or a forensic write-blocked source merely because the recovery engine uses read-only handles.

### Acceptance before coverage claims

Use disposable disk images/virtual disks and generated files. Validate each supported filesystem and method against deletion, quick-format scenarios, fragmentation, locked/encrypted boundaries, source/destination rejection, engine-caused source changes, cancellation, interruption, preview safety and recovered-content verification. Verify operation guards and that all working files stay off the source device. Follow fixture validation with broader hardware acceptance before advertising device/filesystem coverage. Do not test raw recovery against personal drives during this planning work.

For foreign filesystems, score partition detection, recovered-content fidelity and metadata/name/path fidelity separately. Add compressed-file, multi-device/container-layout, encryption/unlock and applicable journal/snapshot fixtures for each backend. A detected partition or listed filename cannot pass content verification. Report partial and unknown validity honestly; enable user-facing filesystem and physical-device coverage only after corresponding acceptance. Image tests precede physical-source support and inherit read-only engine access, no repair writes, output on a different physical device, safe preview, cancellation and accurate partial-result requirements.

### About, terms and identity

Include recovery limitations and privacy/terms links in About, alongside the contextual notices above. Public developer identity supplied by the user: **JonAlexInc**, [GitHub](https://github.com/JonAlexInc). Do not infer a legal publisher name, support address or liability entity from that handle. Terms/liability wording needs release review and must match implemented behavior; it does not substitute for source protection. These About additions are planned, not implemented by this entry.

## Exact duplicate finder

Implemented October 10, 2026 as a dedicated Duplicates page.

- Compare multiple user-selected NTFS local/USB folders or drives, with Downloads and Desktop shortcuts.
- Size buckets and first/last samples narrow the work; full SHA-256 plus byte-for-byte comparison verifies matches.
- Search every result and sort groups by extra size, file size, copy count or name. Groups have 50-item pages; copies have 200-item pages.
- No removal selection by default. Choose a kept copy, explicitly select other copies, then confirm Recycle Bin removal on fixed local drives. USB/removable volumes are review-only.
- Keep the retained file locked while rechecking selected copies. Revalidate file identity, content, attributes and exclusions. Never intentionally fall back to permanent deletion.
- Skip protected/system/application folders, hard links, reparse paths, offline/read-only files, zero-byte files and files with named streams. Extra-copy totals are logical sizes, not promised physical space reclamation.
- Persist scan and per-file action history; export full matching paths and outcomes. Cancellation and the 250,000-entry bound mark the scan partial. Details retain the first 1,000 skip reasons.

Remaining: non-NTFS and network volumes, identical named-stream comparisons, physical allocation/shared-extent accounting, configurable large-scan budgets, persistent scan sessions and similar-photo matching. Shell recycling still has the existing path-based handoff boundary; this is not a hostile filesystem race-proof quarantine. Content equality does not establish that an application no longer needs a particular path.

## Application ownership and remnants

Initial preview reviews known cache/log folder names in current-account Local AppData and compares user/machine uninstall registrations. Missing registrations never establish abandonment. Portable, Store, shared and custom installations are explicitly uncertain. Installed-app usage is not inferred. Future work: richer ownership evidence, Store inventory, verified removed-application manifests, downloaded updater assets, and stronger running-process association before presenting higher-confidence remnants.

### Application-attributed abandoned-data cleanup (future; not implemented)

Expanded October 10, 2026 at the user's request through Research Lane. Keep ownership, application absence, and data disposability as three separate determinations.

1. Read-only ownership report for a small validated rule catalog, spanning Local, Roaming, LocalLow and validated app-specific ProgramData locations. Combine installation records, package registrations, executable identity, known paths and corroborating metadata. Cover current-user and machine installations, explicitly reporting unscanned/inaccessible scope. Check portable copies, multiple versions, shared consumers/publishers. Avoid Win32_Product. Group by application with sizes, categories, evidence and known/likely remnant, shared/still-used, or unknown status; never rely on folder name or age alone.
2. Reviewed removal with tested recovery. Separate cache/log/temp assets from settings, credentials, databases, saves and personal content. Leave uncertain findings unselected; recheck file identity, app presence and current usage at action time. Skip changed/locked/newly used files, avoid reparse traversal, account for hard links. Where supported use protected same-volume recovery preserving metadata/ADS and original-location journals. Restore without overwriting newly created files. Moving to recovery does not reclaim space; purge only under explicitly selected retention/removal policies.
3. Broaden validated version-specific rules and retain a small local ownership/install history. Optional targeted observation around installation/use/uninstallation is later work, with no continuous monitoring by default.

Acceptance uses safe fixtures and controlled installation/use/uninstallation scenarios: retained settings, rarely used active apps, portable/multiple versions, shared folders, package cache versus persistent data, reinstall races, changed/locked files, junctions/hard links, denied permissions, interruptions and restore conflicts. Each rule needs version evidence and tested cleanup/recovery behavior. This remains separate from exact duplicate detection.

## Release readiness: signing and Microsoft Store (future)

Requested through Research Lane on October 10, 2026. Planning only: no purchase, registration, signing enrollment or publishing is authorized here.

- Establish a verified publisher identity; timestamp signatures on the app executable, installer and applicable helper binaries. Respect third-party binaries and their existing signatures.
- Validate signed releases with Windows protections enabled. Signing verifies publisher identity and integrity; it does not guarantee the absence of SmartScreen prompts or antivirus false positives.
- Assess Microsoft Store policy fit and choose the distribution route: Store MSIX receives Microsoft signing; a traditional EXE/MSI route needs publisher signing.
- Verify current eligibility, onboarding fees and signing prices during implementation. Research Lane reported Azure Artifact Signing Basic at $9.99/month, 5,000 signing operations/month and $0.005 per additional signature; these are planning estimates pending verification. Signing operations are per file/build, not customer download. Research Lane reported free new Store registration flows; verify at onboarding.

Track separately from cleanup, duplicate detection and technician licensing.

## Unused apps: initial implementation and remaining coverage

Implemented initial historical report under Apps & leftovers: registered apps/games, 90-day default with 30-day through five-year filters, current-user existing Security 4688 execution records where available, optional local event-driven foreground observations, registration size estimates, source/coverage labels, sortable rows, keep/exclude and Windows uninstall management. Old records remain possible candidates, absent records unknown. Supported inactivity stays empty until an adequate, validated observation source exists. Installation-date evidence is only a conservative exclusion, not a last-use proxy.

Remaining: validated Prefetch ingestion, packaged/portable/launcher-specific identity discovery, sustained coverage accounting sufficient to support inactivity, multiuser boundaries and end-to-end controlled launch/update/reinstall/gap acceptance. No uninstall or file removal was performed as part of implementation. Foreground observation is off by default and session-limited; it does not establish comprehensive execution monitoring or five years of history.


## App data map — initial ownership evidence view

Implemented October 10, 2026 under Cleanup > Apps & leftovers > App data map. Recursively measures current-account Local, Roaming and LocalLow top-level folders, with search, numeric sorting, exact paths, evidence details, folder opening and JSON export. Accessible running executable paths and registered install locations provide association evidence; exact name-only matches stay explicitly unverified. Unknown ownership never implies abandonment. Read-only map, no removal action.

Coverage: 250,000-entry shared budget, cancellation preserves partial rows, inaccessible/linked/recall-required entries omitted. Logical sizes can include hard-link duplication. The map is a precursor to the validated application-attributed cleanup catalog above, not its completion. No Store package identity lookup, ProgramData attribution, all-user scope, exhaustive process discovery, persistent usage observation or validated abandonment verdict yet.

## First-run evidence and contextual visuals — October 10, 2026

App data map now gathers current-user package registration, uninstall and App Paths records, accessible process paths, services, scheduled task actions and Run keys before assessing folder measurements. Small documented rule set: VS Code, Discord/Discord PTB, Notepad++. Recognized markers plus negative checks can yield Likely leftovers, never safe-to-delete. Shared roots, missing inventory, incomplete folder scans or unresolved ownership do not receive that assessment. Generic folders gain category and executable-presence evidence; exact package-name segments are labeled as name evidence only.

This is not universal app attribution or full abandoned-data cleanup. Portable/custom installations elsewhere, other accounts, unregistered consumers and current use by inaccessible processes remain unresolved. Rules use expected file/directory markers, not full content/schema validation or signature verification. No claimed inactivity coverage was added. App-specific catalog expansion, installed/uninstalled history and validated reversible recovery remain future work. Composition is based on path patterns and logical bytes, not guaranteed disposability or reclaimable allocation. Bounds: 25,000 entries per top-level app folder and 250,000 per map; cancellation preserves partial rows.

Compact visuals are now paired with review data, not health scores: measured composition, exact-copy counts, action outcomes, startup configuration and observed-use coverage. Memory slots and CPU cores use Windows-reported counts. Speed and storage retain their existing charts.
