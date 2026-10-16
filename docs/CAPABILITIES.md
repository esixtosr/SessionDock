# Capability and proof matrix

Statuses mean **Supported** = demonstrated in the stated test, **Conditional** = source/API exists but Windows/client/host/policy proof required, **Unavailable** = disabled and not supplied by this build, **Deferred** = outside the saved-PC release. No row without Windows evidence claims remote support.

| Feature | Status | Entry point / implementation | Observed result |
|---|---|---|---|
| Offline endpoint validation | Supported (portable) | EndpointParser, ProfileValidator | Automated tests on macOS |
| Persistence and reference integrity | Supported (portable) | Microsoft.Data.Sqlite, transactions, migrations | Restart/backup/migration/FK tests on macOS |
| Native WPF library/settings | Conditional | WPF controls, MVVM library state | Cross-compilation; Windows rendering untested |
| Integrated RDP logon | Conditional | AxHost + MsTscAx Connect, OnLoginComplete | Compiled; no Windows logon observed |
| Multiple sessions | Conditional | One STA-owned control per window | No-network dual activation probe prepared; remote sessions untested |
| Protected password storage | Conditional | CredWriteW/ReadW/DeleteW/CredFree, scoped generic target | Source reviewed; Windows vault probe required |
| Certificate/NLA protection | Conditional | AuthenticationLevel=1, EnableCredSspSupport | Required by option tests; trusted-host behavior untested |
| Automatic reconnect | Conditional | EnableAutoReconnect, OnAutoReconnecting | Bounded mapping/state tests; interruption and cancellation untested |
| Admin session | Conditional | ConnectToAdministerServer | API request mapped; no privilege escalation implied; remote effect untested |
| Fixed resolution / image scaling | Conditional | DesktopWidth/Height, SmartSizing | Mapping tests; remote effect untested |
| Dynamic resize | Conditional | UpdateSessionDisplaySettings, debounce | Failure retains session; host response untested |
| Full screen / connection bar | Conditional | FullScreen and container layout; generated non-scriptable interop where required | Windows focus/keyboard/native bar proof required |
| Windows/Alt key routing | Conditional | KeyboardHookMode | Mapping tests; input behavior untested |
| Ctrl+Alt+Delete button injection | Unavailable | Native Ctrl+Alt+End instruction provided | No injected key claim |
| RD Gateway and bypass | Conditional | TransportSettings, GatewayHostname/UsageMethod/CredsSource | Mapping source; real gateway/HTTPS/proxy/auth tests needed |
| Clipboard disabled/bidirectional | Conditional | RedirectClipboard | Mapping tests; both remote directions untested |
| One-way clipboard | Unavailable | IMsRdpClipboard investigation pending | Usual flag does not prove enforceable one-way behavior |
| Local/remote/off sound | Conditional | AudioRedirectionMode | Mapping tests; remote audio untested |
| Microphone / printers / smart cards | Conditional | AudioCaptureRedirectionMode, RedirectPrinters/SmartCards | Mapping; policy/device tests needed |
| Cameras | Unavailable | Disabled capability | No camera device exposed intentionally |
| Selected folders / read-only | Unavailable | No folder backend; drive redirection forced off | No broader drive access substituted |
| Session mouse swap | Unavailable | No global SwapMouseButton call | Session-local mechanism still needed |
| Multi-monitor / remote DPI | Unavailable | Requires non-scriptable interface + Windows/host proof | Disabled; ordinary local DPI layout still needs QA |
| Thumbnails | Unavailable | Original static fallback cards only | No screen capture API called |
| Tray launcher | Conditional | Native NotifyIcon | Source compiled; tray/exit test needed |
| Global launcher hotkey / startup | Unavailable | Not registered | No decorative enabled controls |
| Theme, high contrast and accessibility | Conditional | Native system resources, semantic labels | Windows visual/UIA tests needed |
| HTTP proxy / UDP override / interpolation | Unavailable | Uses installed control behavior; no override claimed | Disabled controls explain limitation |
| Notifications | Conditional | Local failures and optional tips | No outbound service; Windows UI proof pending |
| RDP/app-profile import/export | Supported (portable) | Strict typed allowlists, review, remapped secret references | Automated logic/security tests |
| Installer and MSIX | Conditional | Per-user copy/shortcut/uninstall, WAP project | Source prepared; no Windows install/MSIX output verified |
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
