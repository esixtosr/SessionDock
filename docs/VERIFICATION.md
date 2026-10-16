# Verification report

## Current result — Windows CI evidence dated 2026-10-16 UTC

**The complete Windows CI run passed:** compilation, **101 logic/persistence tests**, generated interop checks, **31 native option operations**, two-control activation, temporary credential-vault write/read/delete, published-app UI checks, and fresh per-user installation/uninstallation checks.

Evidence: [Windows preview checks run 38840477301](https://github.com/esixtosr/SessionDock/actions/runs/38840477301), source commit [`8a1f7c9`](https://github.com/esixtosr/SessionDock/commit/8a1f7c9925a96489e7e8bc740488b0f04822acf3). The hosted runner used the `windows-2025` image, Windows build **26100**, x64. Both activated controls reported version **10.0.26100**. These results cover that source revision on the hosted runner. Each distributed tag runs the same checks again. The [preview release notes](https://github.com/esixtosr/SessionDock/releases/tag/v0.1.0-preview.1) link to its exact tag run and provide the download checksums.

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
- Exact folder-only sharing/read-only enforcement, directional clipboard, mouse swap, cameras, multi-monitor, remote DPI negotiation, session screenshots, hotkey/startup, interpolation/UDP/proxy overrides, and automatic injected Ctrl+Alt+Delete are unavailable. Native Ctrl+Alt+End is documented. No whole-drive sharing substitutes for selected-folder access.
- Certificate authentication is strict; an untrusted or mismatched lab certificate can be rejected. No automatic trust bypass is supplied. Certificate behavior during actual connections remains untested.
- Smart cards/printers/microphone/clipboard/audio/gateway/admin/display options are requests subject to host policy. A checked control or accepted setter is not proof of a remote effect.
- WPF/WinForms airspace, keyboard routing, DPI, focus, full-screen exit, high contrast, and accessibility still require interactive tests. Work-area clamping and scrolling exist; the single settings capture and UI smoke are not a complete visual/accessibility pass.
- Card creation is eager. Large-library performance is not benchmarked.
- Fresh unpackaged per-user installation/uninstallation passed on the disposable runner. Upgrades, Windows 11 compatibility, real database/credential preservation, MSIX, and signing remain unverified. No MSIX was built or signed; scripts do not install a trust certificate or change firewall/startup settings.
- No trusted update endpoint, cloud/Teams integration, or optional outbound diagnostics service is configured. Local diagnostic events exclude endpoints, usernames, and passwords.

## Next evidence

For each release, rerun the [Windows workflow](https://github.com/esixtosr/SessionDock/actions/workflows/windows.yml) at its tag and verify its public assets and checksums. For tester feedback, follow [Windows acceptance](WINDOWS-ACCEPTANCE.md) on Windows 11 with authorized hosts: prompted/saved login, two sessions, input, deliberate disconnect, then remote options and upgrades. Supply the 19 images for side-by-side review. Use host aliases and error codes in reports, never passwords or private session images.
