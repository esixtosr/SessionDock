# Using SessionDock Preview

## Your PC library

The normal first launch is empty. Choose **Add → Add PC**. A PC can be a DNS name, IPv4 address, bracketed IPv6 address or host with a custom port. No network access is needed to save a profile. Use a friendly name and group to organize it. Saving applies all four tabs together; Cancel discards the draft.

Choose grid or list, sort by name/last connection/creation, and search by name, host, group or credential label. Search includes matching PCs in collapsed groups without changing the saved expansion preference. Favorites is a filtered view of the same library. A single click selects; double-click or Enter connects. A second connect action focuses its existing session window.

Right-click or use the card menu to edit, duplicate, favorite, move, export or delete. Deleting a profile does not delete anything on the remote computer. Removing a custom group moves its PCs to Saved Devices. Saved Devices cannot be removed.

## Credentials and gateways

**Ask when required** prompts locally for each new connection. The prompt is not saved. A named credential profile stores its label/username/domain in SQLite and its password in Windows Credential Manager for the current Windows account. It uses an application-specific target, not a global `TERMSRV` entry.

Editing a credential profile changes future connections referencing it. A blank password preserves the saved secret. Use the explicit clear checkbox to remove it. Deleting a credential profile identifies affected PCs and gateways and moves them to prompting. Passwords are never included in profile exports or database backups; credential labels, usernames and domains are metadata and are included.

RD Gateways are actual Remote Desktop Gateway services. They are not routers or VPN servers. Create a gateway in Settings and assign it to a PC. The gateway may have a different named credential profile than the PC. Bypass uses the control's documented direct-first gateway behavior; it is not a local-address heuristic. Removing an in-use gateway requires confirmation that affected PCs should connect directly.

## Sessions

Each PC has its own independent session window. The toolbar remains outside the native remote surface. The library shows Connected only after the ActiveX control reports completed logon. Merely opening a window, connecting a socket or starting authentication does not qualify.

Disconnect or close the session window to end the local connection. SessionDock does not send remote sign-out. Full screen has an explicit return action and native Ctrl+Alt+Break toggle; Ctrl+Alt+Left/Right releases remote focus where supported. Focus the remote session and press Ctrl+Alt+End for the remote Windows security screen. Automated Ctrl+Alt+Delete injection is unavailable.

Normal Ctrl+C/X/V/A/Z/F are not globally intercepted; in a terminal, Ctrl+C remains an interrupt. Windows/Alt routing is a per-PC choice. Keep local control available during initial keyboard/full-screen tests.

Automatic reconnect is requested only for eligible interruptions after sign-in, with bounded retries. Deliberate disconnect, authentication trouble and a certificate decision stop retries. After a failure, correct the cause before retrying.

## Requested connection options

Display distinguishes the number of remote pixels from scaling those pixels into the window. Fixed dimensions retain a specific remote size; Fit scales its image; dynamic resizing requests a changed remote resolution after resize. Failed dynamic resizing keeps the session open at its prior size. Multi-monitor and remote DPI optimizations remain unavailable until proven against the Windows engine.

Clipboard, printers, smart cards, microphone and sound are requests; remote policy can block them. Test each actual remote effect before relying on it. Camera, exact folder sharing/read-only, directional clipboard and session-local mouse swapping remain unavailable. SessionDock never silently substitutes a whole drive for a selected folder.

Active sessions use their connection-time profile copy. Saved changes apply to the next connection. Global defaults only apply to newly created PCs.

## Settings and recovery

Settings follow the nine reference categories. Functional changes save when made; credential/gateway/group editors have explicit Save/Cancel. Unsupported options carry an explanation and cannot silently simulate a feature.

Tray Quick Launcher is optional. With it enabled, closing the library hides it and leaves sessions running. Use **Exit SessionDock** to disconnect and exit. Automatic startup and a global launcher hotkey are not implemented. The application does not enable either behind the scenes.

No thumbnails are captured in this preview. Cards use original fallback art. The stored-screenshot deletion action clears the application cache; capture stays disabled. No diagnostics upload or update service is configured.

Use Settings → General to export metadata or back up the database. The normal library is `%LOCALAPPDATA%\SessionDock\library.db`. SQLite's backup operation produces a consistent copy including committed WAL data. Before a schema upgrade an existing database is backed up. A newer unsupported schema is refused rather than overwritten.

For manual database recovery, exit the app and tray, retain the current database and its `-wal`/`-shm` files in a separate folder, and restore a trusted backup as `library.db`. Do not mix sidecar files from an older database with a replacement. Restart. This is metadata recovery; passwords remain separate in the current Windows account's Credential Manager.

RDP import shows a review, rejects invalid values, reports unsupported properties and never connects automatically. Alternate shells, RemoteApp commands, scripts and credential blobs are not imported. Device redirection requires an explicit decision. Application-profile JSON imports likewise do not carry passwords or inherit saved secrets merely because an imported ID matches.

## Installation and removal

The per-user installer copies the bundle into `%LOCALAPPDATA%\Programs\SessionDock` and creates a Start menu shortcut. Exit SessionDock before upgrading. This preview is unsigned and Windows installation behavior remains to be tested.

Uninstall via Installed Apps or the installed `Uninstall-Local.ps1`. Library data and app-owned credentials are retained for reinstall. To remove saved passwords, delete their profiles in Settings before uninstalling. No startup item, firewall rule or trusted certificate is installed. MSIX has a separate packaging/acceptance path.

## Server certificate authentication

Strict remains the default. Optional Interactive mode uses native Windows warnings subject to policy. Read [certificate security and W11PC4-Public testing](CERTIFICATE-AUTHENTICATION.md) before enabling it.
