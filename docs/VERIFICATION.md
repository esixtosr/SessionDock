# Verification report — 2026-10-14

## Overall status

**An implemented, compiled Windows preview; not a validated Windows release or full PC parity.** The user will run Windows tests and provide feedback. No Windows runtime, RDP logon, gateway, device-redirection or installed-package result has been fabricated. No external host was contacted for RDP testing.

## Observed environment

- Empty local project at start: AGENTS.md and empty read-only `sources/`; no existing application repository to modify.
- Host: macOS / Darwin ARM64. Installed SDK: .NET 10.0.201. Portable tests ran on .NET 10.0.5.
- Application target: `net10.0-windows`, win-x64, self-contained runtime 10.0.13. SDK and NuGet versions/locks are recorded in source. The production compiler servicing baseline still needs a Windows review.
- Full supplied text specification read. The 19 screenshot files were not accessible; the inventory is text-derived and visual comparison remains blocked.
- The original specification and synced project references were left unchanged. No user machines/credentials or screenshot contents are seeded in the product.

## Completed checks

| Check | Observed result | Limit |
|---|---|---|
| Locked NuGet restore | Passed after aligning declared runtime identifiers across projects | Does not prove dependency behavior |
| Six-project solution Release build | Passed, **0 warnings / 0 errors** | macOS cross-compilation, not Windows execution |
| Portable automated suite | **101 passed, 0 failed, 0 skipped** | Pure logic and real SQLite; no Windows RDP/vault calls |
| Self-contained Windows x64 publish | Produced SessionDock.exe and dependency/runtime files; PE x64 executable/native SQLite confirmed; both bundled runtimes 10.0.13 | Has not launched on Windows |
| NuGet advisory query (including transitive packages) | No known vulnerable packages reported for any of the six projects | Advisory-feed result at check time, not a security guarantee |
| Source safety review | No mstsc launcher substitute; no imported commands; no auto-connect; secrets not in models/export | Runtime credential/control behavior still requires Windows proof |

Tests cover endpoint parsing/ranges, detached drafts/defaults, 23 presets, allowlisted RDP import/export, malformed application imports, secret-reference remapping, actual SQLite restart/migration/backup, newer-schema refusal, foreign-key rollback, shared-reference deletion, atomic PC+gateway saves, login timestamp regression, session-state rules, backend option mapping and redacted failure classification. The test source links the pure option/error-mapping code used by the Windows adapter.

Machine-readable portable result: `artifacts/TestResults/portable-macos.trx` in the source handoff.

## Milestone results

| Milestone | What is implemented | Test evidence | Remaining gate |
|---|---|---|---|
| 0 — Engine | STA-owned integrated AxHost, native connection/events, credential BSTR handoff, bounded reconnect, security settings, Windows interop generation/probe | Adapter compiles; pure mapping tests pass | Real Windows logon/input, two simultaneous sessions, generated ABI, installed activation; engine parity decision provisional |
| 1 — Library | SQLite, groups/favorites/search/grid/list/sorting, PC CRUD, migrations/settings | Logic/persistence tests pass; native source compiles | Windows UIA/render/Cancel interaction, large-library performance |
| 2 — MVP | Prompt/vault path, independent sessions, event status, connect/focus/disconnect, basic options | Compiler + state/mapping tests | Prompted/saved logon, errors, reconnect cancellation and remote effects |
| 3 — Managers/settings | Four-tab PC form, nine categories, credentials/gateways/groups/resolutions, defaults, reviewed imports | Metadata/reference/atomic import tests | Windows vault/UI and all claimed remote option effects |
| 4 — Workflow/parity | Charcoal native shell, native titlebars, fallback cards, tray, notices, theme/high-contrast resources, work-area sizing | Source review and compilation | All 19 image inspection, Windows accessibility/DPI; folder/read-only/one-way clipboard/camera/mouse/monitor/capture gaps |
| 5 — Release preparation | Unsigned self-contained preview, per-user install scripts, WAP/MSIX source, CI, guides and acceptance probes | Windows PE/payload/archive inspection recorded with handoff | Actual install/upgrade/uninstall, MSIX build/signing, clean-machine acceptance; no publication |
| 6 — Cloud | Apps explains future scope | Source inspection | Intentionally deferred |

## Explicit limitations

- The preview contains **no Windows-generated interop DLL** because no Windows generator was available here. Windowed RDP is implemented without an unexplained binary. Native full screen is capability-gated until the Windows build generates/validates the required wrapper.
- Exact folder-only sharing/read-only enforcement, directional clipboard, mouse swap, cameras, multi-monitor, remote DPI negotiation, screenshots, hotkey/startup, interpolation/UDP/proxy overrides and automatic injected Ctrl+Alt+Delete are unavailable. Native Ctrl+Alt+End is documented. No broad drive redirection substitutes for folder sharing.
- Certificate authentication is strict; a self-signed/untrusted lab host can be rejected. No automatic trust bypass is supplied.
- Smart cards/printers/microphone/clipboard/audio/gateway/admin/display options are mapped requests. A selected checkbox is not evidence of policy-approved remote operation.
- WPF/WinForms airspace, native event ABI, keyboard routing, DPI, focus and full-screen exit require Windows tests. Work-area clamping and scrolling are implemented, but no visual/accessibility pass is claimed.
- Card creation is currently eager. Large-library performance is not benchmarked and remains a release gate.
- MSIX project/scripts exist; **no MSIX package was built or signed**. The per-user installer is also unexecuted on Windows. The preview is unsigned; no trust certificate or firewall/startup change is installed by scripts.
- No trusted update endpoint or cloud/Teams integration is configured. Optional outbound diagnostics do not exist; local diagnostic events exclude endpoints/usernames/passwords.

## Next evidence to collect

Follow WINDOWS-ACCEPTANCE.md. Start with launch, profile persistence, prompted/saved login, two sessions and deliberate disconnect. Use `scripts/Test-Engine.ps1` and `scripts/Test-UI.ps1` for explicit no-network probes. Supply the 19 images for the missing side-by-side review. Report failures with codes and host aliases rather than passwords or private session images.
