using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using SessionDock.App.ViewModels;
using SessionDock.Core;

namespace SessionDock.App.Views;

internal sealed class SettingsWindow : Window
{
    private static readonly string[] Categories = ["General", "Personalization", "Full Screen Mode", "Notifications", "Keyboard", "Resolutions", "Credentials", "Gateways", "Groups", "About"];
    private readonly LibraryViewModel _vm;
    private readonly ListBox _navigation;
    private readonly ContentControl _page = new();
    private readonly Stack<int> _back = [], _forward = [];
    private readonly Button _backButton, _forwardButton;
    private int _current;
    private bool _historyNavigation;
    public SettingsWindow(Window owner, LibraryViewModel vm)
    {
        Owner = owner; _vm = vm; Title = "SessionDock Settings"; Width = 880; Height = Math.Min(680, SystemParameters.WorkArea.Height - 30); MinWidth = 680; MinHeight = 460; WindowStartupLocation = WindowStartupLocation.CenterOwner; Ui.FitToWorkArea(this);
        var root = new DockPanel(); var top = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(10) }; _backButton = Ui.Button("← Back", Back); _forwardButton = Ui.Button("Forward →", Forward); top.Children.Add(_backButton); top.Children.Add(_forwardButton); DockPanel.SetDock(top, Dock.Top); root.Children.Add(top);
        var footer = new DockPanel { Margin = new Thickness(14) }; var done = Ui.Button("Done", Close); DockPanel.SetDock(done, Dock.Right); footer.Children.Add(done); footer.Children.Add(Ui.Text("Settings save when changed. Shared-item editors save only when you choose Save.", 12, "MutedBrush")); DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
        var grid = new Grid(); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(210) }); grid.ColumnDefinitions.Add(new ColumnDefinition()); _navigation = new ListBox { ItemsSource = Categories, Margin = new Thickness(8) }; _navigation.ItemContainerStyle = new Style(typeof(ListBoxItem), (Style)FindResource(typeof(ListBoxItem))); _navigation.ItemContainerStyle.Setters.Add(new Setter(PaddingProperty, new Thickness(12))); _navigation.SelectionChanged += (_, _) => Navigate(_navigation.SelectedIndex); grid.Children.Add(_navigation); Grid.SetColumn(_page, 1); grid.Children.Add(_page); root.Children.Add(grid); Content = root; _navigation.SelectedIndex = 0;
    }
    private void Navigate(int index)
    {
        if (index < 0) return;
        if (!_historyNavigation && index != _current) { _back.Push(_current); _forward.Clear(); }
        _current = index; _backButton.IsEnabled = _back.Count > 0; _forwardButton.IsEnabled = _forward.Count > 0;
        var panel = Ui.Stack(20); panel.Children.Add(Ui.Text(Categories[index], 24));
        switch (index) { case 0: General(panel); break; case 1: Personalization(panel); break; case 2: FullScreen(panel); break; case 3: Notifications(panel); break; case 4: Keyboard(panel); break; case 5: Resolutions(panel); break; case 6: Credentials(panel); break; case 7: Gateways(panel); break; case 8: Groups(panel); break; case 9: About(panel); break; }
        _page.Content = Ui.Scroll(panel);
    }
    private void Back() { if (_back.Count == 0) return; _forward.Push(_current); _historyNavigation = true; _navigation.SelectedIndex = _back.Pop(); _historyNavigation = false; }
    private void Forward() { if (_forward.Count == 0) return; _back.Push(_current); _historyNavigation = true; _navigation.SelectedIndex = _forward.Pop(); _historyNavigation = false; }
    private void Save(Action update) => Ui.Safe(this, () => { try { update(); _vm.SaveSettings(); } catch { App.ApplyTheme(_vm.Snapshot.Settings.Theme); Navigate(_current); throw; } });
    private void Toggle(Panel panel, string label, bool value, Action<bool> update) { var check = Ui.Check(label, value, panel); check.Checked += (_, _) => Save(() => update(true)); check.Unchecked += (_, _) => Save(() => update(false)); }
    private void Select<T>(Panel panel, string label, T value, Action<T> update) where T : struct, Enum { var combo = Ui.Combo(label, Enum.GetValues<T>(), value, panel); combo.SelectionChanged += (_, _) => Save(() => update((T)combo.SelectedItem)); }
    private void General(StackPanel panel)
    {
        Ui.Check("Configure system HTTP proxy — unavailable", false, panel, false); Ui.Note(panel, "There are no workspace downloads. RD Gateway proxy behavior needs Windows validation; the application does not override a proxy.");
        Ui.Check("Prefer UDP — unavailable as an application control", false, panel, false); Ui.Note(panel, "The installed RDP control negotiates transport. Ordinary RDP UDP is separate from AVD Shortpath; neither is claimed as verified here.");
        Ui.Check("Microsoft Teams optimization — deferred", false, panel, false); Ui.Check("Send optional diagnostics — unavailable", false, panel, false); Ui.Note(panel, "No diagnostics upload service exists. Redacted event logs stay in your local application-data folder and contain profile IDs, states, and error codes; no hosts, usernames, or secrets. Never include passwords in diagnostic notes.");
        Ui.Check("Graphics interpolation override — unavailable", false, panel, false); Ui.Check("RemoteApp high-DPI optimization — deferred", false, panel, false);
        panel.Children.Add(Ui.Text("Defaults for new PCs", 18)); Ui.Note(panel, "These defaults apply to newly created profiles. Existing PCs are unchanged. Imported RDP device settings always require explicit approval.");
        Toggle(panel, "Clipboard on new PCs", _vm.Snapshot.Settings.DefaultRedirection.Clipboard == ClipboardMode.Bidirectional, value => _vm.Snapshot.Settings.DefaultRedirection.Clipboard = value ? ClipboardMode.Bidirectional : ClipboardMode.Disabled);
        Toggle(panel, "Printer redirection on new PCs", _vm.Snapshot.Settings.DefaultRedirection.Printers, value => _vm.Snapshot.Settings.DefaultRedirection.Printers = value);
        Select(panel, "Sound for new PCs", _vm.Snapshot.Settings.DefaultRedirection.Sound, value => _vm.Snapshot.Settings.DefaultRedirection.Sound = value);
        Ui.Check("Imported folder defaults: Nothing / read-only unavailable", false, panel, false);
        panel.Children.Add(Ui.Text("Library storage", 18)); Ui.Note(panel, "The per-user database stores profile metadata. Passwords remain in Windows Credential Manager and are excluded from exports and backups."); panel.Children.Add(Ui.Button("Export application profiles…", ExportProfiles)); panel.Children.Add(Ui.Button("Import application profiles…", ImportProfiles)); panel.Children.Add(Ui.Button("Back up database…", Backup));
    }
    private void About(StackPanel panel)
    {
        panel.Children.Add(Ui.Text("SessionDock " + AppBuild.Version, 22));
        Ui.Note(panel, "An independent Windows Remote Desktop connection manager. Unsigned testing preview.");
        Ui.Note(panel, "Build: " + AppBuild.Build);
        Ui.Note(panel, "Windows x64 · Bundled runtime; no development tools needed.");
        Ui.Note(panel, "Running from: " + Environment.ProcessPath);
        panel.Children.Add(Ui.Button("Copy version details", () => Ui.Safe(this, () => Clipboard.SetText(AppBuild.Details))));
        panel.Children.Add(Ui.Button("Open downloads", () => Ui.Safe(this, () => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(AppBuild.DownloadsUrl) { UseShellExecute = true }))));
        Ui.Note(panel, "Choose the newest successful main build, then the SessionDock-v…-Windows-x64 download under Artifacts. GitHub may require sign-in. Download and extract once, close SessionDock completely, then run Install.cmd from the new folder. Your saved PCs and credentials are retained. Updates are installed manually.");
    }
    private void Personalization(StackPanel panel)
    {
        Select(panel, "Library view", _vm.Snapshot.Settings.View, value => _vm.Snapshot.Settings.View = value); Select(panel, "Card size", _vm.Snapshot.Settings.CardSize, value => _vm.Snapshot.Settings.CardSize = value); Toggle(panel, "Group PC connections", _vm.Snapshot.Settings.GroupConnections, value => _vm.Snapshot.Settings.GroupConnections = value);
        Toggle(panel, "Quick Launcher in Windows system tray", _vm.Snapshot.Settings.TrayEnabled, value => _vm.Snapshot.Settings.TrayEnabled = value); Ui.Note(panel, "When enabled, closing the library hides it while the process and sessions stay open. Use Exit SessionDock in the tray to disconnect sessions and exit. Startup launch is not enabled.");
        Select(panel, "Appearance", _vm.Snapshot.Settings.Theme, value => { _vm.Snapshot.Settings.Theme = value; App.ApplyTheme(value); });
        Toggle(panel, "Save last-session previews", _vm.Snapshot.Settings.ScreenshotThumbnails, value => _vm.Snapshot.Settings.ScreenshotThumbnails = value);
        Ui.Note(panel, "Off by default. Previews may contain private remote-screen content. When enabled, a small image is retained while connected and saved locally when the session ends, encrypted for your Windows account. Turning this off hides previews and stops capture; Clear previews also removes saved images and turns capture off. Windows may show a capture border around the session window. Capture is best effort; see Connection details for capture status.");
        panel.Children.Add(Ui.Button("Clear previews", () => Ui.Safe(this, () =>
        {
            _vm.Snapshot.Settings.ScreenshotThumbnails = false;
            _vm.SaveSettings(); // Notify open sessions first so no buffered image can be saved again.
            _vm.Previews.Clear(); _vm.Store.DeleteThumbnailMetadata();
            _vm.PreviewChanged(); Navigate(_current);
            MessageBox.Show(this, "Saved previews cleared. Capture is off; enable Save last-session previews to resume.", "Previews cleared");
        })));

        Ui.Note(panel, "Cloud PCs, Azure Virtual Desktop, Remote Desktop Services feeds, and App view options are deferred. PC Connections is the only current library category.");
    }
    private void FullScreen(StackPanel panel)
    {
        Ui.Note(panel, "A persistent session toolbar sits above the remote surface. Full screen fills the current display while keeping Disconnect and Exit full screen available. This behavior still requires Windows keyboard and DPI validation.");
        var behavior = Ui.Combo("Connection bar", new[] { "Always visible" }, "Always visible", panel); behavior.IsEnabled = false; Ui.Note(panel, "System and auto-hide variants are unavailable. When the Windows-generated interop wrapper enables native full screen, Ctrl+Alt+Break toggles it. Ctrl+Alt+Left or Ctrl+Alt+Right releases focus to the local toolbar; use Exit full screen. Without the wrapper, full screen is disabled. Windows/Alt routing remains a separate per-PC setting.");
    }
    private void Notifications(StackPanel panel)
    {
        Toggle(panel, "Useful tips in the notification panel", _vm.Snapshot.Settings.SmartTips, value => _vm.Snapshot.Settings.SmartTips = value); Ui.Check("New version available notices — unavailable", false, panel, false); Ui.Note(panel, "No trusted update source has been configured. Actionable errors remain visible inside the session window and in the local notification panel regardless of tips preferences.");
    }
    private void Keyboard(StackPanel panel)
    {
        Select(panel, "Windows / Alt routing for new PCs", _vm.Snapshot.Settings.DefaultInput.KeyboardRouting, value => _vm.Snapshot.Settings.DefaultInput.KeyboardRouting = value); Ui.Note(panel, "Local keeps Windows/Alt combinations on this PC. RemoteWhenFocused sends them to the focused session; RemoteInFullScreen sends them remotely only in full screen. Existing PCs retain their own choice."); Ui.Note(panel, "Ctrl+C/X/V/A/Z/F go to the focused remote session. There is no global Ctrl+C interception, so terminal interrupt behavior is preserved. Closing the client disconnects and does not sign out the remote account."); Ui.Note(panel, "Ctrl+Alt+End is the native Remote Desktop security-screen shortcut. The toolbar displays this instruction; unverified key injection is not enabled.");
        Ui.Check("Quick Launcher global shortcut — unavailable", false, panel, false); Ui.Note(panel, "No global hotkey is registered. Conflict detection and exact shortcut routing need Windows acceptance tests.");
    }
    private void Resolutions(StackPanel panel)
    {
        Ui.Note(panel, "Single-monitor presets. Removing a preset preserves dimensions already stored in PC profiles."); var list = new ListBox { ItemsSource = _vm.Snapshot.Resolutions, DisplayMemberPath = "Label", Height = 290 }; panel.Children.Add(list);
        var row = new StackPanel { Orientation = Orientation.Horizontal }; row.Children.Add(Ui.Button("Add…", () =>
        {
            var text = Ui.Ask(this, "Add resolution", "Width × height in pixels", "1920x1080"); if (text is null) return; var parts = text.Replace('×', 'x').ToLowerInvariant().Split('x');
            if (parts.Length != 2 || !int.TryParse(parts[0].Trim(), out var width) || !int.TryParse(parts[1].Trim(), out var height)) { MessageBox.Show(this, "Use width x height, for example 1920x1080."); return; }
            var errors = ProfileValidator.ValidateResolution(width, height); if (errors.Count > 0) { MessageBox.Show(this, string.Join("\n", errors)); return; }
            if (_vm.Snapshot.Resolutions.Any(r => r.Width == width && r.Height == height)) { MessageBox.Show(this, "That resolution is already in the list."); return; }
            Ui.Safe(this, () => { var presets = _vm.Snapshot.Resolutions.ToList(); presets.Add(new ResolutionPreset { Width = width, Height = height }); _vm.Store.SaveResolutions(presets); _vm.Reload(); Navigate(_current); });
        })); row.Children.Add(Ui.Button("Remove", () => { if (list.SelectedItem is ResolutionPreset preset) Ui.Safe(this, () => { _vm.Store.SaveResolutions(_vm.Snapshot.Resolutions.Where(r => r.Id != preset.Id)); _vm.Reload(); Navigate(_current); }); })); row.Children.Add(Ui.Button("Restore defaults", () => { if (Ui.Confirm(this, "Replace the preset list with the 23 reference resolutions? Saved PC dimensions are unchanged.")) Ui.Safe(this, () => { _vm.Store.SaveResolutions(ResolutionPreset.Defaults()); _vm.Reload(); Navigate(_current); }); })); panel.Children.Add(row);
    }
    private void Credentials(StackPanel panel)
    {
        Ui.Note(panel, "Shared names and usernames are stored in the library; passwords are protected by Windows Credential Manager."); var list = new ListBox { ItemsSource = _vm.Snapshot.Credentials, DisplayMemberPath = "Label", Height = 250 }; panel.Children.Add(list); var row = new StackPanel { Orientation = Orientation.Horizontal }; row.Children.Add(Ui.Button("Add…", () => { new CredentialEditor(this, _vm, null).ShowDialog(); Navigate(_current); })); row.Children.Add(Ui.Button("Edit…", () => { if (list.SelectedItem is CredentialProfile credential) { new CredentialEditor(this, _vm, credential).ShowDialog(); Navigate(_current); } }));
        row.Children.Add(Ui.Button("Remove", () =>
        {
            if (list.SelectedItem is not CredentialProfile credential) return; var pcs = _vm.Snapshot.Connections.Where(c => c.CredentialId == credential.Id).Select(c => c.DisplayName); var gateways = _vm.Snapshot.Gateways.Where(g => g.CredentialId == credential.Id).Select(g => "Gateway: " + g.Label); var affected = pcs.Concat(gateways).ToArray();
            if (!Ui.Confirm(this, $"Delete '{credential.Label}' and its saved password?\n\nAffected references move to Ask when required:\n{(affected.Length == 0 ? "None" : string.Join("\n", affected))}")) return;
            Ui.Safe(this, () =>
            {
                using var previous = _vm.Vault.Read(credential.Id);
                _vm.Vault.Delete(credential.Id);
                try { _vm.Store.DeleteCredential(credential.Id); }
                catch (Exception databaseError)
                {
                    try { if (previous is not null) _vm.Vault.Save(credential.Id, credential.Username, previous); }
                    catch (Exception) { throw new InvalidOperationException("The library could not delete the credential, and its previous password could not be restored. Re-enter its password before connecting.", databaseError); }
                    throw;
                }
                _vm.Reload(); Navigate(_current);
            });
        })); panel.Children.Add(row);
    }
    private void Gateways(StackPanel panel)
    {
        Ui.Note(panel, "These are Remote Desktop Gateway servers. Destination credentials and gateway credentials are selected separately. Gateway authentication remains a Windows integration test requirement."); var list = new ListBox { ItemsSource = _vm.Snapshot.Gateways, DisplayMemberPath = "Label", Height = 260 }; panel.Children.Add(list); var row = new StackPanel { Orientation = Orientation.Horizontal }; row.Children.Add(Ui.Button("Add…", () => { new GatewayEditor(this, _vm, null).ShowDialog(); Navigate(_current); })); row.Children.Add(Ui.Button("Edit / information…", () => { if (list.SelectedItem is GatewayProfile gateway) { new GatewayEditor(this, _vm, gateway).ShowDialog(); Navigate(_current); } })); row.Children.Add(Ui.Button("Remove", () =>
        {
            if (list.SelectedItem is not GatewayProfile gateway) return; var affected = _vm.Snapshot.Connections.Where(c => c.GatewayId == gateway.Id).Select(c => c.DisplayName).ToArray(); if (!Ui.Confirm(this, $"Remove '{gateway.Label}'?\n\nExplicitly switch these PCs to direct connections:\n{(affected.Length == 0 ? "None" : string.Join("\n", affected))}\n\nCancel to edit their gateway selections first.")) return; Ui.Safe(this, () => { _vm.Store.DeleteGateway(gateway.Id); _vm.Reload(); Navigate(_current); });
        })); panel.Children.Add(row);
    }
    private void Groups(StackPanel panel)
    {
        Ui.Note(panel, "Saved Devices is the protected fallback. Removing another group moves its PCs to Saved Devices."); var list = new ListBox { ItemsSource = _vm.Snapshot.Groups.OrderBy(g => g.Order).ToList(), DisplayMemberPath = "Name", Height = 260 }; panel.Children.Add(list); var row = new WrapPanel(); row.Children.Add(Ui.Button("Add…", () => { var name = Ui.Ask(this, "Add group", "Group name"); if (name is not null) Ui.Safe(this, () => { _vm.Store.SaveGroup(new Group { Name = name, Order = _vm.Snapshot.Groups.Count }); _vm.Reload(); Navigate(_current); }); })); row.Children.Add(Ui.Button("Rename…", () => { if (list.SelectedItem is not Group group || group.Id == Group.DefaultId) return; var name = Ui.Ask(this, "Rename group", "Group name", group.Name); if (name is not null) Ui.Safe(this, () => { _vm.Store.SaveGroup(group with { Name = name }); _vm.Reload(); Navigate(_current); }); })); row.Children.Add(Ui.Button("Move up", () => Reorder(list, -1))); row.Children.Add(Ui.Button("Move down", () => Reorder(list, 1))); row.Children.Add(Ui.Button("Remove", () => { if (list.SelectedItem is not Group group || group.Id == Group.DefaultId) return; if (Ui.Confirm(this, $"Remove '{group.Name}' and move its PCs to Saved Devices?")) Ui.Safe(this, () => { _vm.Store.DeleteGroup(group.Id); _vm.Reload(); Navigate(_current); }); })); panel.Children.Add(row);
    }
    private void Reorder(ListBox list, int delta)
    {
        if (list.SelectedItem is not Group group || group.Id == Group.DefaultId) return; var groups = _vm.Snapshot.Groups.OrderBy(g => g.Order).ToList(); var index = groups.FindIndex(g => g.Id == group.Id); var next = index + delta; if (next < 1 || next >= groups.Count) return; (groups[index], groups[next]) = (groups[next], groups[index]); Ui.Safe(this, () => { for (int i = 0; i < groups.Count; i++) _vm.Store.SaveGroup(groups[i] with { Order = i }); _vm.Reload(); Navigate(_current); });
    }
    private void ExportProfiles() => Ui.Safe(this, () => { var file = new SaveFileDialog { Filter = "SessionDock profiles (*.json)|*.json", FileName = "SessionDock-profiles.json" }; if (file.ShowDialog(this) == true) File.WriteAllText(file.FileName, ProfileExportCodec.Export(_vm.Snapshot)); });
    private void ImportProfiles() => Ui.Safe(this, () =>
    {
        if (_vm.OpenSessionCount > 0) { MessageBox.Show(this, "Close all session windows before replacing the library."); return; }
        var file = new OpenFileDialog { Filter = "SessionDock profiles (*.json)|*.json", Multiselect = false };
        if (file.ShowDialog(this) != true) return;
        if (new FileInfo(file.FileName).Length > 8_388_608) throw new FormatException("Profile imports are limited to 8 MiB.");
        var json = File.ReadAllText(file.FileName);
        var preview = ProfileExportCodec.Import(json, false);
        var dialog = new Window { Owner = this, Title = "Review application-profile import", Width = 650, Height = 590, MinHeight = 400, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var panel = Ui.Stack(20);
        panel.Children.Add(Ui.Text($"Replace library with {preview.Connections.Count} PCs", 22));
        Ui.Note(panel, $"{preview.Groups.Count} groups, {preview.Credentials.Count} credential labels, {preview.Gateways.Count} gateways, {preview.Resolutions.Count} resolution presets. This replaces all current library metadata and settings after an automatic database backup. It does not connect or delete any remote machines.");
        Ui.Note(panel, "Passwords are not imported. Imported credential IDs are isolated from existing saved secrets, so you must re-enter passwords. The original Windows Credential Manager entries remain available to your pre-import backup.");
        Ui.Note(panel, "Unsupported requests (all monitors, high-DPI negotiation, camera/folder access, and mouse swapping) will be disabled. Tray, screenshots, and global hotkeys remain off. Every supported device redirection request requires the checkbox below.");
        var devices = Ui.Check("Approve supported clipboard, sound, printer, smart-card, and microphone requests", false, panel);
        var list = new ListBox { Height = 160, ItemsSource = preview.Connections.Select(c => $"{c.DisplayName} — {c.Endpoint}").ToList() }; panel.Children.Add(list);
        panel.Children.Add(Ui.Button("Back up and replace library", () => dialog.DialogResult = true)); panel.Children.Add(Ui.Button("Cancel", () => dialog.DialogResult = false)); dialog.Content = Ui.Scroll(panel);
        Ui.FitToWorkArea(dialog); if (dialog.ShowDialog() != true) return;
        var imported = ProfileExportCodec.Import(json, devices.IsChecked == true);
        foreach (var pc in imported.Connections)
        {
            pc.SwapMouseButtons = false; pc.Display.UseAllMonitors = false; pc.Display.HighDpi = false;
            pc.Redirection.Cameras = false; pc.Redirection.RedirectFolders = false; pc.Redirection.Folders.Clear();
        }
        imported.Settings.DefaultDisplay.UseAllMonitors = false; imported.Settings.DefaultDisplay.HighDpi = false;
        imported.Settings.DefaultRedirection.Cameras = false; imported.Settings.DefaultRedirection.RedirectFolders = false; imported.Settings.DefaultRedirection.Folders.Clear();
        var backupDirectory = Path.Combine(_vm.DataDirectory, "backups"); Directory.CreateDirectory(backupDirectory);
        var backup = Path.Combine(backupDirectory, "before-import-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-ffff") + ".db");
        _vm.Store.CreateBackup(backup); _vm.Store.ReplaceLibrary(imported); _vm.Reload(); _vm.Previews.Clear(); App.ApplyTheme(imported.Settings.Theme); Navigate(_current);
        MessageBox.Show(this, "Library replaced. Re-enter passwords before connecting. A backup was saved to:\n" + backup);
    });
    private void Backup() => Ui.Safe(this, () => { var file = new SaveFileDialog { Filter = "SQLite backup (*.db)|*.db", FileName = "SessionDock-backup.db" }; if (file.ShowDialog(this) == true) { _vm.Store.CreateBackup(file.FileName); MessageBox.Show(this, "Database backup created. Saved passwords are not included."); } });
}
