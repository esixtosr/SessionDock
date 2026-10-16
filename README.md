# SessionDock

A remote desktop connection manager for Windows, inspired by the saved-PC layout of **Windows App on Mac**.

The idea is simple: keep your remote PCs in one place, organize them into groups, and open them in separate session windows. This is a small project being built and improved through testing and feedback.

**This is an early preview.** It is ready for people to try and report problems, but it is not a finished replacement for Windows App. Real connections and some Windows-specific behavior still need testing. SessionDock is an independent project, not an official Microsoft app.

## Download and open it

**[Download the Windows preview](https://github.com/esixtosr/SessionDock/releases/tag/v0.1.0-preview.1)**

1. On the release page, download **`SessionDock-v0.1.0-preview.1-win-x64.zip`** under **Assets**. You do not need the source-code downloads to use the app.
2. Right-click the ZIP and choose **Extract All**.
3. Open the extracted folder and double-click **`SessionDock.exe`**. Keep the other files in that folder.
4. If you want a Start menu shortcut, run **`Install.cmd`**. Installing is optional.

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
