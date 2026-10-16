# Integrated Windows RDP engine

## Verification status

The source cross-compiles for `net10.0-windows` with .NET SDK 10.0.201. [Windows CI run 38840477301](https://github.com/esixtosr/SessionDock/actions/runs/38840477301) on build 26100 passed installed-control interop generation, two-control activation, all 31 native option operations (including nonempty and clearing desktop/gateway password calls), temporary Credential Manager write/read/delete, and repeated disconnect/disposal after parent close. See [verification](VERIFICATION.md) for the exact revision and scope. **Real login, remote input, reconnect, redirection, resizing, and installed RDP/vault integration are unverified. Phase 0 has not passed.** Local control tests do not prove an authenticated remote session.

The implementation is an integrated `AxHost` for the installed `MsRdpClient10NotSafeForScripting` class. It does not start `mstsc.exe`, implement RDP itself, or send data anywhere until the user connects an explicitly saved/import-reviewed PC. Each session owns an independent native surface. The WPF host must put toolbars outside `WindowsFormsHost`; the native child can cover WPF overlays.

## Interop and activation

`RdpSessionControl` is a small authored AxHost wrapper using documented IDispatch properties/methods and an explicitly attributed IDispatch event sink. It avoids hand-authored non-scriptable vtable layouts. Its class ID is `A0C63C30-F08D-4AB4-907C-34905D770C7D`; its event interface IID is `336D5562-EFA8-482E-8CB3-C5C0FC7A7DB6`. Both are documented by Microsoft. This design allows cross-compilation without redistributing unexplained interop DLLs.

On Windows, `scripts/Generate-Interop.ps1` uses the installed, signed `%WINDIR%\System32\mstscax.dll` and Windows SDK `tlbimp.exe`/`aximp.exe` to generate `Interop.MSTSCLib.dll`, `AxInterop.MSTSCLib.dll`, source, and a local provenance manifest. It verifies the authored event DISPIDs and parameter/return types against that generated type library. The production host remains the authored wrapper. Windows builds include the generated `Interop.MSTSCLib.dll` only for safe reflection into the non-scriptable ConnectionBarText property; no non-scriptable vtable is guessed. The cross-compiled preview can attempt windowed connections without this file but explicitly disables native full screen. Wrapper generation passed on the recorded Windows runner and is required in every release workflow. Generated binaries must not be downloaded from arbitrary repositories.

Run from a 64-bit Windows PowerShell 5.1 terminal with the Visual Studio .NET desktop workload and .NET Framework SDK tools installed:

```powershell
.\scripts\Generate-Interop.ps1
.\scripts\Test-Engine.ps1
```

The standalone probe creates two independent controls and disposes them on the STA. It applies the actual preconnection property mapping using synthetic `.invalid` host names, plus nonempty synthetic and empty password setters, and fails if a required setter is rejected. It never calls Connect. Its report lists property names and HRESULTs, never assigned values. Accepted setters do not prove remote effects. Its `artifacts/verification/engine-capabilities.json` report contains OS/control versions and explicit untested capabilities, with no endpoint or account values. It makes no network connection and requires no test host. The same explicit probe also writes one randomly identified app-owned Credential Manager item, verifies a protected-secret round trip without printing the value, deletes it in `finally`, and verifies that it is absent. It does not touch other saved credentials. Its success only proves activation; test the installed package separately. The application requires the Client10 control/security members; it selects the newest available advanced/secured/transport interface and aborts if a requested mapping is unavailable. It does not downgrade to an older control or ignore failed settings.

## Connection lifecycle and credentials

`Connect(profile, credentials, gateway, gatewayCredentials)` must execute after the WPF WindowsFormsHost is loaded and initialized. All calls and disposal require its creating STA/UI thread. `Connect` is a native asynchronous connection initiation, so the library remains usable; the native control owns protocol work and authentication prompts.

`OnConnecting` reports Connecting. `OnConnected` reports Authenticating, **not Connected**. Only `OnLoginComplete` reports a newly authenticated Connected session. `OnAutoReconnected` restores Connected only if this control previously observed login completion. Closing the window disconnects and disposes the client; it does not request remote sign-out.

Automatic reconnect requires a previously logged-on session and is limited to three native attempts. Deliberate disconnect, authentication errors, certificate/security interaction, and fatal errors stop retrying. The reconnect callback explicitly returns the native stop value when retry is forbidden. No application-level credential retry loop exists. Documented disconnect/extended codes map to specific DNS, network, certificate, credential, account, delegation, and licensing messages. Unknown codes stay explicit; no raw COM/server text, host, account, or password is included. Specific DNS/gateway/certificate diagnosis still requires Windows acceptance evidence.

Destination and gateway credentials are separate `SessionCredentials` instances. Passwords never enter the mapping list or process arguments. The password bridge loads Windows’ installed `mstscax.dll` type library without registration. It resolves `IMsTscNonScriptable.ClearTextPassword` and `IMsRdpClientTransportSettings2.GatewayPassword`, validates the interface IID, native HRESULT return, one input BSTR, calling convention, and vtable offset, then queries that exact interface. It passes a temporary BSTR made from `SecureString` directly to the native setter and zero-frees that BSTR in `finally`, without making an immutable managed password string. Offsets come from the installed type library, never a guessed layout or a supplied JSON file. CI independently records the generated interop setter signatures and slots. Unknown signatures fail closed. The control necessarily holds authentication material; SessionDock cannot promise that every native in-memory copy stays encrypted. Password properties are cleared on failure/disconnect/disposal. The caller must dispose credential objects after `Connect` returns.

Strict defaults to AuthenticationLevel=1; optional per-PC Interactive uses level 2 and delegates security decisions to Windows. CredSSP remains enabled. Required setter failure aborts before Connect. Warning display stops retries; dismissal never establishes success. The unused public-key callback still rejects unexpected requests. See [certificate authentication](CERTIFICATE-AUTHENTICATION.md).

## Current mapping and limitations

All remote features below remain **Conditional: local Windows probes passed; remote host behavior is unverified**. A successfully accepted setter is only a request, not evidence of host policy accepting it.

| Feature | Backend request | Status / acceptance still needed |
|---|---|---|
| Integrated session | Native Connect/Disconnect; event connection point | Conditional; login, keyboard/mouse, two sessions |
| Host and port | Server, advanced RDPPort | Conditional; DNS, IPv4, bracketed-input IPv6, non-default port |
| Admin mode | ConnectToAdministerServer | Conditional; does not grant privileges |
| Automatic reconnect | EnableAutoReconnect / reconnect event stop value | Conditional; eligible network loss and every cancellation path |
| Fixed/automatic dimensions | DesktopWidth/DesktopHeight, 200–8192 limit | Conditional; actual negotiated size |
| Fit image to window | SmartSizing | Conditional; separate from remote-resolution changes |
| Dynamic resolution | UpdateSessionDisplaySettings after 300 ms debounce | Conditional; failing request keeps existing session, disables future resize requests and reports fallback |
| Color quality | ColorDepth 16/24/32 | Conditional; actual host result may differ |
| Clipboard | RedirectClipboard boolean | Conditional; separately test both directions and Disabled |
| Printers/smart cards | RedirectPrinters / RedirectSmartCards | Conditional; device + host policy tests |
| Microphone | AudioCaptureRedirectionMode | Conditional; host recording test |
| Sound | AdvancedSettings.AudioRedirectionMode 0/1/2 | Conditional; local/remote/off test |
| Keyboard combinations | SecuredSettings.KeyboardHookMode 0/1/2 | Conditional; local, focused remote, full screen; normal Ctrl keys are not intercepted |
| Gateway | Transport GatewayHostname/UsageMethod/ProfileUsageMethod/CredsSource and separate credentials | Conditional; authorized RD Gateway, failed auth, proxy behavior |
| Gateway bypass | UsageMethod 2 | Native direct-first then gateway behavior; not a private-address heuristic |
| Full screen | Native FullScreen, generated ConnectionBarText, and container-handled full-screen events | Conditional; Ctrl+Alt+Break exit, focus-release shortcuts, DPI |
| Remote security screen | Native focused-session Ctrl+Alt+End | Conditional manual shortcut; toolbar key injection unavailable |
| Exact folders/read-only | No mapping; request rejected | Unavailable; RedirectDrives remains false; no scope expansion |
| One-way clipboard | No automatic-direction mapping | Unavailable; boolean is not one-way policy |
| Multi-monitor | Requires non-scriptable interface and tests | Unavailable in current adapter |
| Remote high DPI | No proven scale-factor negotiation | Unavailable; local application DPI scaling is separate |
| Camera / arbitrary USB | No mapping | Unavailable; device, POS-device, drive and port sharing disabled |
| Per-session mouse swap | No mapping | Unavailable; never calls global SwapMouseButton |
| Thumbnails | No app-owned surface capture implementation | Unavailable; no desktop screenshot substitution |
| UDP/proxy/interpolation preferences | No proven per-control mapping yet | Unavailable; do not advertise saved preferences as effective |
| Cloud/RemoteApps/Teams/Shortpath | No backend integration | Deferred |

## Windows release gate

Record client Windows build, x64, control file version, host OS/build, installation type, requested options, observed effect, and result for each row. Do not put addresses, usernames, credential prompts, or secrets in shared evidence.

1. Generate wrappers and run the activation probe both unpackaged and after installation.
2. Create an authorized Windows 11 Pro/Enterprise or Server 2022/2025 host with trusted RDP certificate. Save a PC and connect with prompted credentials, then saved Credential Manager credentials. Record OnLoginComplete. Test input and reconnect after opening another session.
3. Open two different sessions, focus each, resize, exit full screen using only keyboard, disconnect one, keep the other responsive, close both, reopen, and verify no sign-out request was sent.
4. Use wrong credentials once, cancel authentication, reject an untrusted/changed certificate, interrupt/recover networking, and deliberately disconnect during reconnect. Confirm no repeated credential attempts or resurrection after close.
5. Test dynamic-size success and rejection fallback, 100/150/200% DPI, monitor changes, each sound mode, clipboard in each direction and Disabled, printer/smart-card/microphone policies.
6. Test an authorized RD Gateway with separate credentials, failure, direct-first bypass, and any proxy requirements. Verify direct connections never silently use a saved gateway after changing the profile.
7. Mark remaining parity blockers explicitly. Do not enable folder/read-only, one-way clipboard, screenshots, camera, multiple monitors, high DPI, or mouse swapping based only on property names.

## Primary documentation

- [Microsoft RDP Client10 class/interface and CLSIDs](https://learn.microsoft.com/en-us/windows/win32/termserv/imsrdpclient10)
- [Microsoft event DISPIDs and connection points](https://learn.microsoft.com/en-us/windows/win32/termserv/imstscaxevents-interface)
- [OnLoginComplete](https://learn.microsoft.com/en-us/windows/win32/termserv/imstscaxevents-onlogincomplete)
- [Automatic reconnect callback and stop values](https://learn.microsoft.com/en-us/windows/win32/termserv/-imstscaxevents--onautoreconnecting)
- [Logon-error codes, including non-error arbitration](https://learn.microsoft.com/en-us/windows/win32/termserv/imstscaxevents-onlogonerror)
- [AuthenticationLevel](https://learn.microsoft.com/en-us/windows/win32/termserv/imsrdpclientadvancedsettings4-authenticationlevel)
- [CredSSP](https://learn.microsoft.com/en-us/windows/win32/termserv/imsrdpclientadvancedsettings6-enablecredsspsupport)
- [Gateway usage and bypass](https://learn.microsoft.com/en-us/windows/win32/termserv/imsrdpclienttransportsettings-gatewayusagemethod)
- [Gateway credential source](https://learn.microsoft.com/en-us/windows/win32/termserv/imsrdpclienttransportsettings-gatewaycredssource)
- [Dynamic display request](https://learn.microsoft.com/en-us/previous-versions/windows/desktop/legacy/mt703457(v=vs.85))
- [Container-handled full screen](https://learn.microsoft.com/en-us/windows/win32/termserv/imstscadvancedsettings-containerhandledfullscreen)
- [Required ConnectionBarText before FullScreen](https://learn.microsoft.com/en-us/windows/win32/termserv/imsrdpclient-fullscreen)
- [Documented disconnect reasons](https://learn.microsoft.com/en-us/windows/win32/termserv/imstscaxevents-ondisconnected)
- [Extended disconnect reasons](https://learn.microsoft.com/en-us/windows/win32/termserv/extendeddisconnectreasoncode)
- [WPF ActiveX hosting](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/walkthrough-hosting-an-activex-control-in-wpf)

- [Native desktop password setter](https://learn.microsoft.com/en-us/windows/win32/termserv/imstscnonscriptable-cleartextpassword)
- [Native gateway password setter](https://learn.microsoft.com/en-us/windows/win32/termserv/imsrdpclienttransportsettings2-gatewaypassword)
- [Native dual-interface type description](https://learn.microsoft.com/en-us/windows/win32/api/oaidl/nf-oaidl-itypeinfo-getreftypeofimpltype)
- [Native function signature and vtable offset metadata](https://learn.microsoft.com/en-us/windows/win32/api/oaidl/ns-oaidl-funcdesc)
