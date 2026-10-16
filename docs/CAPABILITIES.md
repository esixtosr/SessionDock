# Capability and proof matrix

Statuses mean **Supported** = demonstrated in the stated test, **Conditional** = source/API exists but Windows/client/host/policy proof required, **Unavailable** = disabled and not supplied by this build, **Deferred** = outside the saved-PC release. No row without remote-session evidence claims a remote feature has worked.

Latest recorded Windows evidence: [run 38840477301](https://github.com/esixtosr/SessionDock/actions/runs/38840477301), commit `8a1f7c9`, dated **2026-10-16 UTC**, Windows 2025 hosted runner/build 26100 x64. The complete run passed: generated ABI checks, **101 portable tests**, two controls reporting 10.0.26100, **31 native option operations** including desktop/gateway password set and clear, temporary vault round trip/deletion, control disposal, published UI smoke (**4 checks**), fresh install/uninstall (**7 checks**), and installed UI smoke (**4 checks**). One settings screenshot was reviewed for readability. **No real RDP connection was attempted.** Windows 11 compatibility, remote behavior, upgrades, MSIX, accessibility and reference parity remain unverified. See the [release notes](https://github.com/esixtosr/SessionDock/releases/tag/v0.1.0-preview.2) for the distributed tag's workflow and asset checks. See the [verification report](VERIFICATION.md).

| Feature | Status | Entry point / implementation | Observed result |
|---|---|---|---|
| Offline endpoint validation | Supported (portable) | EndpointParser, ProfileValidator | Automated tests on macOS and Windows CI |
| Persistence and reference integrity | Supported (portable) | Microsoft.Data.Sqlite, transactions, migrations | Restart/backup/migration/FK tests on macOS and Windows CI |
| Native WPF library/settings | Conditional | WPF controls, MVVM library state | Published and installed Save/Cancel/restart/settings-category checks passed; one settings capture reviewed; broader UI/DPI/accessibility untested |
| Integrated RDP logon | Conditional | AxHost + MsTscAx Connect, OnLoginComplete | Control activation and desktop password set/clear passed; no remote logon attempted |
| Multiple sessions | Conditional | One STA-owned control per window | Two controls activated and parent-close/repeated disposal passed; two authenticated remote sessions remain untested |
| Protected password storage | Conditional | CredWriteW/ReadW/DeleteW/CredFree, scoped generic target | Temporary write/read/delete passed on Windows CI; credential editor and installed RDP/vault integration untested |
| Certificate/NLA protection | Conditional | AuthenticationLevel=1 default / 2 optional, EnableCredSspSupport | Required setters accepted by the native probe; trusted-host behavior untested |
| Automatic reconnect | Conditional | EnableAutoReconnect, OnAutoReconnecting | Mapping/state tests and preconnection setters passed; remote interruption and cancellation untested |
| Admin session | Conditional | ConnectToAdministerServer | Native setter accepted; no privilege escalation implied; remote effect untested |
| Fixed resolution / image scaling | Conditional | DesktopWidth/Height, SmartSizing | Mapping tests and native setters passed; remote effect untested |
| Dynamic resize | Conditional | UpdateSessionDisplaySettings, debounce | Failure retains session; host response untested |
| Full screen / connection bar | Conditional | FullScreen and container layout; generated non-scriptable interop where required | Windows wrapper generation/event ABI and connection-bar setup passed; native full-screen/focus/keyboard behavior untested |
| Windows/Alt key routing | Conditional | KeyboardHookMode | Mapping tests and native setter passed; input behavior untested |
| Ctrl+Alt+Delete button injection | Unavailable | Native Ctrl+Alt+End instruction provided | No injected key claim |
| RD Gateway and bypass | Conditional | TransportSettings, GatewayHostname/UsageMethod/CredsSource | Gateway options and nonempty/clear password setters passed; real gateway/HTTPS/proxy/auth tests remain required |
| Clipboard disabled/bidirectional | Conditional | RedirectClipboard | Native setter accepted; both remote directions untested |
| One-way clipboard | Unavailable | IMsRdpClipboard investigation pending | Usual flag does not prove enforceable one-way behavior |
| Local/remote/off sound | Conditional | AudioRedirectionMode | Native setter accepted; remote audio untested |
| Microphone / printers / smart cards | Conditional | AudioCaptureRedirectionMode, RedirectPrinters/SmartCards | Native setters accepted; remote policy/device tests needed |
| Cameras | Unavailable | Disabled capability | No camera device exposed intentionally |
| Selected folders / read-only | Unavailable | No folder backend; drive redirection forced off | No broader drive access substituted |
| Session mouse swap | Unavailable | No global SwapMouseButton call | Session-local mechanism still needed |
| Multi-monitor / remote DPI | Unavailable | Requires non-scriptable interface + Windows/host proof | Disabled; ordinary local DPI layout still needs QA |
| Thumbnails | Unavailable | Original static fallback cards only | No screen capture API called |
| Tray launcher | Conditional | Native NotifyIcon | Source compiled; tray/exit test needed |
| Global launcher hotkey / startup | Unavailable | Not registered | No decorative enabled controls |
| Theme, high contrast and accessibility | Conditional | Native system resources, semantic labels | Limited UIA and settings readability checks passed; accessibility, high contrast and DPI acceptance outstanding |
| HTTP proxy / UDP override / interpolation | Unavailable | Uses installed control behavior; no override claimed | Disabled controls explain limitation |
| Notifications | Conditional | Local failures and optional tips | No outbound service; Windows UI proof pending |
| RDP/app-profile import/export | Supported (portable) | Strict typed allowlists, review, remapped secret references | Automated logic/security tests passed on macOS and Windows CI |
| Fresh per-user installer / uninstall | Supported (hosted Windows) | Per-user copy/shortcut/uninstall | 7 checks and 4 installed UI checks passed on build 26100; Windows 11, upgrades and real-data/vault preservation unverified |
| MSIX | Conditional | WAP project | No package build, signing or installed behavior verified |
| AVD/Windows 365/workspaces/RemoteApps/Teams | Deferred | Apps explanation only | No cloud resources or auth integration |

## Required evidence fields for each Windows check

Record: client Windows edition/build/architecture; .NET runtime; `MsTscAx.dll` file and control versions; package kind and version; host Windows edition/build; documented API; requested setting; observed remote result; date; pass/fail/blocker. Use host aliases in reports. Do not include passwords, tokens, full credential records or remote desktop captures with private content.

## Primary technical references

- [RDP control interfaces](https://learn.microsoft.com/en-us/windows/win32/termserv/remote-desktop-web-connection-reference)
- [Successful logon event](https://learn.microsoft.com/en-us/windows/win32/termserv/imstscaxevents-onlogincomplete)
- [Credential Manager read operation](https://learn.microsoft.com/en-us/windows/win32/api/wincred/nf-wincred-credreadw)
- [SQLite provider](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/)
- [Windows Application Packaging Project](https://learn.microsoft.com/en-us/windows/msix/desktop/vs-package-overview)

Documentation establishes APIs, not results on the user's machine. Engine notes list additional API references and implementation constraints.
