# Verification report

## Current result — Windows CI evidence dated 2026-10-16 UTC

**The Windows build, 101 portable tests, generated event ABI checks, two-control activation, and temporary credential-vault round trip passed. The preconnection option probe failed at password setters with `0x80020003`.** Publishing and the application UI smoke test were not reached in this run. A password-setter fix is in progress and has not yet passed a new Windows run.

Evidence: [Windows preview checks run 38838939088](https://github.com/esixtosr/SessionDock/actions/runs/38838939088), source commit [`72b5448`](https://github.com/esixtosr/SessionDock/commit/72b5448). The hosted runner used the `windows-2025` image, Windows build **26100**, x64. Both activated controls reported version **10.0.26100**. The public [source repository](https://github.com/esixtosr/SessionDock) exists; a downloadable prerelease has not yet been published.

These results establish the named checks on that hosted Windows environment. They do not establish Windows 11 client compatibility, successful RDP login, remote input/redirection, full-screen behavior, or installed-package behavior. No external RDP host was contacted.

| Check | Result for run 38838939088 / commit 72b5448 | What remains |
|---|---|---|
| Locked restore and Windows Release build | Passed on the Windows 2025 hosted runner | Target Windows 11 and installed-build acceptance |
| Portable logic and SQLite suite | **101 passed, 0 failed, 0 skipped** on Windows | Does not exercise remote sessions or application credential dialogs |
| Windows-generated interop and event ABI | Passed against the installed Windows control | Repeat when the control/tooling baseline changes; runtime session-event behavior still needs real connections |
| Two independent ActiveX controls | Both activated; each reported **10.0.26100** | Two simultaneous authenticated sessions, input, disconnect, and cleanup |
| Temporary application-owned Credential Manager item | Write/read round trip and deletion passed | Saved-credential editor, prompted/saved RDP login, and installed-package access |
| Native preconnection option setters | **Failed at password setters: `0x80020003`** | Fix and rerun the exact Windows probe; no passing password handoff is claimed |
| Self-contained Windows publish in this run | **Not reached** because the engine probe failed | Produce and inspect the Windows-built release bundle after the probe passes |
| Published application UI Automation smoke test | **Not reached** | Application launch, Cancel, Save, restart persistence, and nine settings categories |
| Preview archive and checksum preparation | **Not reached** | Package the passing Windows output and verify the release assets |
| Real RDP, gateways, device redirection, full screen | **Not tested** | Authorized interactive hosts and the acceptance checklist |
| Per-user installation / upgrade / uninstall / MSIX | **Not tested** | Separate Windows installation and signing checks |

The run's **Windows-verification** artifact is the intended location for portable test results, `engine-capabilities.json`, and interop provenance. Consult the [run page](https://github.com/esixtosr/SessionDock/actions/runs/38838939088) and its step logs for that exact source revision; generated test artifacts are not part of the Git source archive. A failed probe is retained as failed evidence even if a later revision fixes it.

Tests cover endpoint parsing/ranges, detached drafts/defaults, 23 presets, allowlisted RDP import/export, malformed application imports, secret-reference remapping, actual SQLite restart/migration/backup, newer-schema refusal, foreign-key rollback, shared-reference deletion, atomic PC+gateway saves, login timestamp regression, session-state rules, backend option mapping, and redacted failure classification. The portable test source links the pure option/error-mapping code used by the Windows adapter; the native probe separately attempts setters on an actual activated control.

## Historical macOS baseline — 2026-10-14

The initial implementation was prepared on macOS / Darwin ARM64 with .NET SDK **10.0.201**. Portable tests ran on **.NET 10.0.5**. The six-project Release build passed with **0 warnings and 0 errors**, and **101 portable tests passed**. This was cross-compilation evidence, not Windows execution.

The initial self-contained Windows x64 output contained a PE x64 executable, native SQLite, and .NET/Windows Desktop runtime **10.0.13**. It did not contain Windows-generated interop. That historical output is separate from the planned Windows CI release bundle; it is not evidence that the published application launched or authenticated on Windows.

The initial NuGet advisory query reported no known vulnerable packages, including transitive packages, across the six projects. That is a feed result at the time of the check, not a security guarantee. Source review found no `mstsc.exe` launcher substitute, imported command execution, automatic host connection, network scan, or password fields in saved profiles/exports.

The project began without an existing application repository. The full text specification was read, but the 19 reference image files were not accessible. The screenshot inventory remains text-derived. Original reference files were left unchanged, and no personal machines, credentials, screenshot addresses, or commands were seeded into the product.

## Milestone status

| Milestone | Implemented work | Evidence so far | Remaining gate |
|---|---|---|---|
| 0 — Engine | STA-owned integrated AxHost, native events, reconnect controls, option mapping, interop generation/probes | Windows compilation, generated ABI, dual activation, and temporary vault probe passed; password setters failed | Repair password handoff; real logon/input, two remote sessions, installed activation; engine parity decision remains provisional |
| 1 — Library | SQLite, groups/favorites/search/grid/list/sorting, PC CRUD, migrations/settings | Portable logic/persistence tests passed on macOS and Windows | Native application UI smoke, visual review, and large-library performance |
| 2 — MVP | Prompt/vault path, independent session windows, event-derived status, connect/focus/disconnect | State/mapping tests; control activation and vault primitives passed | Password setter probe, prompted/saved logon, failure handling, reconnect cancellation, and remote effects |
| 3 — Managers/settings | Four-tab PC form, nine categories, shared managers, defaults, reviewed imports | Metadata/reference/atomic import tests; temporary vault operations passed | Credential/editor UI and all claimed remote option effects |
| 4 — Workflow/parity | Native shell/titlebars, fallback cards, tray, notices, themes, work-area sizing | Source review and compilation | All 19 image inspection, Windows accessibility/DPI; folder/read-only/one-way clipboard/camera/mouse/monitor/capture gaps |
| 5 — Release preparation | Public source, build/packaging scripts, per-user installer, WAP/MSIX source, CI, guides | First Windows CI run reached and exposed a native setter failure | Passing exact-source Windows run, published assets, install/upgrade/uninstall, MSIX build/signing, clean-machine acceptance |
| 6 — Cloud | Apps explains future scope | Source inspection | Intentionally deferred |

## Remaining limitations

- The current Windows probe has an unresolved password-setter failure. Do not describe RDP authentication as working until the corrected code passes its native probe and authorized real login tests.
- Windows interop was generated and its event identifiers/signatures checked in CI. This removes the earlier lack of generation evidence for that run; it does not prove native full-screen behavior or real event sequences. The eventual release bundle must be inspected for the generated wrapper.
- Exact folder-only sharing/read-only enforcement, directional clipboard, mouse swap, cameras, multi-monitor, remote DPI negotiation, screenshots, hotkey/startup, interpolation/UDP/proxy overrides, and automatic injected Ctrl+Alt+Delete are unavailable. Native Ctrl+Alt+End is documented. No whole-drive sharing substitutes for selected-folder access.
- Certificate authentication is strict; an untrusted or mismatched lab certificate can be rejected. No automatic trust bypass is supplied. Certificate behavior during actual connections remains untested.
- Smart cards/printers/microphone/clipboard/audio/gateway/admin/display options are requests subject to host policy. A checked control or accepted setter is not proof of a remote effect.
- WPF/WinForms airspace, keyboard routing, DPI, focus, full-screen exit, high contrast, and accessibility still require Windows tests. Work-area clamping and scrolling exist in source; no visual/accessibility pass is claimed.
- Card creation is eager. Large-library performance is not benchmarked.
- No MSIX was built or signed. Per-user installation is unverified. Signing, deployment, and clean-machine acceptance remain separate from compilation; no script installs a trust certificate or changes firewall/startup settings.
- No trusted update endpoint, cloud/Teams integration, or optional outbound diagnostics service is configured. Local diagnostic events exclude endpoints, usernames, and passwords.

## Next evidence

Fix the password setter failure and rerun CI, then record the new commit, run, exact test results, and output bundle. After the automated gates pass, follow [Windows acceptance](WINDOWS-ACCEPTANCE.md) on Windows 11 with authorized hosts: prompted/saved login, two sessions, input, deliberate disconnect, then the remaining remote and installation checks. Supply the 19 images for side-by-side review. Use host aliases and error codes in reports, never passwords or private session images.
