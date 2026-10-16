using System.ComponentModel;
using System.Security.Cryptography;
using System.Windows.Threading;
using SessionDock.Infrastructure;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms.Integration;
using System.Windows.Input;
using SessionDock.App.ViewModels;
using SessionDock.Core;
using SessionDock.Rdp.Windows;

namespace SessionDock.App.Views;

internal sealed class SessionWindow : Window
{
    private readonly LibraryViewModel _vm;
    private readonly ConnectionProfile _profile;
    private readonly WindowsFormsHost _host = new();
    private readonly TextBlock _status = Ui.Text("Disconnected. Preparing the installed RDP control.", 13);
    private readonly Button _connect, _disconnect, _fullscreen;
    private RdpSessionControl? _control;
    private bool _fullScreen, _closeWithoutPrompt, _fullScreenTransition;
    private WindowState _savedWindowState;
    private Rect _savedBounds;
    private readonly DispatcherTimer _previewTimer = new() { Interval = TimeSpan.FromSeconds(5) };
    private SavedPreview? _lastPreview;
    private string _lastStatusMessage = "";
    private bool _previewWarningShown;
    public SessionWindow(LibraryViewModel vm, ConnectionProfile profile)
    {
        _vm = vm; _profile = profile.DeepClone(); Title = $"{profile.DisplayName} — SessionDock"; Width = 1100; Height = 750; MinWidth = 600; MinHeight = 380; Ui.FitToWorkArea(this);
        var root = new DockPanel(); var toolbar = new WrapPanel { Margin = new Thickness(5) }; DockPanel.SetDock(toolbar, Dock.Top);
        _connect = Ui.Button("Connect / reconnect", StartConnection); _disconnect = Ui.Button("Disconnect", () => Ui.Safe(this, DisconnectWithPreview)); _fullscreen = Ui.Button("Full screen", () => Ui.Safe(this, () => RequestFullScreen(!_fullScreen)));
        _disconnect.IsEnabled = false; _fullscreen.IsEnabled = false;
        toolbar.Children.Add(_connect); toolbar.Children.Add(_disconnect); toolbar.Children.Add(_fullscreen);
        toolbar.Children.Add(Ui.Button("Remote security keys…", () => MessageBox.Show(this, "Focus the remote desktop and press Ctrl+Alt+End to open its Windows security screen. Automated key injection is unavailable.", "Remote security keys")));
        toolbar.Children.Add(Ui.Button("Connection details", Details)); root.Children.Add(toolbar);
        var statusPanel = new StackPanel { Margin = new Thickness(12, 4, 12, 8) }; statusPanel.Children.Add(_status); _status.ToolTip = "Open Connection details for host-policy limits and diagnostics."; DockPanel.SetDock(statusPanel, Dock.Bottom); root.Children.Add(statusPanel); root.Children.Add(_host); Content = root;
        _previewTimer.Tick += (_, _) => CapturePreview();
        _vm.Changed += OnLibraryChanged;
        Loaded += (_, _) => Initialize(); Closing += OnClosing; Closed += (_, _) =>
        {
            _previewTimer.Stop(); _vm.Changed -= OnLibraryChanged; ForgetPreview();
            if (_control is not null) _control.StateChanged -= OnStateChanged;
            try { _host.Child = null; } catch (Exception) { }
            try { _control?.Dispose(); } catch (Exception) { }
            try { _host.Dispose(); } catch (Exception) { }
        };
        _host.SizeChanged += (_, _) => ResizeRemote(); DpiChanged += (_, _) => ResizeRemote();
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Home && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Alt)) { _fullscreen.Focus(); e.Handled = true; } else if (e.Key == Key.F11 && Keyboard.Modifiers == ModifierKeys.Control) { RequestFullScreen(!_fullScreen); e.Handled = true; } };
    }
    private void Initialize()
    {
        try
        {
            _control = new RdpSessionControl(); ((ISupportInitialize)_control).BeginInit(); _host.Child = _control; ((ISupportInitialize)_control).EndInit();
            _control.StateChanged += OnStateChanged; _control.FullScreenRequested += (_, _) => RequestFullScreen(true); _control.LeaveFullScreenRequested += (_, _) => RequestFullScreen(false); _control.MinimizeRequested += (_, _) => WindowState = WindowState.Minimized; _control.FocusReleased += (_, _) => _fullscreen.Focus();
            StartConnection();
        }
        catch (Exception ex) { _status.Text = "RDP control activation failed: " + ex.Message; _vm.SetSessionState(_profile.Id, SessionState.Failed, "The installed Remote Desktop control could not be activated."); }
    }
    private void StartConnection()
    {
        if (_control is null) { _status.Text = "RDP control unavailable. Close this window and run the Windows engine probe."; return; }
        if (_control.State is not (SessionState.Disconnected or SessionState.Failed)) { MessageBox.Show(this, "Disconnect the current session before connecting again."); return; }
        try
        {
            using var credentials = ResolveCredentials(_profile.CredentialId, "Destination PC credentials"); if (credentials is null) { _status.Text = "Connection cancelled before authentication."; return; }
            GatewayProfile? gateway = null; SessionCredentials? gatewayCredentials = null;
            try
            {
                if (_profile.GatewayId is { } id)
                {
                    gateway = _vm.Snapshot.Gateways.FirstOrDefault(g => g.Id == id) ?? throw new InvalidOperationException("This profile's gateway no longer exists. Edit the PC before connecting.");
                    gatewayCredentials = ResolveCredentials(gateway.CredentialId, "RD Gateway credentials"); if (gatewayCredentials is null) { _status.Text = "Gateway authentication cancelled. No connection started."; return; }
                }
                ForgetPreview();
                _control.AuthenticationDialogParentWindow = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                _control.Connect(_profile, credentials, gateway, gatewayCredentials);
            }
            finally { gatewayCredentials?.Dispose(); }
        }
        catch (Exception ex) { _status.Text = ex.Message; _vm.SetSessionState(_profile.Id, SessionState.Failed, "The connection could not be started. See its session window for details."); }
    }
    private SessionCredentials? ResolveCredentials(Guid? id, string title)
    {
        var metadata = _vm.Snapshot.Credentials.FirstOrDefault(c => c.Id == id);
        if (metadata is not null)
        {
            using var secret = _vm.Vault.Read(metadata.Id);
            if (secret is not null) return new SessionCredentials(metadata.Username, secret, metadata.Domain);
        }
        var prompt = new CredentialPrompt(this, title, metadata); return prompt.ShowDialog() == true ? prompt.Credentials : null;
    }
    private void OnStateChanged(object? sender, RdpStateChangedEventArgs e)
    {
        _lastStatusMessage = e.Message;
        _status.ToolTip = e.Message;
        if (e.State == SessionState.Connected && _vm.Snapshot.Settings.ScreenshotThumbnails) _previewTimer.Start();
        else _previewTimer.Stop();
        if (e.State is SessionState.Disconnected or SessionState.Failed) SavePreview();
        _status.Text = e.State == SessionState.Connected && e.Message == "Signed in. Requested features may be restricted by remote host policy." ? "Connected · Host policies may limit features." : $"{e.State}: {e.Message}" + (e.Code is { } code ? $" (code {code})" : ""); _vm.SetSessionState(_profile.Id, e.State, e.Message, e.Code);
        _connect.IsEnabled = e.State is SessionState.Disconnected or SessionState.Failed; _disconnect.IsEnabled = e.State is SessionState.Connecting or SessionState.Authenticating or SessionState.Connected or SessionState.Reconnecting; _fullscreen.IsEnabled = e.State == SessionState.Connected && _control?.CanFullScreen == true;
        _fullscreen.ToolTip = _fullscreen.IsEnabled ? "Full screen; Ctrl+Alt+Break toggles via the native control" : "Requires a validated Windows-generated interop wrapper; use the resizable window";
        if (e.State == SessionState.Connected) { _control?.Focus(); ResizeRemote(); }
        if (e.State is SessionState.Disconnected or SessionState.Failed) SetFullScreen(false);
    }
    private void OnLibraryChanged(object? sender, EventArgs e)
    {
        if (!_vm.Snapshot.Settings.ScreenshotThumbnails) { _previewTimer.Stop(); ForgetPreview(); }
        else if (_control?.State == SessionState.Connected) _previewTimer.Start();
    }
    private void CapturePreview()
    {
        if (!_vm.Snapshot.Settings.ScreenshotThumbnails || _control?.State != SessionState.Connected || WindowState == WindowState.Minimized) return;
        var png = RdpPreviewCapture.TryCapture(_control);
        if (png is null) return;
        ForgetPreview(); _lastPreview = new(DateTimeOffset.UtcNow, png);
    }
    private void ForgetPreview()
    {
        if (_lastPreview is not null) CryptographicOperations.ZeroMemory(_lastPreview.Png);
        _lastPreview = null;
    }
    private void SavePreview()
    {
        if (_lastPreview is null) return;
        try
        {
            if (_vm.Snapshot.Settings.ScreenshotThumbnails && _vm.Snapshot.Connections.Any(p => p.Id == _profile.Id))
            { _vm.Previews.Save(_profile.Id, _lastPreview); _vm.PreviewChanged(); }
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException or Win32Exception or ArgumentException)
        {
            if (!_previewWarningShown) { _vm.Notifications.Insert(0, "A last-session preview could not be saved. The connection was not affected."); _previewWarningShown = true; }
        }
        finally { ForgetPreview(); }
    }
    private void DisconnectWithPreview()
    {
        CapturePreview(); SavePreview(); _control?.Disconnect();
    }
    private void ResizeRemote()
    {
        if (_control is null || !_profile.Display.DynamicResolution) return;
        var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(_host);
        _control.ResizeRemote((int)Math.Clamp(_host.ActualWidth * dpi.DpiScaleX, 200, 8192), (int)Math.Clamp(_host.ActualHeight * dpi.DpiScaleY, 200, 8192));
    }
    private void RequestFullScreen(bool full)
    {
        if (_fullScreenTransition || _fullScreen == full) return;
        _fullScreenTransition = true;
        try { if (_control?.SetFullScreen(full) == true) SetFullScreen(full); }
        finally { _fullScreenTransition = false; }
    }
    private void SetFullScreen(bool full)
    {
        if (_fullScreen == full) return;
        if (full)
        {
            _savedWindowState = WindowState; _savedBounds = new Rect(Left, Top, Width, Height); WindowState = WindowState.Normal; WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; WindowState = WindowState.Maximized;
        }
        else
        {
            WindowState = WindowState.Normal; WindowStyle = WindowStyle.SingleBorderWindow; ResizeMode = ResizeMode.CanResize; Left = _savedBounds.Left; Top = _savedBounds.Top; Width = _savedBounds.Width; Height = _savedBounds.Height; WindowState = _savedWindowState;
        }
        _fullScreen = full; _fullscreen.Content = full ? "Exit full screen" : "Full screen";
    }
    private void Details()
    {
        var limitations = _control?.Limitations ?? [];
        MessageBox.Show(this, $"Endpoint: {_profile.Endpoint}\nServer certificate authentication: {_profile.CertificatePolicy}\nControl version: {_control?.ControlVersion ?? "Unavailable"}\nState: {_control?.State.ToString() ?? "Disconnected"}\nLast status: {_lastStatusMessage}\n\nConnected is shown only after the native control reports completed logon. Device redirection is requested, not remotely verified.\n\n" + string.Join("\n\n", limitations), "Connection details");
    }
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (!_closeWithoutPrompt && _control?.State is SessionState.Connected or SessionState.Connecting or SessionState.Authenticating or SessionState.Reconnecting)
        {
            if (!Ui.Confirm(this, "Disconnect and close this session? The remote user stays signed in.")) { e.Cancel = true; return; }
        }
        try { DisconnectWithPreview(); } catch { /* Disposing the control below releases local resources even after COM failure. */ }
    }
    public void CloseWithoutPrompt() { _closeWithoutPrompt = true; Close(); }
}
