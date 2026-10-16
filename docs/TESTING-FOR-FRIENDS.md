# Try SessionDock in about five minutes

SessionDock brings a saved-PC library to Windows, inspired by Windows App on macOS. This preview handles **saved PC connections only**; cloud workspaces and published apps are not available.

The app passed automated checks on a Windows test machine for opening, saving PCs, settings, and installation. **Real remote sessions and Windows 11 compatibility still need verification.** This quick check helps establish what works on your computer.

Use Windows 11 x64 and a remote PC you own or have permission to access, already set up for Remote Desktop. If you do not have one ready, try steps 1–2. Setting up a remote PC may take longer than five minutes.

## Quick check

Record **Worked**, **Didn't work**, or **Not tried yet** for each step.

1. **Open it.** Extract the complete Windows preview ZIP and open `SessionDock.exe`. Keep the bundled files together. A new installation should show an empty library. The preview is unsigned; if Windows blocks it, record the message instead of changing security settings for this test.
2. **Save a PC and restart.** Choose **Add → Add PC**. Enter your authorized PC's address and a friendly name such as “Test PC.” Leave **Credentials** on **Ask when required**, then **Save**. Choose **Exit SessionDock**, reopen it, and check that the PC remains saved.
3. **Connect.** Double-click the PC and enter its remote account details in the app. Check for a usable remote desktop, **Connected** status, and working typing/mouse input. An open session window alone does not prove success. If it fails, record the message.
4. **Try clipboard and sound.** With **Clipboard: Bidirectional** and **Play sound: Local** in **Devices & Audio**, copy a harmless sentence between local and remote Notepad in both directions. Play a short sound remotely and listen locally. Changed settings need a new connection; remote policies may restrict these features.
5. **Try saved credentials.** Disconnect and close the session window. Use **Settings → Credentials → Add** to save an account you are comfortable storing on this Windows account. Edit the PC, select that credential, and save. Exit/reopen SessionDock and reconnect. Check whether it signs in without the local password prompt.

**Optional:** Connect to a second authorized PC. Check that both windows respond independently. Disconnect and close both afterward; this should leave their remote users signed in.

## Share your result

Open **Issues → New issue → Report a problem or test result** in this repository. Successful results are useful too. Include:

- The release version or ZIP filename and Windows version from **Settings → System → About → Windows specifications**.
- Which steps worked, failed, or were not tried.
- Your steps, expected result, actual result, and any error text/code.

Issues are public. Replace real addresses and account names with labels such as “Test PC.” Never share passwords, credential dialogs, databases, or profile exports. Screenshots are optional; hide private remote-screen content first.

## Skip these for now

Selected-folder/read-only sharing, one-way clipboard, cameras, multiple monitors, remote DPI optimization, mouse swapping, and session screenshots are unavailable. Full screen needs further validation. Basic installation/uninstall passed on the Windows test machine; Windows 11 installation and upgrades still need testing.

Untrusted server certificates can prevent a connection. Report that result without bypassing certificate checks.

More help: [user guide](USER-GUIDE.md), [full Windows checklist](WINDOWS-ACCEPTANCE.md), and [downloads/setup](../README.md).
