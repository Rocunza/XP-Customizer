# Known issues and release status

This file reports what is supported by the attached source, prior in-game logs, or the repository audit. It does not represent passing end-to-end game tests.

## Profile application failure observed in a Workshop-subscribed install — unconfirmed fix

- **Observed in RimWorld 1.6.4871:** Long and Very Long profile files both loaded, the apply preflight returned `ReadyChanged`, and preparation returned `Prepared`.
- On confirmation the production apply path returned `status=Rejected` and `reason=EXCEPTION_BEFORE_MUTATION_InvalidOperationException` for both profiles.
- Startup settings XML audit also returned `NOT_VERIFIED` with `InvalidOperationException`.
- The evidence suggests a settings XML path/existence/validation issue **but does not prove which subcase caused the exception**. The cause needs isolation and repair before a stable release.
- The log records failure before mutation; do not interpret this as a successful profile apply.

**Source BuildId in failing log:** `XPC-RW16-4CC-T04-B1-B1-PNL1-HF1-RU1-R1-20261115`.

**Later, separate test:** A Debug build successfully applied a profile, verified selected mod-settings XML fields, and completed with `WrittenVerifiedNoRestart` in a different game environment. This does **not** reproduce the original clean Workshop-subscription situation and does **not** close the issue. Confirm clean-install XML path, apply, save/restart, and both default profiles before claiming a fix.

## 1.7 support not fully certified

This snapshot contains a **RimWorld 1.6 .NET Framework 4.7.2 project**. An identical DLL has been used in a RimWorld 1.7 deployment, but there is no separately built 1.7 project in this repository; complete compatibility testing is outstanding.

## About metadata differs from some Workshop packaging

`XpCustomizer/Packaging/About/About.xml` currently declares RimWorld **1.6** only. A separately supplied Workshop package has advertised 1.6 and 1.7. Validate metadata and the actual binaries before packaging; source cleanup did not silently rewrite metadata.

## GH2 is source synchronization, not a new validated release

GH2 includes already-tested Debug changes GTL1 and FLR1, but this ZIP is a *source-only* GitHub export, not an installable mod. The current Release binary must be rebuilt separately, and RimWorld 1.6/1.7 behavior requires separate testing. Static checks verify file integrity, project references, XML/JSON syntax, and absence of excluded files; they do not prove compilation or runtime compatibility.
