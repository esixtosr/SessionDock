using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using SessionDock.App.ViewModels;
using SessionDock.Core;

namespace SessionDock.App.Views;

internal sealed record Choice(Guid? Id, string Label) { public override string ToString() => Label; }
internal sealed class PcEditor : Window
{
    private readonly LibraryViewModel _vm;
    private readonly ConnectionProfile _draft;
    private readonly GatewayProfile? _stagedGateway;
    private readonly TextBox _endpoint, _friendly, _width, _height;
    private readonly ComboBox _credentials, _group, _gateway, _resolution, _color, _clipboard, _sound, _routing;
    private readonly CheckBox _bypass, _reconnect, _admin, _allMonitors, _fullscreen, _fit, _dynamic, _printers, _smartCards, _microphone;
    private readonly TextBlock _error;
    public PcEditor(Window owner, LibraryViewModel vm, ConnectionProfile? profile, GatewayProfile? stagedGateway = null)
    {
        Owner = owner; _vm = vm; _stagedGateway = stagedGateway; _draft = profile?.DeepClone() ?? new ConnectionProfile { Display = vm.Snapshot.Settings.DefaultDisplay with { }, Redirection = vm.Snapshot.Settings.DefaultRedirection.DeepClone(), Input = vm.Snapshot.Settings.DefaultInput with { } };
        Title = profile is null ? "Add PC" : "Configure PC"; Width = 680; Height = Math.Min(680, SystemParameters.WorkArea.Height - 30); MinWidth = 530; MinHeight = 450; WindowStartupLocation = WindowStartupLocation.CenterOwner; Ui.FitToWorkArea(this);
        var root = new DockPanel(); var header = Ui.Stack(18); DockPanel.SetDock(header, Dock.Top); _endpoint = Ui.Input("PC name (DNS, IPv4, [IPv6], optionally :port)", profile is null ? "" : _draft.Endpoint, header);
        var choices = CredentialChoices(); _credentials = Ui.Combo("Credentials", choices, choices.First(c => c.Id == _draft.CredentialId || _draft.CredentialId is not null && !choices.Any(x => x.Id == _draft.CredentialId)), header);
        header.Children.Add(Ui.Button("Add credentials…", () => { var editor = new CredentialEditor(this, vm, null); if (editor.ShowDialog() == true) { var next = CredentialChoices(); _credentials.ItemsSource = next; _credentials.SelectedItem = next.First(c => c.Id == editor.SavedCredentialId); } })); root.Children.Add(header);
        var footer = Ui.Stack(8); DockPanel.SetDock(footer, Dock.Bottom); _error = Ui.Text("", 12); footer.Children.Add(_error); Ui.Note(footer, "Changes apply to new sessions. An open session keeps its connection-time options."); Ui.Footer(footer, Save, () => DialogResult = false); root.Children.Add(footer);
        var tabs = new TabControl { Margin = new Thickness(18, 0, 18, 0) }; root.Children.Add(tabs);
        var general = Ui.Stack(); tabs.Items.Add(new TabItem { Header = "General", Content = Ui.Scroll(general) });
        _friendly = Ui.Input("Friendly name", _draft.FriendlyName, general);
        var groups = vm.Snapshot.Groups.Select(g => new Choice(g.Id, g.Name)).ToList(); _group = Ui.Combo("Group", groups, groups.FirstOrDefault(c => c.Id == _draft.GroupId) ?? groups.First(), general);
        var gateways = new List<Choice> { new(null, "No gateway") }; gateways.AddRange(vm.Snapshot.Gateways.Select(g => new Choice(g.Id, g.Label))); if (stagedGateway is not null) gateways.Add(new Choice(stagedGateway.Id, stagedGateway.Label)); _gateway = Ui.Combo("RD Gateway", gateways, gateways.FirstOrDefault(c => c.Id == _draft.GatewayId) ?? gateways.First(), general);
        Ui.Note(general, "An RD Gateway is a Remote Desktop Gateway server, not a router or VPN endpoint. Manage gateways in Settings.");
        _bypass = Ui.Check("Bypass gateway for local addresses", _draft.GatewayBypass, general, _draft.GatewayId.HasValue); _gateway.SelectionChanged += (_, _) => _bypass.IsEnabled = (_gateway.SelectedItem as Choice)?.Id.HasValue == true;
        _reconnect = Ui.Check("Reconnect if the connection is dropped", _draft.AutoReconnect, general); _admin = Ui.Check("Connect to an administrative session", _draft.AdminSession, general); Ui.Note(general, "Administrative mode requests host-supported behavior; it does not grant administrator privileges.");
        Ui.Check("Swap mouse buttons — unavailable", false, general, false); Ui.Note(general, "A proven session-only mechanism is not available. Local Windows mouse settings are never changed.");
        _routing = Ui.Combo("Windows / Alt key routing", Enum.GetValues<KeyboardRouting>(), _draft.Input.KeyboardRouting, general); Ui.Note(general, "Normal Ctrl shortcuts pass to the focused remote session, including terminal Ctrl+C. Ctrl+Alt+End opens remote security options.");
        var display = Ui.Stack(); tabs.Items.Add(new TabItem { Header = "Display", Content = Ui.Scroll(display) });
        var resolutions = new List<string> { "Default for this display", "Custom dimensions" }; resolutions.AddRange(vm.Snapshot.Resolutions.Select(r => r.Label));
        string selected = _draft.Display.AutomaticResolution ? resolutions[0] : vm.Snapshot.Resolutions.FirstOrDefault(r => r.Width == _draft.Display.Width && r.Height == _draft.Display.Height)?.Label ?? resolutions[1];
        _resolution = Ui.Combo("Resolution", resolutions, selected, display); _width = Ui.Input("Width (remote pixels)", _draft.Display.Width.ToString(), display); _height = Ui.Input("Height (remote pixels)", _draft.Display.Height.ToString(), display);
        _allMonitors = Ui.Check("Use all monitors — unavailable until interface validation", false, display, false);
        _fullscreen = Ui.Check("Start session in full screen (requires Windows-generated interop)", _draft.Display.StartFullScreen, display); _fit = Ui.Check("Fit existing desktop image to window", _draft.Display.FitToWindow, display); _dynamic = Ui.Check("Update remote resolution on resize", _draft.Display.DynamicResolution, display); _color = Ui.Combo("Color quality", new[] { 16, 24, 32 }, _draft.Display.ColorDepth, display);
        Ui.Check("Optimize for high-DPI displays — unavailable", false, display, false); Ui.Note(display, "Automatic sizing uses the window area. Fit scales existing pixels; dynamic resizing asks the host to change resolution. If a resize request fails, the session retains its last size. Host policy may limit color or redirection.");
        _resolution.SelectionChanged += (_, _) => UpdateResolution(); _dynamic.Checked += (_, _) => UpdateDisplay(); _dynamic.Unchecked += (_, _) => UpdateDisplay(); UpdateResolution();
        var devices = Ui.Stack(); tabs.Items.Add(new TabItem { Header = "Devices & Audio", Content = Ui.Scroll(devices) });
        _printers = Ui.Check("Printers", _draft.Redirection.Printers, devices); _smartCards = Ui.Check("Smart cards", _draft.Redirection.SmartCards, devices); _microphone = Ui.Check("Microphone", _draft.Redirection.Microphone, devices); Ui.Check("Cameras — unavailable", false, devices, false); _clipboard = Ui.Combo("Clipboard", Enum.GetValues<ClipboardMode>(), _draft.Redirection.Clipboard, devices); _sound = Ui.Combo("Play sound", Enum.GetValues<SoundMode>(), _draft.Redirection.Sound, devices);
        Ui.Note(devices, "Local = this computer; Remote = the remote computer; Off = no playback. Requested settings need engine/host support and may be restricted by policy. Directional clipboard requires separate capability proof and is unavailable.");
        var folders = Ui.Stack(); tabs.Items.Add(new TabItem { Header = "Folders", Content = Ui.Scroll(folders) }); Ui.Check("Redirect selected folders — unavailable", false, folders, false);
        var table = new DataGrid { AutoGenerateColumns = false, IsReadOnly = true, CanUserAddRows = false, Height = 150, IsEnabled = false, Foreground = SystemColors.ControlTextBrush }; foreach (var name in new[] { "Name", "Path", "ReadOnly" }) table.Columns.Add(new DataGridTextColumn { Header = name == "ReadOnly" ? "Read-only" : name, Binding = new System.Windows.Data.Binding(name), Width = new DataGridLength(1, DataGridLengthUnitType.Star) }); table.ItemsSource = _draft.Redirection.Folders; folders.Children.Add(table); var folderButtons = new StackPanel { Orientation = Orientation.Horizontal }; foreach (var title in new[] { "Add", "Edit", "Remove" }) { var button = Ui.Button(title, () => { }); button.IsEnabled = false; folderButtons.Children.Add(button); } folders.Children.Add(folderButtons);
        Ui.Note(folders, "Exact folder-only access and per-folder read-only enforcement are not proven for this engine. No folders or containing drives are redirected. This feature remains a PC-parity requirement.");
        Content = Ui.Scroll(root);
    }
    private List<Choice> CredentialChoices() { var result = new List<Choice> { new(null, "Ask when required") }; result.AddRange(_vm.Snapshot.Credentials.Select(c => new Choice(c.Id, c.Label))); return result; }
    private void UpdateResolution()
    {
        var label = _resolution.SelectedItem as string; bool custom = label == "Custom dimensions"; _width.IsEnabled = custom; _height.IsEnabled = custom;
        var preset = _vm.Snapshot.Resolutions.FirstOrDefault(r => r.Label == label); if (preset is not null) { _width.Text = preset.Width.ToString(); _height.Text = preset.Height.ToString(); }
        UpdateDisplay();
    }
    private void UpdateDisplay() { if (_fit is not null) { _fit.IsEnabled = _dynamic.IsChecked != true; _fit.ToolTip = _fit.IsEnabled ? "Scale the fixed remote desktop without changing its resolution" : "Dynamic resolution changes remote size; fit is a fallback if resize fails"; } }
    private void Save()
    {
        Ui.Safe(this, () =>
        {
            if (!EndpointParser.TryParse(_endpoint.Text, out var endpoint, out var error) || endpoint is null) { _error.Text = error; _endpoint.Focus(); return; }
            if (!int.TryParse(_width.Text, out var width) || !int.TryParse(_height.Text, out var height)) { _error.Text = "Resolution dimensions must be whole numbers."; return; }
            _draft.Host = endpoint.Host; _draft.Port = endpoint.Port; _draft.FriendlyName = _friendly.Text.Trim(); _draft.CredentialId = (_credentials.SelectedItem as Choice)?.Id; _draft.GroupId = ((Choice)_group.SelectedItem).Id ?? Group.DefaultId; _draft.GatewayId = (_gateway.SelectedItem as Choice)?.Id;
            _draft.GatewayBypass = _draft.GatewayId.HasValue && _bypass.IsChecked == true; _draft.AutoReconnect = _reconnect.IsChecked == true; _draft.AdminSession = _admin.IsChecked == true; _draft.SwapMouseButtons = false; _draft.Input.KeyboardRouting = (KeyboardRouting)_routing.SelectedItem;
            _draft.Display.AutomaticResolution = _resolution.SelectedIndex == 0; _draft.Display.Width = width; _draft.Display.Height = height; _draft.Display.UseAllMonitors = false; _draft.Display.StartFullScreen = _fullscreen.IsChecked == true; _draft.Display.FitToWindow = _fit.IsChecked == true; _draft.Display.DynamicResolution = _dynamic.IsChecked == true; _draft.Display.ColorDepth = (int)_color.SelectedItem; _draft.Display.HighDpi = false;
            _draft.Redirection.Printers = _printers.IsChecked == true; _draft.Redirection.SmartCards = _smartCards.IsChecked == true; _draft.Redirection.Microphone = _microphone.IsChecked == true; _draft.Redirection.Clipboard = (ClipboardMode)_clipboard.SelectedItem; _draft.Redirection.Sound = (SoundMode)_sound.SelectedItem; _draft.Redirection.Cameras = false; _draft.Redirection.RedirectFolders = false;
            var errors = ProfileValidator.Validate(_draft); if (errors.Count > 0) { _error.Text = string.Join("\n", errors); return; }
            if (_stagedGateway is not null && _draft.GatewayId == _stagedGateway.Id) _vm.Store.SaveConnectionWithGateway(_draft, _stagedGateway); else _vm.Store.SaveConnection(_draft); _vm.Reload(); DialogResult = true;
        });
    }
}
