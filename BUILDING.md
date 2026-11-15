# Building XP Customizer from source

## Prerequisites

- Windows and Visual Studio 2022 with **.NET Framework 4.7.2 targeting pack**.
- A local **RimWorld 1.6** installation and its assemblies (`Assembly-CSharp.dll`, Unity modules).
- The matching Harmony mod installation and `0Harmony.dll` for RimWorld 1.6.
- Open `XpCustomizer.sln` (this is an **XP Customizer-only** solution).

## Local dependency paths

Choose one of these options:

1. Copy `XpCustomizer/Local.BuildPaths.props.example` to `XpCustomizer/Local.BuildPaths.props`; edit `RimWorld16GameDir` and `Harmony16DllPath` to real **local paths**.
2. Alternatively, copy `RimWorld.local.props.example` to `RimWorld.local.props` in the repository root and edit `RimWorldDir` and `HarmonyDllPath`. The `.csproj` imports it from its parent directory if present.

`Local.BuildPaths.props` is imported after the root file, so project-specific values can override root settings. Both local props filenames are explicitly excluded by `.gitignore` (`RimWorld.local.props`, `**/Local.BuildPaths.props`) and must remain private. No game DLL or Harmony DLL is checked into this repository.

## Release build

1. Launch Visual Studio 2022 and open `XpCustomizer.sln`.
2. Choose **Release | Any CPU**.
3. Build the solution. The project uses .NET Framework **4.7.2**.
4. Expected output: `XpCustomizer/bin/Release/XpCustomizer.dll`.
5. Verify the game startup log has `BUILD_ID=XPC-RW16-4CC-T04-B1-B1-PNL1-HF1-RU1-GTL1-FLR1-R1-20261115` before evaluating behavior.

This GH2 source ZIP was **not compiled or game-tested in this preparation environment**. GTL1 and FLR1 have been observed in Debug game logs elsewhere, but that does **not** verify this ZIP's Release build. Do not deploy a prior DLL and assume it matches this source without checking its BuildId and hash.

## Deployment and verification

This repository is source-only. **Do not install the `XpCustomizer/` source directory as a game mod.** A game-installable package must contain valid `About/About.xml`, the applicable `1.6/Assemblies/XpCustomizer.dll` (and an independently validated 1.7 arrangement if advertised), `Languages/English/Keyed`, `Languages/Korean/Keyed`, and `Presets/long.json` + `Presets/verylong.json`. Check the full install ZIP after packaging.

`XpCustomizer/Packaging/About/About.xml` is a **historical source-side metadata file** currently advertising **1.6 only**; verify it against your current Workshop package and actual supported versions before preparing a release. It is retained byte-for-byte as in the supplied source.

The former all-mods `Tools/Package-Mods.ps1` has been **excluded**. It omitted `Languages/` and `Presets/` for XP Customizer, so it must not be run as a source of production packages.

Check profile application and config persistence on a clean install and a Workshop-subscribed install. Test Korean/English, Long/Very Long, cancel/confirm, XML creation, reopen, and save/restart. Test RimWorld 1.7 independently. The currently reported profile-apply failure is documented in `KNOWN_ISSUES.md`.
