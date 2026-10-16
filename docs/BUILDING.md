# Building and testing SessionDock on Windows

These instructions are for contributors who want to build the application or run its Windows probes. To try a published preview without development tools, follow the download instructions in the [main README](../README.md).

A successful build does not establish that RDP login, device redirection, full screen, or installation works. Record those results separately using the [Windows acceptance checklist](WINDOWS-ACCEPTANCE.md).

The latest recorded [Windows CI run 38840477301](https://github.com/esixtosr/SessionDock/actions/runs/38840477301), commit `8a1f7c9`, passed on **2026-10-16 UTC**: compilation, **101 logic/persistence tests**, generated interop checks, two-control activation, **31 native option operations** including desktop/gateway password set and clear, vault write/read/delete, control disposal, published UI smoke (**4 checks**), fresh install/uninstall (**7 checks**), and installed UI smoke (**4 checks**). This was Windows 2025/build 26100 x64, not Windows 11 or a real remote-session test. Every distributed tag must pass its own [workflow run](https://github.com/esixtosr/SessionDock/actions/workflows/windows.yml); release notes link that exact run. Follow the [verification report](VERIFICATION.md) for exact scope and outstanding checks.

## Requirements

- A currently supported **Windows 11 x64** computer and an interactive Windows desktop. ARM64 and other client versions have separate compatibility work outstanding.
- The **.NET SDK 10.0.201** specified in [global.json](../global.json). SDK roll-forward is disabled, so a different SDK alone does not satisfy the pin.
- Visual Studio with **.NET desktop development**, a version that supports the pinned .NET SDK, and the Windows/.NET Framework SDK tools that provide `tlbimp.exe` and `aximp.exe`.
- **64-bit Windows PowerShell 5.1** for the supplied build and interop scripts.
- Access to NuGet for the initial dependency restore. [NuGet.Config](../NuGet.Config) uses the public NuGet feed; package versions and dependency lock files are included.

The self-contained application separately pins the **.NET and Windows Desktop runtime to 10.0.13**. The SDK version and bundled application runtime are different settings. The runtime is included in the published preview folder; its users do not need the SDK to launch that folder.

For MSIX packaging, also install Visual Studio's **Windows Application Packaging Project** support and **Windows SDK 10.0.26100.0**. These are additional requirements for the packaging project, not for trying the unpackaged preview.

## Build the complete Windows preview

Clone or download the source, extract it if necessary, and open the repository folder. Double-click **Build-and-Test.cmd**, or run this from 64-bit Windows PowerShell in that folder:

```powershell
.\scripts\Build-Windows.ps1
```

The command file starts a PowerShell process with a process-only execution-policy setting; it does not change the machine's saved execution policy.

The build script performs these steps:

1. Records the Windows environment and locates the pinned SDK.
2. Restores the solution with `--locked-mode` and builds the Release configuration.
3. Runs the portable logic, SQLite persistence, import/export, and option-mapping tests.
4. Generates and checks the RDP interop wrappers from the installed Windows control.
5. Runs the Windows control-activation and temporary credential-vault probe.
6. Publishes the self-contained x64 application and copies the installation scripts, guides, and dependency notices into its output folder.

The expected output is `artifacts/SessionDock-win-x64/`. Run `SessionDock.exe` from that folder and keep its DLLs and subfolders together. The build does not start an RDP connection automatically.

If a step fails, resolve the reported error before continuing. Do not call an omitted or failed probe a pass. `-SkipInteropProbe` is available for source-build troubleshooting, but it deliberately skips interop generation and the Windows engine/vault probe; it is not the complete Windows validation path.

## Generate and check the RDP interop separately

From 64-bit Windows PowerShell 5.1:

```powershell
.\scripts\Generate-Interop.ps1
```

The script uses `%WINDIR%\System32\mstscax.dll`, checks its Windows signature, locates `tlbimp.exe` and `aximp.exe`, and generates wrappers under `artifacts/interop/`. It verifies event identifiers/signatures and native password-setter metadata against that installed type library and writes a provenance manifest.

If the tools are installed outside the locations the script searches, supply their actual paths with `-TlbImpPath` and `-AxImpPath`. Do not download unexplained prebuilt interop DLLs or copy the Windows system `MsTscAx.dll` into the application bundle.

The application includes the generated `Interop.MSTSCLib.dll` when it exists at build/publish time. Native full screen additionally requires successful capability setup. Generating a wrapper proves neither control activation nor remote logon. See the [engine notes](ENGINE.md) for the adapter's supported requests and outstanding checks.

## Run the Windows probes

Run the control and vault probe after generating the wrapper:

```powershell
.\scripts\Test-Engine.ps1
```

This probe activates two independent RDP controls on a Windows UI thread, attempts native preconnection option setters (including nonempty and clearing calls for desktop/gateway passwords), and records accepted/rejected operations. It also checks parent-window closure and repeated disconnect/disposal. It also writes, reads, and removes one temporary, application-owned Windows Credential Manager item. It does not connect to a remote host or validate keyboard input, remote resolution, redirection, or installed-package behavior. Dependency restore during the command can contact NuGet; the probe itself makes no RDP connection.

Its report is `artifacts/verification/engine-capabilities.json`. A rejected required setter or temporary credential cleanup failure is a failed probe. Successful activation or vault storage alone does not make the whole probe pass.

After publishing the application, run the UI Automation smoke checks on an interactive desktop:

```powershell
.\scripts\Test-UI.ps1
```

For a different build location, supply `-Executable` with the path to that build's `SessionDock.exe`.

The UI script uses an isolated temporary library. It checks that Cancel discards a draft, a synthetic saved PC survives restart, and all nine settings categories are accessible. It never connects to the synthetic PC. It leaves its temporary test metadata for inspection and writes `artifacts/verification/ui-smoke.json`; the normal user library is not the test destination.

These checks do not replace manual testing of real authorized RDP hosts, accessibility, display scaling, keyboard routing, gateways, or installation. Do not post passwords, private desktop images, or unreviewed diagnostic files with test feedback.

## Build or run the portable tests separately

From the repository folder:

```powershell
dotnet restore SessionDock.slnx --locked-mode
dotnet build SessionDock.slnx -c Release --no-restore
dotnet test tests/SessionDock.Tests/SessionDock.Tests.csproj -c Release --no-build
```

Portable tests exercise actual SQLite storage plus logic and pure adapter mapping. They do not call Windows Credential Manager or establish a remote session. Cross-compilation on another operating system is useful compiler evidence, but it is not Windows runtime evidence.

Dependency changes should intentionally update the affected package versions and lock files. Do not remove locked restore merely to suppress an unexpected dependency change. The GitHub Actions workflow generates interop, restores/builds, runs portable tests, publishes, runs isolated UI smoke checks, prepares archives/checksums, extracts the download and tests its per-user installation/uninstallation, then runs the required native option/control/vault probe. Release artifacts are uploaded only after those checks pass; available verification reports are retained even on failure. This CI order differs from the local build script. Hosted checks remain separate from interactive remote-session acceptance and Windows 11 compatibility testing.

## Installation and MSIX packaging

The published folder includes `Install.cmd` for the per-user unpackaged installation workflow. Follow the [user guide](USER-GUIDE.md) and test install, upgrade, and uninstall on Windows. The recorded CI run passed a fresh installation and uninstall; upgrades and Windows 11 acceptance remain outstanding.

The configured CI workflow additionally extracts the actual preview ZIP and invokes `scripts/Test-Install.ps1` with `-AllowDisposableRunner` and that extracted folder as `-BundlePath`. This is **CI-only**: the script refuses personal computers, self-hosted runners, existing SessionDock installations, and existing user-data locations. Do not bypass those checks. It uses the real per-user installation locations on a disposable GitHub-hosted Windows runner.

That smoke test checks installed file hashes, the Start menu shortcut, Installed Apps registration, the installed executable's isolated UI flow, uninstall cleanup, and retention of a synthetic user-data sentinel. The sentinel is not a database or a saved credential. Reports are `artifacts/verification/install-smoke.json` and `installed-ui-smoke.json`. The recorded Windows CI run passed all **7 installation checks** and **4 installed UI checks**. It does not test upgrades, MSIX, installed RDP/vault integration, Windows 11 compatibility, or actual remote sessions.

For the separate Windows Application Packaging Project:

```powershell
.\scripts\Package-Msix.ps1
```

This uses Visual Studio MSBuild and creates an **unsigned** package when the packaging build succeeds. It does not generate or trust a signing certificate. A development/test-signed package is not a production-signed release. Keep private signing keys outside the repository, configure the publisher/signing setup in the release environment, and verify installed activation, Credential Manager access, upgrades, and removal separately.

Read the [MSIX packaging notes](../SessionDock.Package/README.md) before distributing an MSIX. The `.wapproj` is kept outside the portable solution because it requires the Windows DesktopBridge packaging tools.

## Source layout and evidence

| Location | Purpose |
|---|---|
| `src/SessionDock.Core` | Models, validation, import/export, defaults, and session-state logic |
| `src/SessionDock.Infrastructure` | SQLite storage/migrations/backups, Windows credential vault, redacted local diagnostics |
| `src/SessionDock.Rdp.Windows` | Hosted ActiveX surface, native events, capability checks, and option mapping |
| `src/SessionDock.App` | WPF library, four-tab PC editor, settings/managers, tray, and session windows |
| `tests/SessionDock.Tests` | Portable logic, SQLite, import-security, and pure mapping tests |
| `tests/SessionDock.WindowsTests` | Windows activation, native option-setter, and temporary credential-vault probes |
| `SessionDock.Package` | MSIX packaging project and original application icons |
| `scripts` | Build, interop, probes, install/uninstall, and packaging commands |
| `artifacts` | Local build and verification outputs; excluded from Git |

The local `Build-Windows.ps1` script writes `artifacts/windows-environment.txt`; portable test results and probe reports use the paths above. CI publishes available reports as its **Windows-verification** artifact. These generated outputs are excluded from the Git source archive, and a failed early step can leave later reports absent. Review reports before sharing: environment output and local paths can identify the test computer or account.

The [capability matrix](CAPABILITIES.md), [verification report](VERIFICATION.md), and [backlog](BACKLOG.md) distinguish implemented source, observed tests, unsupported features, and remaining Windows gates. Keep those distinctions when reporting a new result. Dependency notices are in [THIRD-PARTY-NOTICES.md](../THIRD-PARTY-NOTICES.md) and `licenses/`; the original application's licensing decision is separate from those dependency licenses.

The Windows engine probe additionally exercises optional previews using synthetic control pixels: overlap isolation, size/blank/hidden checks, per-user encrypted cache reload, corruption fallback, deletion and clear. No remote desktop is captured by that probe. UI smoke now checks preview default-off, opt-in persistence and clearing. Actual RDP renderer behavior remains an interactive acceptance task.
