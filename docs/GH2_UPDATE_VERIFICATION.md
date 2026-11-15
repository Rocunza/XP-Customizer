# GH2 GitHub source update verification

- Repository revision: `XPC-GH2-GTL1-FLR1-MIT-R1-20261115`
- Date: 2026-11-15
- GH1 source ZIP: `XP_Customizer_GitHub_Only_GH1_R1_20261115_SOURCE (1).zip`; SHA-256 `c7fcf9ee2fad2dd32ccb2f9cb2ccc4af933a3e241378971c5d61759f6d343503`
- Latest workspace ZIP: `1.6 261115 1919.zip`; SHA-256 `a3aece7dda5dadfa2668a2482fc351556a99ffcefebc74b787f87c1575a350f5`
- Source BuildId in GH2: `XPC-RW16-4CC-T04-B1-B1-PNL1-HF1-RU1-GTL1-FLR1-R1-20261115`
- Author/copyright label from the source-side About.xml: `ZCN`; added root `LICENSE` using standard MIT text. **Review copyright ownership and third-party portions before publication.**

## Scope of this export

1. Started from the exact **376-file GH1** ZIP. Overwrote only the **12** listed C# files from the newer workspace; all other **320** C# files stay byte-for-byte unchanged.
2. Preserved XP-only `.sln`/`.csproj`, **332 compile-registered C# files**, **28 Korean/English XML** files and **two JSON profiles**. All XML/JSON resources were preserved byte-identically from GH1.
3. Added `LICENSE`, updated `README.md`, `README_KR.md`, `BUILDING.md`, `CHANGELOG.md`, `KNOWN_ISSUES.md` and `.gitignore`.
4. Retained historical `docs/GH1_CLEANUP_REPORT.md` and `docs/SOURCE_MANIFEST_SHA256.csv` as **GH1 evidence**. This latest export's records are `docs/GH2_UPDATE_VERIFICATION.md`, `docs/GH2_CHANGED_SOURCES_SHA256.csv`, and `docs/GH2_SOURCE_MANIFEST_SHA256.csv`.
5. Excluded bin/obj/.vs, compiled DLL/PDB, other mods, ZIP archives, machine-local props, and the obsolete multi-mod packaging script.

## Key functional changes carried forward (not newly implemented by GH2)

- GTL1: GWS tooltip shows XP multiplier only (`GW speed: x93%`); associated self-test golden strings and log prefix updated.
- FLR1: the **existing** `Restore Further Learning to Vanilla` button routes to the *60% Mod Defaults* reset, while the separate internal Vanilla reset keeps 100%. Related tests and diagnostic logging updated.
- No new gameplay code, Harmony patches or preset/translation changes have been made by this source packaging operation.

## Verification limits

- ZIP CRC and per-file SHA-256, 332 C# compile paths, XML/JSON syntax and exclusions are tested by the packaging script.
- **Visual Studio Release compilation NOT RUN**; **gameplay / live mod testing NOT RUN for this package**.
- The historical Workshop clean-install profile-apply failure is not proven fixed by a successful separate Debug test.
- RimWorld 1.7 does not have a separately compiled project in this repository; compatibility is not fully certified.
- The source-side About.xml states 1.6 only and is intentionally unchanged; check real deployment packaging separately.

## Revert

Use the original SHA-256 verified GH1 ZIP. All GH2 changes were made in a new staging dictionary, without modifying GH1 or the 1.6 workspace archive.
