# Windows acceptance handoff

**None of these Windows results has been claimed as passed by the author.** The user will run them and provide feedback. Source/compiler/portable-test evidence is in VERIFICATION.md. Use your own authorized hosts, never addresses visible in reference screenshots.

## First ten checks

1. Record Windows edition/build/x64, host edition/build, .NET/control versions and bundle SHA-256. Use aliases such as Test-PC-A in shared reports. Initial target clients are supported Windows 11 x64; target hosts Windows 11 Pro/Enterprise and Server 2022/2025 with RDP enabled.
2. Extract all files and run `SessionDock.exe`. Confirm an empty library and native titlebar. Report launch failures exactly, with no credentials.
3. Add an offline test profile, edit it, cancel a second edit, close/reopen the application, and verify only saved changes remain. Try grid/list, search, groups, sorting and favorites.
4. Create a shared credential using a test account. Confirm blank-password edit preserves it; explicit clear removes it; deleting the profile warns about all affected references. Do not take screenshots of credential dialogs.
5. Connect to authorized Test-PC-A with prompted credentials. Confirm actual remote desktop keyboard and mouse interaction and **Connected only after sign-in**. Record errors or codes if it fails.
6. Disconnect. Verify the remote account remains signed in and no automatic reconnect begins. Reconnect with saved credentials.
7. Use a deliberately wrong test password once. Confirm failure has an actionable message and does not repeatedly retry. Stop before approaching your account-lockout threshold.
8. Open Test-PC-B simultaneously. Verify both work independently and reconnecting from a library card focuses its existing window.
9. Resize; test full screen and return to local control; test Ctrl+C in a remote terminal, Windows/Alt routing, and Ctrl+Alt+End. Do not consider a resized picture proof of changed remote resolution.
10. Exit all session windows and the tray. Install with `Install.cmd`, launch from Start, restart, upgrade from an extracted bundle and uninstall. Confirm settings/credential retention follows the guide. These are separate from MSIX tests.

## Build and no-network probes

From source, use `Build-and-Test.cmd` or `scripts/Build-Windows.ps1`. Windows tooling generates wrappers from the installed Microsoft DLL and checks the adapter's ABI. No unexplained interop assemblies are downloaded. Run `scripts/Test-Engine.ps1` on an interactive Windows desktop: it activates two controls and exercises a temporary app-owned Credential Manager entry, without connecting to a host. Temporary probe credentials are deleted; no password is printed.

Run `scripts/Test-UI.ps1` after publishing to exercise Add/Cancel, Save/restart persistence and the nine settings categories through Windows UI Automation. It uses an isolated temporary library, does not touch the normal library, uses `.invalid` hostnames and never presses Connect. Retained test metadata can be deleted afterward. These no-network probes passed on the recorded hosted Windows runner; see [verification](VERIFICATION.md). Repeat the relevant checks on the target Windows 11 machine.

Inspect `artifacts/verification/engine-capabilities.json`, `ui-smoke.json`, `artifacts/interop/interop-manifest.json` after generation and `artifacts/TestResults/*.trx`. A no-network probe is not a successful RDP logon test.

## Complete test matrix

| Area | Required checks | Passing evidence |
|---|---|---|
| Certificates and NLA | Trusted certificate, wrong hostname, expired/untrusted certificate, NLA-required host | No silent acceptance/downgrade; trusted case logs on; refusal explained |
| Failures | Nonexistent DNS, offline PC, blocked endpoint, wrong password, locked/expired account, auth cancel | Responsive library; specific status/code; no retry loop |
| Reconnect | Eligible temporary network loss, manual disconnect during retry, failure followed immediately by retry, late control events, sleep/resume | Bounded retries; late login cannot override deliberate disconnect/failure |
| Gateways | Separate gateway credentials, no gateway, required gateway, documented direct-first bypass, unavailable gateway | Observed path/auth; destination and gateway credentials not mixed |
| Display | Preset/custom/automatic, dynamic resize and host rejection, full screen escape, monitor change, 100/150/200% DPI | Remote resolution measured separately from image scaling; session retained on resize rejection |
| Devices/audio | Clipboard disabled; bidirectional copy in each direction; printer/smart-card/mic; sound local/remote/off | Actual remote effects and host-policy restrictions recorded |
| Sharing gaps | Selected folder/read-only, cameras, directional clipboard, mouse swap, multi-monitor | Current preview must identify unavailable; do not mark parity passed |
| Future read-only proof | Read plus attempted create/modify/rename/delete; sibling and parent listing | All writes fail, reads pass, no broader directory exposed before enabling feature |
| Future capture proof | Obscure/minimize session, put private local windows above it, disable/delete cache | Only app-owned remote pixels; fallback when impossible; no capture when disabled |
| Persistence | Delete referenced credentials/gateway/group; preset deletion; export/import; backup recovery; upgrade schema | No lost PCs, missing-secret prompt, explicit direct-mode change, transactional recovery |
| Import security | Unknown key, password blob, alternate shell, RemoteApp command, invalid ranges, changed-host JSON with old credential ID | Rejection/reporting, no executable actions, no auto-connect, no old vault-secret reuse |
| Accessibility | Tab/Shift+Tab/Enter/Escape; Narrator labels; high contrast; text scaling; 1280×720 at display scales | All content/actions reachable; readable focus/selection; no clipped save controls |
| Large library | At least 1,000 marked development-only records in an isolated test library | Responsive filtering/sort/scroll; no sessions opened or screenshots decoded eagerly |
| Tray and exit | Hide library, active sessions, reopen, explicit Exit, startup disabled | Clear behavior; no orphan process or accidental sign-out |
| MSIX | Build/sign separately, clean install, activation, vault, real login, update, uninstall | Actual package identity/signing status and observations recorded |

## Feedback to provide

```text
SessionDock bundle/build:
Client Windows edition/build/architecture:
Control version (Connection details or engine probe):
Host alias and Windows edition/build:
Installation type: portable / per-user / MSIX
Steps performed:
Expected result:
Observed result and error code:
Passed checks:
Failed / not tested checks:
```

Include only the relevant redacted report. Do not send passwords, tokens, credential database exports or screenshots containing unrelated remote content. A failure report is useful evidence; do not mark a checklist item passed merely because its UI toggle can be selected.

## Certificate policy acceptance: NOT VERIFIED

Run trusted certificate, self-signed, hostname mismatch, changed certificate, invalid credentials, unreachable host and deliberate cancellation in both modes. Record versions/policies, strict rejection, optional native warnings, ownership/visibility, dismissal without premature Connected and no retry loops. Verify restart persistence and unchanged credentials. See [W11PC4-Public procedure](CERTIFICATE-AUTHENTICATION.md#test-w11pc4-public).
