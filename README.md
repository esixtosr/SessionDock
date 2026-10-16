# SessionDock

A remote desktop connection manager for Windows, inspired by the saved-PC layout of **Windows App on Mac**.

The idea is simple: keep your remote PCs in one place, organize them into groups, and open them in separate session windows. This is a small project being built and improved through testing and feedback.

**This is an early preview.** It is ready for people to try and report problems, but it is not a finished replacement for Windows App. Real connections and some Windows-specific behavior still need testing. SessionDock is an independent project, not an official Microsoft app.

## Download and open it

**[Open the Windows downloads](https://github.com/esixtosr/SessionDock/actions/workflows/windows.yml?query=branch%3Amain)**

1. Open the newest successful **main** build (green check). GitHub may ask you to sign in.
2. Under **Artifacts**, download **SessionDock-v0.1.0-preview.5-Windows-x64**. Choose the Windows download, not developer source files or verification reports.
3. Right-click the downloaded ZIP and choose **Extract All** once.
4. Close SessionDock completely, then run **Install.cmd** from the new folder. Or run **SessionDock.exe** there without installing. Keep its other files together.
5. Open **Settings → About** to check the installed version. The library footer also shows it.

Each update has its own numbered Windows download. The installer shows the incoming version and keeps saved PCs and credentials. Earlier published previews remain on the [releases page](https://github.com/esixtosr/SessionDock/releases); they may lack newer fixes. Updates are manual; the app does not silently download or install them.

You need a **Windows 11 x64 computer**. ARM-based Windows PCs have not been tested. The download includes the app's runtime, so you should not need to install development tools.

This preview is unsigned, so Windows may show an unknown-publisher warning. If it is blocked or you are unsure about a warning, report what you see; you do not need to turn off Windows security to provide useful feedback.

## Add your first PC

The remote computer must already accept Remote Desktop connections, and you must have permission to use it. SessionDock does not enable Remote Desktop, create an account, or set up a VPN for you.

1. Select **Add → Add PC**.
2. Enter the remote computer's name or IP address.
3. Give it a friendly name, such as **Office PC**.
4. Leave **Credentials** on **Ask when required** for the first test.
5. Select **Save**, then double-click its card to connect.

Use the account you normally use to sign in to that remote PC. If your organization requires a VPN or RD Gateway, you still need that connection setup. Closing a session disconnects it; the app does not sign you out of the remote computer.

## What is included

- Saved PCs, groups, favorites, search, and grid/list views.
- Separate session windows and a built-in Remote Desktop connection engine.
- Named credential profiles, with password storage through Windows Credential Manager.
- Display, clipboard, audio, and device settings where the engine supports them.
- Settings that stay saved, plus profile import/export and database backups.
- An optional system-tray launcher.
- Optional encrypted last-session previews, per-PC certificate authentication, and an About screen with version/build details.

Automated checks on a Windows test machine passed for the build, saved profiles, settings, password storage and handoff, and installation/uninstall. Real remote connections and Windows 11 compatibility still need testing. [Testing results and limitations](docs/VERIFICATION.md) explain exactly what has been checked.

## What is still unfinished

Cloud workspaces, Windows 365, Azure Virtual Desktop, and published applications are planned for later. The focus right now is **saved PC connections**.

Selected-folder sharing and read-only folders, one-way clipboard, cameras, multiple monitors, remote DPI optimization, session-only mouse swapping, and saved desktop thumbnails are not available yet. Unsupported options are labeled in the app. The preview includes the Windows-generated RDP wrapper, but full-screen behavior still needs testing; use the resizable window if it is unavailable.

Server certificates are checked. If a connection fails with a certificate or sign-in error, report the error instead of expecting the app to bypass it.

## Help test it

Try saving a PC, restarting the app, connecting, and disconnecting. Then try the parts you care about, such as clipboard, sound, or a second session.

**[Simple testing guide](docs/TESTING-FOR-FRIENDS.md)** · **[Report a problem](https://github.com/esixtosr/SessionDock/issues/new?template=bug_report.yml)** · **[Suggest an improvement](https://github.com/esixtosr/SessionDock/issues/new?template=feature_request.yml)**

Tell us what you clicked, what you expected, and what happened. Screenshots help when they do not contain private information. Please leave out passwords, account details, and private server addresses. Feedback about confusing wording or an awkward layout is useful too.

## More information

- [User guide](docs/USER-GUIDE.md) — settings, credentials, backups, and uninstalling.
- [Roadmap](docs/BACKLOG.md) — the work still ahead.
- [Build from source](docs/BUILDING.md) — for anyone who wants to develop the app.
- [Detailed Windows tests](docs/WINDOWS-ACCEPTANCE.md) · [Feature status](docs/CAPABILITIES.md).
- [Third-party notices](THIRD-PARTY-NOTICES.md).
