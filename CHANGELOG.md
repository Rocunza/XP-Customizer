# Change log

## XPC-GH2-GTL1-FLR1-MIT-R1-20261115 — 2026-11-15

**GitHub repository sync and licensing update. No new gameplay code was authored for this export.**

- Replaced exactly 12 already-modified C# files from the `1.6 261115 1919.zip` workspace; the other 320 C# files remain byte-identical to GH1.
- Included GTL1 tooltip formatting (`GW speed: x93%` without repeated source ratio) and FLR1 Further Learning UI reset to 60% while keeping its existing Vanilla button label.
- Added the standard MIT License with `Copyright (c) 2026 ZCN` and updated English/Korean repository docs.
- Updated `.gitignore` to exclude `Local.BuildPaths.props` machine-specific paths.
- Preserved both language XML sets, the bundled profiles, the 1.6 solution/project, and the historical GH1 report/manifest.
- Retained the known Workshop clean-install profile issue and RimWorld 1.7 verification limits. The ZIP is source-only and does not establish Release or live gameplay testing.

**Current source BuildId:** `XPC-RW16-4CC-T04-B1-B1-PNL1-HF1-RU1-GTL1-FLR1-R1-20261115`.


## XPC-GH1-SOURCE-CLEANUP-R1-20261115 — 2026-11-15

**Repository organization only. No gameplay or C# code changes.**

- Extracted the XP Customizer project from the supplied four-mod development ZIP.
- Preserved all 332 compile-registered C# files, the XP-only `.sln`/`.csproj`, 28 English/Korean Keyed XML files, two bundled profile JSON files, and dependency path templates.
- Left the `Development/` diagnostics and self-tests in place because they are compiled by the project.
- Excluded other mods, VS/obj/bin caches, binaries, personal local props, old reports, and redundant archives.
- Replaced stale multi-mod instructions with focused English/Korean README and build instructions.
- Documented the known profile application failure and 1.7 verification limitations.

**Original source BuildId (unchanged):** `XPC-RW16-4CC-T04-B1-B1-PNL1-HF1-RU1-R1-20261115`.

Earlier gameplay changes cannot be reconstructed reliably from this source snapshot alone; this file intentionally does not invent a full historical version timeline.
