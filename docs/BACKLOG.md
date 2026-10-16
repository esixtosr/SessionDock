# SessionDock implementation backlog

Status key: `SOURCE` implemented source, awaiting listed verification; `PASS` evidence recorded in VERIFICATION.md; `OPEN` remaining work; `BLOCKED` external prerequisite; `DEFERRED` intentionally outside this PC release. A phase is not complete merely because source exists.

## Milestone 0 — Windows engine foundation

| ID | Work and dependency | Exit evidence | Status |
|---|---|---|---|
| E01 | Integrated AxHost RDP surface, UI-thread ownership, event sink | Windows x64 control activation; NLA logon; keyboard/mouse; clean close | SOURCE; control activation/cleanup PASS; real logon/input still required |
| E02 | Reproducible Windows interop generation and ABI check | Generated wrappers from installed Microsoft DLL; build/control versions recorded | PASS for recorded Windows build; see VERIFICATION.md |
| E03 | Actual connection lifecycle; two independent surfaces | OnLoginComplete for both; deliberate disconnect does not reconnect or sign out | SOURCE; interactive tests required |
| E04 | Security decisions, incorrect credentials, reconnect | No bypass; no retry loop after rejection; actionable redacted error | SOURCE; interactive tests required |
| E05 | Capability investigation before claiming parity | Matrix identifies API, host/client/control versions, observed effect | SOURCE; no remote effects verified |
| E06 | Exact folders/read-only, directional clipboard, mouse swap, capture | Enforce scope and policy under adversarial remote read/write/input tests | OPEN; disabled in current engine |
| E07 | Installed activation and vault access | Test package launches control and reads app-owned credential | SOURCE; installed UI PASS, installed RDP/vault integration still required |

Exit: real-session and installed results plus a proven plan for each parity gap. **Not passed.** ActiveX is the provisional engine, not a completed parity decision. Another engine may be evaluated after E06 evidence, with licensing/deployment review first.

## Milestone 1 — Persistent PC library (depends E01 source, no network needed)

| ID | Work | Exit evidence | Status |
|---|---|---|---|
| L01 | Endpoint parser and profile validation | DNS, IPv4, bracketed IPv6, custom ports; malformed input rejected offline | SOURCE + automated tests |
| L02 | SQLite schema, transactions, versioning and backup | Restart retains profiles/settings; invalid references roll back; migration backup | SOURCE + automated tests |
| L03 | Native navigation, cards/list, selection, CRUD and context actions | Empty first run; both views operate on same records; Cancel discards draft | SOURCE; Windows UI test required |
| L04 | Grouping, search, sorting, favorite and move | Search reveals collapsed matches without losing state; group delete retains PCs | SOURCE + logic tests; Windows UI test required |
| L05 | Scale and accessibility | Large-library timings; keyboard and screen reader; 100/150/200% display scale | OPEN Windows validation |

## Milestone 2 — RDP MVP (depends E01–E04, L01–L04)

| ID | Work | Exit evidence | Status |
|---|---|---|---|
| M01 | Prompted/saved credentials, protected vault | Windows Credential Manager roundtrip; passwords absent from database/export/logs | SOURCE; vault/password handoff PASS; real prompted/saved logon required |
| M02 | Connect/focus windows, event status, saved logon timestamp | Successful logon event only updates active status and last-connected | SOURCE; Windows integration required |
| M03 | Display/clipboard/audio and full-screen return | Demonstrated remote effects, keyboard escape and reconnect/disconnect behavior | SOURCE; Windows integration required |
| M04 | Failure coverage and cancellation | Bad credentials, DNS, offline, cert, network loss, sleep/resume | OPEN interactive acceptance |

## Milestone 3 — Configuration and shared managers (depends M01–M04)

| ID | Work | Exit evidence | Status |
|---|---|---|---|
| C01 | Four-tab editor with atomic Save and detached drafts | All fields retained; running sessions use frozen connection-time options | SOURCE; Windows UI test required |
| C02 | Nine settings categories and back/forward | Persist functional choices; disable unsupported choices with explanation | SOURCE; nine-category navigation PASS; broader settings/history tests required |
| C03 | Credentials/groups/gateway managers | Stable-ID edits; confirmed deletion identifies affected PCs; explicit clear password | SOURCE; vault primitive PASS; editor flows still require Windows tests |
| C04 | Resolution manager and defaults | 23 presets; uniqueness/range checks; removed preset does not erase PC dimensions | SOURCE + automated tests |
| C05 | Gateway, admin mode, redirection, DPI, multi-monitor | Real remote effects on each target Windows version and policy | SOURCE where mapped; otherwise OPEN capability gap |
| C06 | Shortcuts and launcher hotkey | Ctrl+C reaches terminal; Windows/Alt routes correctly; hotkey conflicts handled | Keyboard mapping SOURCE; launcher hotkey OPEN |

## Milestone 4 — Reference workflow and parity (depends C01–C06)

| ID | Work | Exit evidence | Status |
|---|---|---|---|
| P01 | Inspect and compare all 19 screenshots | Per-image differences in SCREENSHOT-MATRIX.md and side-by-side Windows captures | BLOCKED: images not supplied in accessible files |
| P02 | Cache-only remote-surface thumbnails | No local desktop pixels under overlap/minimize; opt-in and complete deletion | IMPLEMENTED, conditional; synthetic overlap/privacy/storage probe passed; real RDP rendering and full-screen/minimize acceptance pending |
| P03 | Tray, local notices, theme, full-screen bar | Exit behavior with active sessions; no fake updater; contrast/scaling checks | SOURCE for available options; Windows tests required |
| P04 | Exact folders/read-only, directional clipboard, session mouse swap | E06 proof plus backend implementation and UI tests | OPEN parity gates |
| P05 | Responsiveness and native accessibility | 1280×720; text/display scaling; Narrator; high contrast; substantial sample library | OPEN Windows validation |

## Milestone 5 — Installable release (depends all claimed features passing)

| ID | Work | Exit evidence | Status |
|---|---|---|---|
| R01 | Reproducible solution, dependency locks, Windows CI | Clean Windows restore/build/tests; dependency audit; source/binary hashes | PASS for recorded Windows build; see VERIFICATION.md |
| R02 | Portable build, per-user installer, MSIX packaging project | Fresh installation, upgrade, uninstall; no silent certificate trust changes | SOURCE; extracted-ZIP fresh install/uninstall PASS; upgrade/MSIX OPEN |
| R03 | Import/export and recovery | Strict allowlist, review, no auto-connect or passwords; backup reopens | SOURCE + automated tests |
| R04 | Setup, user guide, notices and acceptance report | User can build/install/connect/disconnect/restart and report evidence | SOURCE; user validation pending |
| R05 | Release signing/security update baseline | Supported patched SDK/runtime, trusted signature, clean-machine acceptance | OPEN; unsigned public testing preview; production signing and clean Windows 11 acceptance remain |

## Milestone 6 — Beyond saved PCs

Workspace feeds, identity/token storage, AVD/Windows 365, RemoteApps, Teams optimization and enterprise media integrations are **DEFERRED**. Apps navigation explains this. No fake resources or third-party wallpaper assets ship.

## User testing arrangement

The user and a friend will test real connections on Windows 11 and provide feedback. Hosted Windows build, no-network control/vault/UI, and fresh install/uninstall checks passed; see VERIFICATION.md. They do not close real-session, Windows 11, upgrade, accessibility, or full parity gates. Screenshot file paths remain requested. The original text specification is external reference material and is not modified.
