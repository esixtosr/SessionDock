using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Security.Cryptography;
using Microsoft.Win32;
using SessionDock.App.ViewModels;
using SessionDock.Core;
using Forms = System.Windows.Forms;

namespace SessionDock.App.Views;

public sealed class MainWindow : Window
{
    private readonly LibraryViewModel _vm;
    private readonly Dictionary<Guid, SessionWindow> _sessions = [];
    private readonly ContentControl _content = new();
    private readonly Grid _layout = new();
    private readonly ColumnDefinition _sidebarColumn = new() { Width = new GridLength(240) };
    private readonly TextBlock _heading = Ui.Text("Devices", 26);
    private readonly TextBlock _count = Ui.Text("", 12, "MutedBrush");
    private string _section = "Devices";
    private Guid? _selected;
    private Border? _selectedCard;
    private Forms.NotifyIcon? _tray;
    private bool _exiting;
    private readonly Dictionary<string, Button> _navigation = [];
    public MainWindow(LibraryViewModel vm)
    {
        _vm = vm; Title = "SessionDock"; Width = 1200; Height = 780; MinWidth = 760; MinHeight = 520; Ui.FitToWorkArea(this);
        // Keep the initial library entirely within the available desktop, including the taskbar inset.
        Left = SystemParameters.WorkArea.Left + (SystemParameters.WorkArea.Width - Width) / 2;
        Top = SystemParameters.WorkArea.Top + (SystemParameters.WorkArea.Height - Height) / 2;
        _layout.ColumnDefinitions.Add(_sidebarColumn); _layout.ColumnDefinitions.Add(new ColumnDefinition());
        var sidebar = new DockPanel { Background = Ui.Brush("PanelBrush"), Margin = new Thickness(0, 0, 12, 0) };
        sidebar.SetResourceReference(BackgroundProperty, "PanelBrush");
        var bottom = Ui.Stack(); bottom.Children.Add(Ui.Button("Settings", OpenSettings)); bottom.Children.Add(Ui.Button("Exit SessionDock", ExitApp)); DockPanel.SetDock(bottom, Dock.Bottom); sidebar.Children.Add(bottom);
        var nav = Ui.Stack(20);
        var brand = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 10) };
        var brandLabel = new TextBlock { Text = "SD", FontFamily = new FontFamily("Consolas"), FontSize = 14, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        brandLabel.SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush");
        var emblem = new Border { Width = 38, Height = 38, CornerRadius = new CornerRadius(10), BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 10, 0), Child = brandLabel };
        emblem.SetResourceReference(Border.BorderBrushProperty, "AccentBrush"); brand.Children.Add(emblem);
        var wordmark = Ui.Text("SessionDock", 22); wordmark.FontWeight = FontWeights.SemiBold; wordmark.VerticalAlignment = VerticalAlignment.Center; brand.Children.Add(wordmark); nav.Children.Add(brand);
        var caption = Ui.Text("REMOTE DESKTOP", 10, "MutedBrush"); caption.FontFamily = new FontFamily("Consolas"); caption.Margin = new Thickness(0, 0, 0, 24); nav.Children.Add(caption);
        foreach (var section in new[] { "Favorites", "Devices", "Apps" })
        {
            var button = Ui.Button(section, () => { _section = section; _vm.FavoritesOnly = section == "Favorites"; Refresh(); });
            button.HorizontalContentAlignment = HorizontalAlignment.Left; button.Padding = new Thickness(16, 12, 16, 12); button.Margin = new Thickness(0, 3, 0, 3);
            _navigation[section] = button; nav.Children.Add(button);
        }
        sidebar.Children.Add(nav); _layout.Children.Add(sidebar);
        var main = new DockPanel { Margin = new Thickness(8, 18, 18, 12) }; Grid.SetColumn(main, 1); _layout.Children.Add(main);
        var header = new DockPanel(); DockPanel.SetDock(header, Dock.Top);
        var collapse = Ui.Button("☰", () => _sidebarColumn.Width = new GridLength(_sidebarColumn.Width.Value == 0 ? 240 : 0)); collapse.ToolTip = "Show or hide sidebar"; AutomationProperties.SetName(collapse, "Show or hide sidebar"); DockPanel.SetDock(collapse, Dock.Left); header.Children.Add(collapse); header.Children.Add(_heading); main.Children.Add(header);
        var toolbar = new WrapPanel { Margin = new Thickness(0, 4, 0, 14) }; DockPanel.SetDock(toolbar, Dock.Top);
        var add = Ui.Button("＋ Add", () => { }); var addMenu = new ContextMenu(); addMenu.Items.Add(Item("Add PC", () => Edit(null))); addMenu.Items.Add(Item("Add Group", AddGroup)); addMenu.Items.Add(Item("Import RDP file…", ImportRdp)); add.ContextMenu = addMenu; add.Click += (_, _) => { addMenu.PlacementTarget = add; addMenu.IsOpen = true; }; add.SetResourceReference(Button.ForegroundProperty, "AccentBrush"); add.SetResourceReference(Button.BorderBrushProperty, "AccentBrush"); toolbar.Children.Add(add);
        toolbar.Children.Add(Ui.Button("Notifications", ShowNotifications));
        toolbar.Children.Add(Ui.Button("Grid / List", () => { _vm.Snapshot.Settings.View = _vm.Snapshot.Settings.View == LibraryView.Grid ? LibraryView.List : LibraryView.Grid; _vm.SaveSettings(); }));
        var sort = new ComboBox { ItemsSource = Enum.GetValues<LibrarySort>(), SelectedItem = vm.Snapshot.Settings.Sort, Width = 125, Margin = new Thickness(4), ToolTip = "Sort connections" }; AutomationProperties.SetName(sort, "Sort connections"); sort.SelectionChanged += (_, _) => Ui.Safe(this, () => { _vm.Snapshot.Settings.Sort = (LibrarySort)sort.SelectedItem; _vm.SaveSettings(); }); toolbar.Children.Add(sort);
        toolbar.Children.Add(Ui.Button("↑ / ↓", () => { _vm.Snapshot.Settings.SortDescending = !_vm.Snapshot.Settings.SortDescending; _vm.SaveSettings(); }));
        var view = Ui.Button("View options", () => { }); var menu = new ContextMenu(); menu.Items.Add(Item("Toggle groups", () => { _vm.Snapshot.Settings.GroupConnections = !_vm.Snapshot.Settings.GroupConnections; _vm.SaveSettings(); })); foreach (var size in Enum.GetValues<CardSize>()) menu.Items.Add(Item($"{size} cards", () => { _vm.Snapshot.Settings.CardSize = size; _vm.SaveSettings(); })); view.Click += (_, _) => { menu.PlacementTarget = view; menu.IsOpen = true; }; toolbar.Children.Add(view);
        var search = new TextBox { Width = 210, Margin = new Thickness(4), ToolTip = "Search name, host, group, or credential label" }; AutomationProperties.SetName(search, "Search connections");
        var searchContainer = new Grid(); searchContainer.Children.Add(search);
        var watermark = new TextBlock { Text = "Search PCs…", Margin = new Thickness(15, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center, Foreground = Ui.Brush("MutedBrush"), IsHitTestVisible = false };
        watermark.SetResourceReference(TextBlock.ForegroundProperty, "MutedBrush");
        searchContainer.Children.Add(watermark);
        search.TextChanged += (_, _) => { watermark.Visibility = search.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed; _vm.Query = search.Text; Refresh(); }; toolbar.Children.Add(searchContainer); main.Children.Add(toolbar);
        DockPanel.SetDock(_count, Dock.Bottom); main.Children.Add(_count); main.Children.Add(_content); Content = _layout;
        _vm.Changed += (_, _) => { Refresh(); ConfigureTray(); }; Closing += OnClosing; Loaded += (_, _) => { Refresh(); ConfigureTray(); };
    }
    private static MenuItem Item(string title, Action action) { var item = new MenuItem { Header = title }; item.Click += (_, _) => action(); return item; }
    private void Refresh()
    {
        foreach (var pair in _navigation)
        {
            pair.Value.SetResourceReference(Button.BackgroundProperty, pair.Key == _section ? "AccentSurfaceBrush" : "PanelBrush");
            pair.Value.SetResourceReference(Button.ForegroundProperty, pair.Key == _section ? "AccentBrush" : "MutedBrush");
            pair.Value.SetResourceReference(Button.BorderBrushProperty, pair.Key == _section ? "AccentBrush" : "PanelBrush");
        }
        _heading.FontWeight = FontWeights.SemiBold;
        _heading.Text = _section; _count.Text = "SessionDock " + AppBuild.Version + " · " + _sessions.Count + " session window(s)";
        if (_section == "Apps") { var future = Ui.Stack(24); future.Children.Add(Ui.Text("Published applications are planned", 22)); Ui.Note(future, "SessionDock currently manages saved PC connections. Cloud workspaces, Azure Virtual Desktop, Windows 365, and published RemoteApps are deferred."); _content.Content = future; return; }
        var profiles = _vm.Filtered().ToList();
        if (profiles.Count == 0) { var empty = Ui.Stack(24); empty.Children.Add(Ui.Text(string.IsNullOrWhiteSpace(_vm.Query) ? (_section == "Favorites" ? "No favorites yet" : "Add your first PC") : "No matching PCs", 22)); Ui.Note(empty, "Save an authorized remote PC to connect. No example machines or credentials are added."); empty.Children.Add(Ui.Button("Add PC", () => Edit(null))); _content.Content = empty; return; }
        if (_vm.Snapshot.Settings.View == LibraryView.List) { ShowList(profiles); return; }
        var stack = new StackPanel();
        if (!_vm.Snapshot.Settings.GroupConnections) { stack.Children.Add(Cards(profiles)); }
        else
        {
            foreach (var group in _vm.Snapshot.Groups.OrderBy(g => g.Order))
            {
                var items = profiles.Where(p => p.GroupId == group.Id).ToList(); if (items.Count == 0) continue;
                var expander = new Expander { Header = $"{group.Name}  ·  {items.Count}", IsExpanded = !string.IsNullOrWhiteSpace(_vm.Query) || group.IsExpanded, Content = Cards(items) };
                expander.Expanded += (_, _) => SetExpanded(group, true); expander.Collapsed += (_, _) => SetExpanded(group, false); stack.Children.Add(expander);
            }
        }
        _content.Content = Ui.Scroll(stack);
    }
    private void SetExpanded(Group group, bool value) { if (!string.IsNullOrWhiteSpace(_vm.Query)) return; group.IsExpanded = value; Ui.Safe(this, () => _vm.Store.SaveGroup(group)); }
    private WrapPanel Cards(IEnumerable<ConnectionProfile> profiles)
    {
        var wrap = new WrapPanel(); double width = _vm.Snapshot.Settings.CardSize switch { CardSize.Small => 230, CardSize.Large => 360, _ => 300 };
        foreach (var profile in profiles)
        {
            var state = _vm.State(profile.Id); var content = new StackPanel();
            var preview = Preview(profile.Id, (width - 24) * .625);

            content.Children.Add(preview); var top = new DockPanel(); var favorite = Ui.Button(profile.Favorite ? "★" : "☆", () => ToggleFavorite(profile)); AutomationProperties.SetName(favorite, profile.Favorite ? "Remove favorite" : "Add favorite"); favorite.ToolTip = "Favorite"; DockPanel.SetDock(favorite, Dock.Right); top.Children.Add(favorite);
            var ellipsis = Ui.Button("···", () => { }); ellipsis.Click += (_, _) => { ellipsis.ContextMenu = Actions(profile); ellipsis.ContextMenu.PlacementTarget = ellipsis; ellipsis.ContextMenu.IsOpen = true; }; AutomationProperties.SetName(ellipsis, "Connection actions"); DockPanel.SetDock(ellipsis, Dock.Right); top.Children.Add(ellipsis); top.Children.Add(Ui.Text("PC Connection", 11, "MutedBrush")); content.Children.Add(top);
            content.Children.Add(Ui.Text(profile.DisplayName, 18)); content.Children.Add(Ui.Text(_vm.CredentialLabel(profile.CredentialId), 12, "MutedBrush")); content.Children.Add(Ui.Text(state.ToString(), 12, state == SessionState.Connected ? "AccentBrush" : "MutedBrush"));
            var card = new Border { Width = width, Margin = new Thickness(0, 0, 14, 14), Padding = new Thickness(12), CornerRadius = new CornerRadius(12), Background = Ui.Brush("PanelBrush"), BorderBrush = Ui.Brush(state == SessionState.Connected || _selected == profile.Id ? "AccentBrush" : "BorderBrush"), BorderThickness = new Thickness(1, 1, 1, state == SessionState.Connected ? 4 : 1), Child = content, Focusable = true, ContextMenu = Actions(profile) };
            AutomationProperties.SetName(card, $"{profile.DisplayName}, {state}, {_vm.CredentialLabel(profile.CredentialId)}"); System.Windows.Input.KeyboardNavigation.SetIsTabStop(card, true);
            card.MouseLeftButtonDown += (_, e) => { if (InsideButton(e.OriginalSource as DependencyObject)) return; if (_selectedCard is not null && _selected is { } oldId && _vm.State(oldId) != SessionState.Connected) _selectedCard.BorderBrush = Ui.Brush("BorderBrush"); _selected = profile.Id; _selectedCard = card; card.Focus(); card.BorderBrush = Ui.Brush("AccentBrush"); if (e.ClickCount == 2) Connect(profile); }; card.KeyDown += (_, e) => { if (e.Key == Key.Enter) { Connect(profile); e.Handled = true; } }; wrap.Children.Add(card);
        }
        return wrap;
    }
    private Border Preview(Guid id, double height)
    {
        var frame = new Border { Height = height, CornerRadius = new CornerRadius(6), Background = Ui.Brush("InputBrush"), ClipToBounds = true };
        var fallback = new Grid();
        var monitor = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        monitor.Children.Add(new Border { Width = 74, Height = 46, BorderBrush = Ui.Brush("MutedBrush"), BorderThickness = new Thickness(3), CornerRadius = new CornerRadius(4), Child = new Border { Margin = new Thickness(6), Background = Ui.Brush("PanelBrush") } });
        monitor.Children.Add(new Border { Width = 4, Height = 9, Background = Ui.Brush("MutedBrush") });
        monitor.Children.Add(new Border { Width = 32, Height = 3, Background = Ui.Brush("MutedBrush"), CornerRadius = new CornerRadius(2) });
        fallback.Children.Add(monitor); frame.Child = fallback;
        frame.ToolTip = _vm.Snapshot.Settings.ScreenshotThumbnails ? "No saved preview yet. A usable remote image is saved when a signed-in session ends; some renderers cannot provide one." : "Previews are off. Enable Save last-session previews in Settings → Personalization.";
        AutomationProperties.SetName(frame, "Computer illustration; no saved preview");
        if (!_vm.Snapshot.Settings.ScreenshotThumbnails) return frame;
        var saved = _vm.Previews.Load(id);
        if (saved is null) return frame;
        try
        {
            using var stream = new MemoryStream(saved.Png);
            var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = 480; bitmap.StreamSource = stream; bitmap.EndInit(); bitmap.Freeze();
            var grid = new Grid(); grid.Children.Add(new Image { Source = bitmap, Stretch = Stretch.Uniform });
            string label = "Last session · " + saved.CapturedAt.ToLocalTime().ToString("g");
            grid.Children.Add(new Border { VerticalAlignment = VerticalAlignment.Bottom, Background = new SolidColorBrush(Color.FromArgb(220, 24, 28, 28)), Padding = new Thickness(5), Child = new TextBlock { Text = label, FontSize = 10, Foreground = Brushes.White, TextWrapping = TextWrapping.Wrap } });
            frame.Child = grid; frame.ToolTip = label + ". Saved image, not a live view.";
            AutomationProperties.SetName(frame, label + "; saved image, not a live view");
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or ArgumentException or System.Runtime.InteropServices.COMException)
        { /* Corrupt or unsupported image retains the illustration. */ }
        finally { CryptographicOperations.ZeroMemory(saved.Png); }
        return frame;
    }
    private void ShowList(List<ConnectionProfile> profiles)
    {
        var grid = new DataGrid { AutoGenerateColumns = false, IsReadOnly = true, CanUserAddRows = false, SelectionMode = DataGridSelectionMode.Single, Background = Ui.Brush("PanelBrush"), Foreground = Ui.Brush("TextBrush"), RowHeight = 40, RowBackground = Ui.Brush("PanelBrush"), AlternatingRowBackground = Ui.Brush("InputBrush") };
        foreach (var field in new[] { ("Name", "Name"), ("Host", "Host"), ("Group", "Group"), ("Credential", "Credential"), ("Status", "Status"), ("Last connected", "LastConnected") }) grid.Columns.Add(new DataGridTextColumn { Header = field.Item1, Binding = new System.Windows.Data.Binding(field.Item2), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        var rows = profiles.Select(p => new LibraryRow(p, p.DisplayName, p.Endpoint, _vm.GroupName(p.GroupId), _vm.CredentialLabel(p.CredentialId), _vm.State(p.Id).ToString(), p.LastConnectedAt?.ToLocalTime().ToString("g") ?? "Never")).ToList(); grid.ItemsSource = rows;
        grid.PreviewMouseRightButtonDown += (_, e) =>
        {
            var target = e.OriginalSource as DependencyObject;
            while (target is not null && target is not DataGridRow) target = VisualTreeHelper.GetParent(target);
            if (target is DataGridRow selectedRow) { selectedRow.IsSelected = true; selectedRow.Focus(); }
        };
        grid.MouseDoubleClick += (_, e) =>
        {
            var target = e.OriginalSource as DependencyObject;
            while (target is not null && target is not DataGridRow) target = target is Visual ? VisualTreeHelper.GetParent(target) : LogicalTreeHelper.GetParent(target);
            if (target is DataGridRow { Item: LibraryRow row }) Connect(row.Profile);
        }; grid.KeyDown += (_, e) => { if (e.Key == Key.Enter && grid.SelectedItem is LibraryRow row) { Connect(row.Profile); e.Handled = true; } }; grid.ContextMenuOpening += (_, e) => { if (grid.SelectedItem is LibraryRow row) grid.ContextMenu = Actions(row.Profile); else e.Handled = true; }; grid.ContextMenu = new ContextMenu(); _content.Content = grid;
    }
    private static bool InsideButton(DependencyObject? target)
    {
        while (target is not null)
        {
            if (target is System.Windows.Controls.Primitives.ButtonBase) return true;
            if (target is Border border && border.Focusable) return false;
            target = target is System.Windows.Media.Visual ? VisualTreeHelper.GetParent(target) : LogicalTreeHelper.GetParent(target);
        }
        return false;
    }
    private sealed record LibraryRow(ConnectionProfile Profile, string Name, string Host, string Group, string Credential, string Status, string LastConnected);
    private ContextMenu Actions(ConnectionProfile profile)
    {
        var menu = new ContextMenu(); menu.Items.Add(Item(_sessions.ContainsKey(profile.Id) ? "Focus session" : "Connect", () => Connect(profile))); menu.Items.Add(Item("Edit", () => Edit(profile))); menu.Items.Add(Item("Duplicate", () => { var copy = profile.DeepClone(); copy.Id = Guid.NewGuid(); copy.CreatedAt = DateTimeOffset.UtcNow; copy.LastConnectedAt = null; copy.FriendlyName = profile.DisplayName + " copy"; Edit(copy); })); menu.Items.Add(Item(profile.Favorite ? "Remove favorite" : "Favorite", () => ToggleFavorite(profile)));
        var move = new MenuItem { Header = "Move to group" }; foreach (var group in _vm.Snapshot.Groups) move.Items.Add(Item(group.Name, () => Ui.Safe(this, () => { var changed = profile.DeepClone(); changed.GroupId = group.Id; _vm.Store.SaveConnection(changed); _vm.Reload(); }))); menu.Items.Add(move); menu.Items.Add(Item("Export RDP…", () => ExportRdp(profile))); menu.Items.Add(new Separator()); menu.Items.Add(Item("Delete profile", () => { if (Ui.Confirm(this, $"Remove {profile.DisplayName} from your library? The remote PC and any open session are unchanged.")) Ui.Safe(this, () => { _vm.Store.DeleteConnection(profile.Id); _vm.Reload(); _vm.Previews.Delete(profile.Id); }); })); return menu;
    }
    private void ToggleFavorite(ConnectionProfile profile) => Ui.Safe(this, () => { var changed = profile.DeepClone(); changed.Favorite = !changed.Favorite; _vm.Store.SaveConnection(changed); _vm.Reload(); });
    private void Edit(ConnectionProfile? profile) => Ui.Safe(this, () => { new PcEditor(this, _vm, profile).ShowDialog(); Refresh(); });
    private void AddGroup() { var name = Ui.Ask(this, "Add group", "Group name"); if (name is not null) Ui.Safe(this, () => { _vm.Store.SaveGroup(new Group { Name = name, Order = _vm.Snapshot.Groups.Count }); _vm.Reload(); }); }
    public void Connect(ConnectionProfile profile)
    {
        if (_sessions.TryGetValue(profile.Id, out var existing)) { existing.Show(); if (existing.WindowState == WindowState.Minimized) existing.WindowState = WindowState.Normal; existing.Activate(); return; }
        Ui.Safe(this, () => { var session = new SessionWindow(_vm, profile); _sessions.Add(profile.Id, session); _vm.OpenSessionCount = _sessions.Count; session.Closed += (_, _) => { _sessions.Remove(profile.Id); _vm.OpenSessionCount = _sessions.Count; _vm.SetSessionState(profile.Id, SessionState.Disconnected, "Session closed"); }; session.Show(); Refresh(); });
    }
    private void OpenSettings() => Ui.Safe(this, () => { new SettingsWindow(this, _vm).ShowDialog(); App.ApplyTheme(_vm.Snapshot.Settings.Theme); Refresh(); ConfigureTray(); });
    private void ShowNotifications() { var text = _vm.Notifications.Count == 0 ? "No local connection problems.\n\nNew-version notices are unavailable until a trusted update source is configured." : string.Join("\n\n", _vm.Notifications); if (_vm.Snapshot.Settings.SmartTips) text += "\n\nTip: double-click a PC or press Enter to connect. An existing session is focused instead of opening a duplicate."; MessageBox.Show(this, text, "SessionDock notifications"); }
    private void ImportRdp() => Ui.Safe(this, () =>
    {
        var pick = new OpenFileDialog { Filter = "Remote Desktop files (*.rdp)|*.rdp", Multiselect = false }; if (pick.ShowDialog(this) != true) return;
        if (new FileInfo(pick.FileName).Length > 2_097_152) throw new FormatException("RDP imports are limited to 2 MiB on disk.");
        var review = RdpFileCodec.Review(File.ReadAllText(pick.FileName)); var dialog = new Window { Owner = this, Title = "Review RDP import", Width = 660, Height = 530, MinHeight = 380, WindowStartupLocation = WindowStartupLocation.CenterOwner }; var panel = Ui.Stack(); panel.Children.Add(Ui.Text(review.Draft.Endpoint, 22)); Ui.Note(panel, "Import creates a saved draft. It never connects automatically. Passwords and executable settings are excluded."); foreach (var issue in review.Issues) panel.Children.Add(Ui.Text($"{issue.Property}: {issue.Message}", 13, "MutedBrush")); var redirect = Ui.Check("Allow the supported device redirection requested by this file", false, panel, review.HasRequestedRedirection); panel.Children.Add(Ui.Button("Review PC settings", () => { if (review.IsValid) dialog.DialogResult = true; else MessageBox.Show(dialog, "Resolve invalid properties before importing."); })); panel.Children.Add(Ui.Button("Cancel", () => dialog.DialogResult = false)); dialog.Content = Ui.Scroll(panel); Ui.FitToWorkArea(dialog); if (dialog.ShowDialog() == true)
        {
            Guid? gatewayId = null; GatewayProfile? stagedGateway = null;
            if (!string.IsNullOrWhiteSpace(review.RequestedGatewayHost))
            {
                if (!Ui.Confirm(this, $"This file requests RD Gateway '{review.RequestedGatewayHost}'. Create that gateway entry and use it for this PC? Cancel stops the import.")) return;
                stagedGateway = new GatewayProfile { Label = review.RequestedGatewayHost, Host = review.RequestedGatewayHost }; gatewayId = stagedGateway.Id;
            }
            new PcEditor(this, _vm, review.Accept(redirect.IsChecked == true, gatewayId), stagedGateway).ShowDialog(); Refresh();
        }
    });
    private void ExportRdp(ConnectionProfile profile) => Ui.Safe(this, () => { var pick = new SaveFileDialog { Filter = "Remote Desktop files (*.rdp)|*.rdp", FileName = "Connection.rdp" }; if (pick.ShowDialog(this) == true) File.WriteAllText(pick.FileName, RdpFileCodec.Export(profile, _vm.Snapshot.Gateways.FirstOrDefault(g => g.Id == profile.GatewayId)), System.Text.Encoding.Unicode); });
    private void ConfigureTray()
    {
        if (!_vm.Snapshot.Settings.TrayEnabled) { _tray?.Dispose(); _tray = null; return; }
        if (_tray is null) { _tray = new Forms.NotifyIcon { Icon = System.Drawing.SystemIcons.Application, Text = "SessionDock", Visible = true }; _tray.DoubleClick += (_, _) => Dispatcher.Invoke(ShowLibrary); }
        _tray.ContextMenuStrip?.Dispose(); var menu = new Forms.ContextMenuStrip(); menu.Items.Add("Open library", null, (_, _) => Dispatcher.Invoke(ShowLibrary)); foreach (var profile in _vm.Snapshot.Connections.Where(c => c.Favorite).Take(15)) menu.Items.Add(profile.DisplayName, null, (_, _) => Dispatcher.Invoke(() => Connect(profile))); menu.Items.Add(new Forms.ToolStripSeparator()); menu.Items.Add("Exit SessionDock", null, (_, _) => Dispatcher.Invoke(ExitApp)); _tray.ContextMenuStrip = menu;
    }
    private void ShowLibrary() { Show(); WindowState = WindowState.Normal; Activate(); }
    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_exiting) return;
        e.Cancel = true;
        if (_vm.Snapshot.Settings.TrayEnabled) { Hide(); return; }
        ExitApp();
    }
    private void ExitApp()
    {
        if (_sessions.Count > 0 && !Ui.Confirm(this, $"Exit and disconnect {_sessions.Count} session window(s)? This disconnects without signing out remote users.")) return;
        _exiting = true; foreach (var session in _sessions.Values.ToArray()) session.CloseWithoutPrompt(); _tray?.Dispose(); System.Windows.Application.Current.Shutdown();
    }
}
