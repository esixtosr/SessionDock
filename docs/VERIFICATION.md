# Verification report

## Current result — Windows CI evidence dated 2026-10-16 UTC

**The complete Windows CI run passed:** compilation, **101 logic/persistence tests**, generated interop checks, **31 native option operations**, two-control activation, temporary credential-vault write/read/delete, published-app UI checks, and fresh per-user installation/uninstallation checks.

Evidence: [Windows preview checks run 38840477301](https://github.com/esixtosr/SessionDock/actions/runs/38840477301), source commit [`8a1f7c9`](https://github.com/esixtosr/SessionDock/commit/8a1f7c9925a96489e7e8bc740488b0f04822acf3). The hosted runner used the `windows-2025` image, Windows build **26100**, x64. Both activated controls reported version **10.0.26100**. These results cover that source revision on the hosted runner. Each distributed tag runs the same checks again. The [preview release notes](https://github.com/esixtosr/SessionDock/releases/tag/v0.1.0-preview.2) link to its exact tag run and provide the download checksums.

**Real RDP login has not been tested.** No external RDP host was contacted. Hosted Windows Server checks do not establish Windows 11 client compatibility, remote input/redirection, full-screen behavior, upgrades, or MSIX deployment.

| Check | Result for run 38840477301 / commit 8a1f7c9 | What remains |
|---|---|---|
| Locked restore and Windows Release build | Passed | Windows 11 client acceptance |
| Portable logic and SQLite suite | **101 passed, 0 failed, 0 skipped** | Remote sessions and application credential dialogs |
| Windows-generated interop | Event identifiers/signatures and native password-setter metadata passed against the installed control | Real session-event sequences and other control versions |
| Two independent ActiveX controls | Both activated; each reported **10.0.26100** | Two simultaneous authenticated remote sessions and remote input |
| Native preconnection option operations | **31 accepted**, including nonempty and clearing calls for desktop and gateway passwords | Remote authentication and effective host settings |
| Control cleanup | Parent-window closure and repeated disconnect/disposal passed | Cleanup while authenticating, connected, and reconnecting to real hosts |
| Temporary application-owned Credential Manager item | Write/read round trip and deletion passed | Saved-credential editor, prompted/saved RDP login, installed RDP/vault integration |
| Self-contained Windows publish | Passed with generated interop; runtime **10.0.13** | Clean Windows 11 machine; tag-specific evidence is linked in the release notes |
| Published application UI Automation | **4 checks passed**: Cancel, Save without connecting, restart persistence, nine settings categories | Broader forms/workflows, accessibility, DPI and reference parity |
| Settings screenshot | Captured and visually reviewed; displayed controls and text were readable | Other views and display configurations; library capture was skipped because another window overlapped it |
| Preview archive and checksum preparation | Passed; the downloaded CI artifacts passed SHA-256 and ZIP checks; installation used the actual preview ZIP | Tag-specific asset verification is recorded with the release |
| Fresh per-user installation and uninstall | **7 checks passed**, including matching installed file hashes, shortcut/Installed Apps registration, and synthetic user-data retention | Upgrade, real-data/vault preservation, Windows 11, MSIX |
| Installed application UI Automation | Same **4 checks passed** from the installed executable | Installed remote-session and credential behavior |
| Real RDP, gateways, redirection, full screen | **Not tested** | Authorized interactive hosts and the acceptance checklist |

The run's **Windows-verification** artifact contains the test results, `engine-capabilities.json`, `ui-smoke.json`, `install-smoke.json`, `installed-ui-smoke.json`, the settings capture, and interop provenance. **SessionDock-release-files** contains the prepared archives and checksums. Consult the [run page](https://github.com/esixtosr/SessionDock/actions/runs/38840477301) for exact-revision logs and artifacts; these generated outputs are excluded from the Git source archive.

A separate audit of the downloaded CI bundle confirmed the x64 executable, runtime **10.0.13**, generated interop, installer/docs, and source archive commit. No author-specific `/Users/` compiler paths were found in the audited bundle. The release notes separately record verification of the distributed assets.

Tests cover endpoint parsing/ranges, detached drafts/defaults, 23 presets, allowlisted RDP import/export, malformed application imports, secret-reference remapping, actual SQLite restart/migration/backup, newer-schema refusal, foreign-key rollback, shared-reference deletion, atomic PC+gateway saves, login timestamp regression, session-state rules, backend option mapping, and redacted failure classification. The native probe separately calls actual control setters. The installer fixture is a synthetic sentinel, not a real database or saved credential.

Earlier Windows runs exposed a password-dispatch failure and a parent-window disposal failure. Both were corrected and their regression probes passed in the recorded run. An accepted setter proves that the local control accepted a request; it does not prove authentication or a remote effect.

## Historical macOS baseline — 2026-10-14

The initial implementation was prepared on macOS / Darwin ARM64 with .NET SDK **10.0.201**. Portable tests ran on **.NET 10.0.5**. The six-project Release build passed with **0 warnings and 0 errors**, and **101 portable tests passed**. This was cross-compilation evidence, not Windows execution.

The initial self-contained Windows x64 output contained a PE x64 executable, native SQLite, and .NET/Windows Desktop runtime **10.0.13**, but no Windows-generated interop. It is separate from the Windows CI output and is not the release candidate.

The initial NuGet advisory query reported no known vulnerable packages, including transitive packages, across the six projects. That is a feed result at the time of the check, not a security guarantee. Source review found no `mstsc.exe` launcher substitute, imported command execution, automatic host connection, network scan, or password fields in saved profiles/exports.

The project began without an existing application repository. The full text specification was read, but the 19 reference image files were not accessible. The screenshot inventory remains text-derived. Original reference files were left unchanged, and no personal machines, credentials, screenshot addresses, or commands were seeded into the product.

## Milestone status

| Milestone | Implemented work | Evidence so far | Remaining gate |
|---|---|---|---|
| 0 — Engine | STA-owned integrated AxHost, native events, reconnect controls, option mapping, interop generation/probes | Windows ABI, dual activation, 31 native option operations, password/vault and disposal probes passed | Real logon/input, two remote sessions, connected cleanup; parity decision remains provisional |
| 1 — Library | SQLite, groups/favorites/search/grid/list/sorting, PC CRUD, migrations/settings | Logic/persistence suite and published/installed Save/Cancel/restart UI checks passed | Broader CRUD/visual review and large-library performance |
| 2 — MVP | Prompt/vault path, independent session windows, event-derived status, connect/focus/disconnect | State/mapping tests and native password/control/vault primitives passed | Prompted/saved logon, failure handling, reconnect cancellation, remote effects |
| 3 — Managers/settings | Four-tab PC form, nine categories, shared managers, defaults, reviewed imports | Metadata/reference/atomic import tests; settings-category UI checks and settings capture reviewed | Credential/editor flows and claimed remote option effects |
| 4 — Workflow/parity | Native shell/titlebars, fallback cards, tray, notices, themes, work-area sizing | Limited native UI smoke and one settings screenshot reviewed | All 19 references, accessibility/DPI, remaining workflows; unavailable capability gaps |
| 5 — Release preparation | Public source, Windows-built bundle, archives/checksums, per-user installer, WAP/MSIX source, CI, guides | Full recorded Windows CI run and fresh install/uninstall checks passed | Windows 11, upgrades, MSIX/signing, real-host acceptance; release notes track tag/asset checks |
| 6 — Cloud | Apps explains future scope | Source inspection | Intentionally deferred |

## Remaining limitations

- No successful RDP authentication or remote feature has been demonstrated. Windows interop, native password handoff, and credential storage now have local Windows probe evidence, but remote credentials, certificates, event sequences, and installed RDP/vault integration remain unverified.
- Exact folder-only sharing/read-only enforcement, directional clipboard, mouse swap, cameras, multi-monitor, remote DPI negotiation, hotkey/startup, interpolation/UDP/proxy overrides, and automatic injected Ctrl+Alt+Delete are unavailable. Native Ctrl+Alt+End is documented. No whole-drive sharing substitutes for selected-folder access.
- Certificate authentication defaults to Strict; optional per-PC Interactive delegates warnings to Windows; an untrusted or mismatched lab certificate can be rejected. No automatic trust bypass is supplied. Certificate behavior during actual connections remains untested.
- Smart cards/printers/microphone/clipboard/audio/gateway/admin/display options are requests subject to host policy. A checked control or accepted setter is not proof of a remote effect.
- WPF/WinForms airspace, keyboard routing, DPI, focus, full-screen exit, high contrast, and accessibility still require interactive tests. Work-area clamping and scrolling exist; the single settings capture and UI smoke are not a complete visual/accessibility pass.
- Card creation is eager. Large-library performance is not benchmarked.
- Fresh unpackaged per-user installation/uninstallation passed on the disposable runner. Upgrades, Windows 11 compatibility, real database/credential preservation, MSIX, and signing remain unverified. No MSIX was built or signed; scripts do not install a trust certificate or change firewall/startup settings.
- No trusted update endpoint, cloud/Teams integration, or optional outbound diagnostics service is configured. Local diagnostic events exclude endpoints, usernames, and passwords.

## Next evidence

For each release, rerun the [Windows workflow](https://github.com/esixtosr/SessionDock/actions/workflows/windows.yml) at its tag and verify its public assets and checksums. For tester feedback, follow [Windows acceptance](WINDOWS-ACCEPTANCE.md) on Windows 11 with authorized hosts: prompted/saved login, two sessions, input, deliberate disconnect, then remote options and upgrades. Supply the 19 images for side-by-side review. Use host aliases and error codes in reports, never passwords or private session images.

## Certificate authentication feature: local results, 2026-10-16

- Pinned SDK 10.0.201 installed in ignored artifacts. Locked restore passed without dependency changes.
- Release solution build passed: zero warnings/errors. Unit tests: 112 passed, zero failed/skipped. Covers policy mapping, invalid values, CredSSP, no level 0, unchanged options, clone, SQLite restart/legacy payload, JSON and .rdp exchange.
- Windows build 26200 x64, control 10.0.26100: existing no-network probe passed two-control activation, all 32 option operations including both authentication levels, temporary vault round-trip/deletion, and repeated disconnect/disposal. Local report: ignored artifacts/verification/certificate-engine.json.
- Generated interop validation NOT VERIFIED locally: tlbimp.exe/aximp.exe SDK tools unavailable. Dialog-parent metadata validation added to the generation script. Actual warning ownership/visibility NOT VERIFIED; needs generated-wrapper CI and interactive testing.
- Real RDP matrix, in both modes: trusted certificate, self-signed, hostname mismatch, changed certificate, invalid credentials, unreachable host, deliberately cancelled connection: **NOT VERIFIED**. No authorized test server/account supplied. Unit tests and accepted setters do not prove real certificate authentication.
- UI automation, self-contained publish, installation and MSIX not run for this change. Earlier evidence applies only to its recorded revision.
- Published releases/tags, machine-wide policies and existing credentials unchanged.

## Last-session preview implementation: local results, 2026-10-16

- Release solution build: passed with zero warnings/errors. Existing unit tests: 112 passed, zero failed/skipped.
- No-network Windows probe passed on build 26200 / control 10.0.26100: synthetic control capture/bounds, blank and hidden rejection, identical control pixels under a covering magenta test window, DPAPI cache reload with timestamp, separate profile deletion, corrupt-cache fallback and clear. Existing activation, 32 native option operations, temporary vault deletion and disposal checks also passed. Synthetic patterns are not real remote desktops.
- Real RDP capture, loss/close frame timing, native full screen, minimized/locked behavior, and responsiveness: NOT VERIFIED. A renderer returning blank output retains the previous preview or illustration.
- User previously reported successful real Interactive logins, restart persistence, multiple sessions, correct disconnect behavior and everyday controls in their setup. Certificate-warning cancellation was not exercised because no warning appeared; this remains unverified.
- The shorter normal sign-in status preserves detailed warning/failure messages and Connection details.

- Self-contained publish passed. Isolated UI smoke passed all 5 checks, including preview default-off, opt-in persistence after restart and Clear previews disabling capture. Synthetic library/settings screenshots were visually reviewed. An initial framework-dependent UI launch timed out on the installed runtime; the self-contained build resolved it. Full local installation smoke was not run on this personal machine.

## Blank RDP preview follow-up, 2026-10-16

The user reported no preview after opt-in, with the enabled-setting fallback tooltip visible. The PrintWindow-only implementation did not demonstrate usable RDP rendering in that setup. Added app-window-only Windows compositor capture, strict remote-client cropping, stale-result cancellation and capture status in Connection details. The previous synthetic GDI test had passed but was not evidence of real RDP capture.

Local locked restore, Release build (zero warnings/errors), 112 portable tests, self-contained publish, all five isolated UI checks, existing native/vault/storage probes and the new compositor probe passed. The compositor probe verifies actual synthetic blue/gold pixels cropped from a window, excludes a covering magenta window, and rejects a minimized window. The synthetic source title is changed while covered to trigger a new compositor frame; Windows need not supply new frames for unchanged content. A first attempted assertion that a synthetic control refusing WM_PRINT would always make PrintWindow fail was removed because Windows did not behave that way. No actual remote desktop was used.

Real RDP capture after this fix, native full screen, lock/unlock, and responsiveness remain NOT VERIFIED until user testing. Windows projection references intentionally updated; bundled .NET/Windows Desktop runtime remains 10.0.13 (verified in published runtimeconfig). Published releases remain unchanged.

## Version/download follow-up, 2026-10-16

The user confirmed real previews on two PCs and subsequently reported the restart/clear checks working in their environment. This is user-reported acceptance, separate from automated probes.

Added version 0.1.0-preview.3 in the library footer and About, with actual assembly informational version, source build, executable location and manual downloads link. Numbered Windows artifacts contain app files directly; the installer reads the same version from the DLL. Package labels are checked against the published binary. Existing published releases/tags remain unchanged. Validation results for this change follow below.

Local results: Release build passed with zero warnings/errors; all 112 portable tests passed; self-contained publish passed; all six isolated UI checks passed, including About matching the actual DLL version. Packaging rejects mismatched version/build source and dirty tracked source. The Windows CI install smoke now requires Installed Apps registration to match the DLL version exactly. Full install/uninstall remains a disposable-runner check, not executed on this personal machine.

The flat numbered Windows ZIP was inspected: SessionDock.exe, Install.cmd and BUILD-INFO.json are at its root. About and library screenshots were visually reviewed. A repeat UI run failed while looking for Clear previews on a different settings page; the test now waits for each selected page's heading before continuing, and all six checks passed on rerun.

## Portfolio-inspired appearance, preview 4 (2026-10-16)

Inspected edwinsixtos.com in the browser and used its navy background, teal accents, pale headings and muted text as direction for original WPF templates. No website code, fonts or images are bundled. The app uses system Segoe UI and Consolas. Controls retain WPF behavior/automation peers, explicit keyboard focus, disabled states and system high-contrast brush mapping. Light and System appearance remain available. The PC editor retains fixed Save/Cancel actions with scrollable settings.

Local Release build: zero warnings/errors. Portable tests: 112 passed. Self-contained publish and eight isolated UI checks passed, including selecting Interactive through the restyled certificate dropdown and switching Light/Dark. Library, editor, About and light/dark settings captures were visually reviewed. The selection test was corrected to address the dropdown's owning automation peer rather than its separate popup wrapper, and to avoid a PowerShell Unicode label-encoding mismatch.

Static text/accent contrast samples passed 4.5:1: dark muted/input 7.18, dark teal/selected 7.38, light muted/input 6.02, light teal/selected 5.38. These sample checks are not a complete accessibility audit. High contrast, Narrator, high-DPI and full keyboard traversal remain manual acceptance tasks. No real RDP session was started for the appearance update; connection configuration and capture engine are unchanged. Existing releases/tags retained.


## Visual refinement, preview 5 (2026-10-16)

Dedicated full-width search row, compact accessible card actions, 16:9 preview areas, stronger heading hierarchy and an original navigation symbol replace the acronym badge. General settings show usable defaults and storage actions first, with unsupported features retained in a collapsed expander. The native WPF CheckBox behavior and automation peer are retained under a themed template, including checked, indeterminate, disabled and keyboard-focus states.

Local Release build passed with zero warnings/errors. Self-contained publish, all 112 portable tests and eight isolated UI checks passed. Synthetic library, PC editor and settings screenshots were reviewed; the checkbox opt-in, persistence and clear flow passed. A subsequent alignment-only edit extends the search field across its row. No real remote connection, full accessibility audit or personal-machine installation was performed. SessionDock storage/credential identifiers and branding are retained pending name selection; no releases or tags are changed.
