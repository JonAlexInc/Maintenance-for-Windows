# GitHub publication checklist

Public consumer repository: `JonAlexInc/maintenance-for-windows`, https://github.com/JonAlexInc/maintenance-for-windows. Repository creation and private vulnerability reporting were verified during publication. No stable binary release or signing enrollment is authorized by source publication.

## Naming

| Use | Name |
| --- | --- |
| Product | Maintenance for Windows |
| Paid edition | Maintenance for Windows — Technician Edition |
| Future platforms | Maintenance for Mac; Maintenance for Linux |
| Repository | maintenance-for-windows |
| First preview tag | v0.0.1 |
| First preview asset | maintenance-for-windows-0.0.1-win-x64.zip |

Technician Edition is a planned edition, not a second implemented app. Do not use Microsoft logos or imply Microsoft affiliation.

## Before public source publication

- Public consumer source and free personal use with reserved reuse rights are approved; see LICENSE and LICENSING.md. Legal publisher identity and contributor terms need final release review. GitHub hosting does not require an open-source license.
- Set a publisher identity and verify product naming and platform trademark usage.
- Establish a private security and conduct reporting contact. Enable GitHub private vulnerability reporting, and update SECURITY.md with a verified route.
- Review source, documents, history, and assets for secrets, personal records, licenses and third-party notices. Do not upload `.tools`, `.test-data`, local activity journals, or private signing keys.
- Create the GitHub repository under the chosen owner; configure branch protection, required CI and appropriate repository permissions.
- Check dependency/runtime support and supply-chain controls. Pin workflow actions to reviewed commit SHAs before a public release.

## Before publishing a binary preview

- Complete the manual acceptance steps in V0.01.md on a disposable machine; record exact OS/build and remaining limitations.
- Verify ordinary-user operation, all file-boundary protections, offline operation, accessibility/scaling, and report privacy.
- Decide signing and distribution policy. Unsigned local builds are not signed production installers.
- Build from a recorded commit; attach versioned binaries, SHA-256 checksum, prerequisites and release notes.
- Mark v0.0.1 as a GitHub prerelease, not a stable release. Do not advertise future technician/security features as implemented.
- Set a supported-runtime policy before distribution; this development build currently targets .NET 8.

References: [GitHub community profiles](https://docs.github.com/en/communities/setting-up-your-project-for-healthy-contributions/about-community-profiles-for-public-repositories), [Microsoft trademark guidance](https://www.microsoft.com/en-us/legal/intellectualproperty/trademarks).
