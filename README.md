# XP Customizer

[한국어 README](README_KR.md)

XP Customizer is a RimWorld mod for changing XP requirements, learning speed, and skill progression. Its default settings are designed to preserve vanilla XP and learning behavior; features can be enabled separately.

## Features

- **XP requirements:** Total skill XP, level-range distribution, per-skill requirements, and level-20 XP reserve.
- **Learning speed:** Configurable Consciousness and Global Work Speed (GWS) effects, including multipliers and curves.
- **Daily learning capacity:** Change daily XP saturation limits and the effect of Consciousness.
- **Further learning:** Change learning after saturation, with optional Consciousness and Mental Threshold effects.
- **Skill decay:** Configure custom XP decay at levels 1–20 (level 0 is fixed at zero).
- **Biotech aptitude:** Customize gene-based skill aptitude bonuses and penalties. Requires Biotech for this feature.
- **Books and skilltrainers:** Customize XP gains.
- **Profiles:** Save, rename, overwrite, delete, and restore configurations. Bundled optional profiles: Long (×3 total XP) and Very Long (×6 total XP). They are never auto-applied.

## Requirements and source scope

- RimWorld **1.6 source project**, Visual Studio 2022, .NET Framework 4.7.2.
- Harmony (`brrainz.harmony`) is required; Biotech DLC is used only by Biotech-related features.
- The Workshop package has also been used with RimWorld **1.7**, but this repository **does not contain a separately compiled 1.7 project**; full 1.7 compatibility has **not** been verified. Do not treat a shared DLL as a completed port.
- The source repository is **not** a directly installable mod. It contains no compiled DLL, game library, or third-party Harmony DLL.

## Build

Open [`XpCustomizer.sln`](XpCustomizer.sln) and follow [BUILDING.md](BUILDING.md). Machine-local paths are supplied via `.props` files, which must not be committed. This repository intentionally contains no multi-mod packaging script.

## Known limitations

A Workshop-subscribed installation previously rejected Long and Very Long profile application with `EXCEPTION_BEFORE_MUTATION_InvalidOperationException`, before settings mutation. A later Debug run applied a profile successfully in a different environment, which does **not** prove that clean Workshop installs are fixed. See [KNOWN_ISSUES.md](KNOWN_ISSUES.md).

## Project information

- Mod ID: `zcn.xpcustomizer`
- Author: ZCN
- Source BuildId: `XPC-RW16-4CC-T04-B1-B1-PNL1-HF1-RU1-GTL1-FLR1-R1-20261115`
- Repository sync revision: `XPC-GH2-GTL1-FLR1-MIT-R1-20261115` (12 updated C# files from the verified current workspace)
- [Change notes](CHANGELOG.md) · [GH2 verification](docs/GH2_UPDATE_VERIFICATION.md) · [GH1 historical record](docs/GH1_CLEANUP_REPORT.md)

**License:** [MIT License](LICENSE) · Copyright (c) 2026 ZCN. This license covers only original work that ZCN is authorized to license; RimWorld, Harmony, and third-party materials retain their own licenses.
