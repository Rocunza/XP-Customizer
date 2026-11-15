# GH1 GitHub source cleanup verification

- Cleanup record: `XPC-GH1-SOURCE-CLEANUP-R1-20261115`
- Date: 2026-11-15
- Input: `261115 0547 깃허브.zip`
- Input SHA-256: `46f1e0dfff539bc59c68e43d6d5d95e356c235d4a1390fd863e47f1a9c0023e3`
- Original source BuildId (unchanged): `XPC-RW16-4CC-T04-B1-B1-PNL1-HF1-RU1-R1-20261115`
- C# modifications: **0**
- Original project files retained byte-for-byte: **369** (`KEEP` classification)
- Removed/excluded original files: **336**, including 3 other mods, old archives, caches, binary outputs, obsolete documents and the unsafe multi-mod packaging script.
- Newly authored docs: `BUILDING.md, CHANGELOG.md, KNOWN_ISSUES.md, README.md, README_KR.md`
- Full path/hash inventory: `docs/SOURCE_MANIFEST_SHA256.csv` (manifest lists all files except itself)

## Validation scope

- ZIP original entries and all **705** CSV classifications verified by size and SHA-256.
- XP source-only `.sln` and `.csproj` preserved, exactly 332 compile-registered C# paths found; no missing/duplicate paths or cross-project references.
- English and Korean each include 14 `Keyed/*.xml` translation files; parsed with XML parser. Key equality and duplicate keys checked.
- Both bundled JSON profiles parse.
- Packaging/About/About.xml parses and declares `zcn.xpcustomizer` and RimWorld 1.6.
- No `bin/obj/.vs`, other-mod folders, DLL/PDB, archives, private local props, or staging output copied.
- Release **not** compiled; no game launch, runtime, XML persistence, profile-apply or RimWorld 1.7 behavior verified here.

## Known source/package mismatches to resolve outside GH1

1. Profile apply rejection from a Workshop-subscribed install: `EXCEPTION_BEFORE_MUTATION_InvalidOperationException` (both bundled profiles).
2. `Packaging/About/About.xml` (1.6-only) differs from one actual Workshop About supporting 1.6/1.7; compatibility requires separate verification.
3. Removed outdated all-mods package script because it omitted XP `Languages/` and `Presets/`.

## Safe rollback

Revert to the SHA-256-identified original ZIP. This export **never modifies the original**. Source BuildId remains unchanged since this operation creates only a cleaner repository layout and new documentation.
