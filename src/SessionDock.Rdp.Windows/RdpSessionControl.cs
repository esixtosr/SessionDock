using System.ComponentModel;
using System.Reflection;
using System.Runtime.InteropServices;
using SessionDock.Core;

namespace SessionDock.Rdp.Windows;

public sealed record RdpOptionProbeResult(string Target, string Member, bool Accepted, int? ErrorCode, string? Diagnostic = null);

public sealed class RdpStateChangedEventArgs(SessionState state, string message, int? code = null) : EventArgs
{
    public SessionState State { get; } = state;
    public string Message { get; } = message;
    public int? Code { get; } = code;
}

/// <summary>One app-owned native ActiveX surface per session. All access must be on its creating STA.</summary>
public sealed class RdpSessionControl : AxHost
{
    public const string ControlClassId = "A0C63C30-F08D-4AB4-907C-34905D770C7D";
    private readonly int _threadId = Environment.CurrentManagedThreadId;
    private readonly System.Windows.Forms.Timer _resizeTimer;
    private ConnectionPointCookie? _events;
    private RdpEventSink? _sink;
    private object? _client, _advanced, _secured, _transport;
    private bool _deliberateDisconnect, _reconnectAllowed, _loggedOn, _failed, _dynamicResolution;
    private bool _disposed, _startedFullScreen, _fullScreen, _canFullScreen, _usesGateway, _nativeDisconnectPending;
    private RdpCertificatePolicy _certificatePolicy;
    private bool _authenticationWarningPending, _authenticationWarningSeen;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IntPtr AuthenticationDialogParentWindow { get; set; }
    private int _generation;
    private (int Width, int Height) _pendingSize;
    private readonly List<string> _limitations = [];

    public RdpSessionControl() : base(ControlClassId)
    {
        EnsureSta();
        _resizeTimer = new System.Windows.Forms.Timer { Interval = 300 };
        _resizeTimer.Tick += (_, _) => { _resizeTimer.Stop(); ApplyResize(); };
        Dock = DockStyle.Fill;
        TabStop = true;
        AccessibleName = "Remote desktop session";
    }
    public new SessionState State { get; private set; } = SessionState.Disconnected;
    public string ControlVersion { get; private set; } = "Not activated";
    public IReadOnlyList<string> Limitations => _limitations.AsReadOnly();
    public bool CanSendCtrlAltDelete => false;
    public bool CanFullScreen => _canFullScreen;
    public IReadOnlyList<Capability> GetCapabilityReport() =>
    [
        new("Integrated RDP lifecycle", CapabilityStatus.Conditional, $"Control {ControlVersion}; real login and remote input require interactive Windows tests."),
        new("Server authentication and NLA", CapabilityStatus.Conditional, "Strict server authentication by default; optional native warning prompts. CredSSP stays enabled; real certificate and bad-credential tests pending."),
        new("Full screen", _canFullScreen ? CapabilityStatus.Conditional : CapabilityStatus.Unavailable, _canFullScreen ? "Generated non-scriptable interop configured; native full screen and keyboard escape need Windows tests." : "Generated interop or ConnectionBarText setup unavailable."),
        new("Display resize and scaling", CapabilityStatus.Conditional, "Independent SmartSizing and debounced display update requests; effective host behavior unverified."),
        new("Clipboard, sound, printers, smart cards, microphone", CapabilityStatus.Conditional, "Request mapping implemented; policy, devices, and both clipboard directions need tests."),
        new("RD Gateway", _transport is null ? CapabilityStatus.Unavailable : CapabilityStatus.Conditional, "Separate gateway credentials; real gateway, bypass, and proxy tests pending."),
        new("Exact folder sharing and read-only", CapabilityStatus.Unavailable, "No folder request is converted into whole-drive sharing."),
        new("Last-session previews", CapabilityStatus.Conditional, "Optional app-window compositor capture cropped to the RDP client, with native rendering fallback; actual remote capture requires interactive verification."),
        new("Directional clipboard, camera, multi-monitor, remote DPI, mouse swap", CapabilityStatus.Unavailable, "No verified implementation; UI must keep controls unavailable."),
        new("Cloud workspaces, published apps, Teams, Shortpath", CapabilityStatus.Deferred, "Outside the saved-PC release.")
    ];
    /// <summary>Applies preconnection settings only. Intended for the explicit no-network Windows probe.
    /// Never calls Connect, uses no real credentials, and returns property names/codes without values.</summary>
    public IReadOnlyList<RdpOptionProbeResult> ProbeOptionMappings()
    {
        EnsureSta();
        if (State != SessionState.Disconnected || _client is null || _advanced is null || _secured is null || _events is null)
            throw new InvalidOperationException("Option probing requires an activated, disconnected control.");
        var gateway = new GatewayProfile { Host = "sessiondock-gateway-probe.invalid" };
        var profile = new ConnectionProfile { Host = "sessiondock-pc-probe.invalid", GatewayId = gateway.Id };
        var options = RdpOptionPlan.Create(profile, gateway, 1024, 768).ToList();
        profile.CertificatePolicy = RdpCertificatePolicy.Interactive;
        options.Add(RdpOptionPlan.Create(profile, gateway, 1024, 768).Single(x => x.Member == "AuthenticationLevel"));
        List<RdpOptionProbeResult> results = [];
        foreach (var option in options)
        {
            try
            {
                object? target = option.Target switch
                {
                    RdpOptionTarget.Client => _client,
                    RdpOptionTarget.Advanced => _advanced,
                    RdpOptionTarget.Secured => _secured,
                    RdpOptionTarget.Transport => _transport,
                    _ => null
                };
                if (target is null) throw new NotSupportedException();
                ComDispatch.Set(target, option.Member, option.Value);
                results.Add(new(option.Target.ToString(), option.Member, true, null));
            }
            catch (Exception ex) when (ex is COMException or TargetInvocationException or MissingMethodException or NotSupportedException)
            { results.Add(new(option.Target.ToString(), option.Member, false, ComDispatch.ErrorCode(ex))); }
        }
        using var probePassword = new System.Security.SecureString();
        // Reserved synthetic marker only, never a real credential or a network connection.
        foreach (char character in "SessionDock-Probe-Only!42") probePassword.AppendChar(character);
        probePassword.MakeReadOnly();
        using var emptyPassword = new System.Security.SecureString();
        foreach (bool gatewayPassword in new[] { false, true })
        {
            string targetName = gatewayPassword ? "Transport" : "NonScriptable";
            string member = gatewayPassword ? "GatewayPassword" : "ClearTextPassword";
            foreach (bool clear in new[] { false, true })
            {
                try
                {
                    var password = clear ? emptyPassword : probePassword;
                    if (gatewayPassword) NativePasswordSetter.SetGateway(_transport ?? throw new NotSupportedException(), password);
                    else NativePasswordSetter.SetDesktop(_client, password);
                    results.Add(new(targetName, clear ? member + " (clear)" : member, true, null));
                }
                catch (Exception ex) when (ex is COMException or TargetInvocationException or MissingMethodException or NotSupportedException)
                { results.Add(new(targetName, clear ? member + " (clear)" : member, false, ComDispatch.ErrorCode(ex), ex is NotSupportedException ? ex.Message : "Native password setter rejected the request.")); }
            }
        }
        if (_transport is not null) ComDispatch.TrySet(_transport, "GatewayUsageMethod", 0u);
        return results;
    }
    public event EventHandler<RdpStateChangedEventArgs>? StateChanged;
    public event EventHandler? FullScreenRequested;
    public event EventHandler? LeaveFullScreenRequested;
    public event EventHandler? MinimizeRequested;
    public event EventHandler? FocusReleased;

    protected override void AttachInterfaces()
    {
        _client = GetOcx() ?? throw new InvalidOperationException("The RDP ActiveX object is unavailable.");
        ControlVersion = Convert.ToString(ComDispatch.Get(_client, "Version")) ?? "Unknown";
        // Select the newest available scriptable settings interface. Missing required properties
        // fail the connection; there is no silent option drop or security downgrade.
        _advanced = FirstAvailable(_client, "AdvancedSettings9", "AdvancedSettings8", "AdvancedSettings7");
        _secured = FirstAvailable(_client, "SecuredSettings3", "SecuredSettings2");
        _transport = FirstAvailable(_client, "TransportSettings4", "TransportSettings3", "TransportSettings2", "TransportSettings");
        _canFullScreen = GeneratedInteropBridge.TrySetConnectionBarText(_client);
    }
    protected override void CreateSink()
    {
        _sink = new RdpEventSink(this);
        _events = new ConnectionPointCookie(GetOcx() ?? throw new InvalidOperationException("The RDP ActiveX object is unavailable."), _sink, typeof(IRdpControlEvents));
    }
    protected override void DetachSink()
    {
        _events?.Disconnect();
        _events = null;
        _sink = null;
        base.DetachSink();
    }

    public void Connect(ConnectionProfile profile, SessionCredentials credentials, GatewayProfile? gateway = null, SessionCredentials? gatewayCredentials = null)
    {
        EnsureSta();
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_nativeDisconnectPending) throw new InvalidOperationException("The previous session is still disconnecting. Wait for native cleanup before retrying.");
        if (State is not (SessionState.Disconnected or SessionState.Failed)) throw new InvalidOperationException("This session is already active. Disconnect before connecting again.");
        // Construct the validated request before initiating any network access.
        var options = RdpOptionPlan.Create(profile, gateway, ClientSize.Width, ClientSize.Height);
        if (!IsHandleCreated) CreateControl();
        if (_client is null || _advanced is null || _secured is null || _events is null)
            throw new InvalidOperationException("The installed Microsoft Remote Desktop ActiveX control could not be activated. Install and run on supported Windows 11 x64, then run the engine probe.");
        if (Convert.ToInt32(ComDispatch.Get(_client, "Connected")) != 0)
            throw new InvalidOperationException("The native RDP control is still active. Close this session window before starting another attempt.");
        _generation++;
        _deliberateDisconnect = false;
        _loggedOn = false;
        _failed = false;
        _reconnectAllowed = profile.AutoReconnect;
        _certificatePolicy = profile.CertificatePolicy;
        _authenticationWarningPending = _authenticationWarningSeen = false;
        _dynamicResolution = profile.Display.DynamicResolution;
        _startedFullScreen = profile.Display.StartFullScreen;
        _fullScreen = false;
        _usesGateway = gateway is not null;
        _limitations.Clear();
        _limitations.Add("Requested redirection and display settings remain subject to remote host policy. Effective behavior has not been measured by this client.");
        try
        {
            foreach (var option in options)
            {
                object target = option.Target switch
                {
                    RdpOptionTarget.Client => _client,
                    RdpOptionTarget.Advanced => _advanced,
                    RdpOptionTarget.Secured => _secured,
                    RdpOptionTarget.Transport => _transport ?? throw new NotSupportedException("The installed control does not expose RD Gateway settings."),
                    _ => throw new ArgumentOutOfRangeException()
                };
                ComDispatch.Set(target, option.Member, option.Value);
            }
            if (gateway is null && _transport is not null)
                ComDispatch.Set(_transport, "GatewayUsageMethod", 0u);
            if (!GeneratedInteropBridge.TrySetAuthenticationDialogParent(_client, AuthenticationDialogParentWindow != IntPtr.Zero ? AuthenticationDialogParentWindow : Handle))
                _limitations.Add("Native dialog parenting is unavailable. Check Windows for the security dialog; no warning is automatically approved.");
            _canFullScreen = GeneratedInteropBridge.TrySetConnectionBarText(_client);
            if (!_canFullScreen) _limitations.Add("Native full screen requires the generated Windows interop wrapper and successful ConnectionBarText setup.");
            ComDispatch.Set(_client, "UserName", credentials.Username);
            ComDispatch.Set(_client, "Domain", credentials.Domain);
            NativePasswordSetter.SetDesktop(_client, credentials.Password);
            if (gateway is not null && gatewayCredentials is not null)
            {
                ComDispatch.Set(_transport!, "GatewayUsername", gatewayCredentials.Username);
                ComDispatch.Set(_transport!, "GatewayDomain", gatewayCredentials.Domain);
                NativePasswordSetter.SetGateway(_transport!, gatewayCredentials.Password);
            }
            SetState(SessionState.Connecting, "Connecting; successful sign-in has not yet been confirmed.");
            ComDispatch.Call(_client, "Connect");
        }
        catch (Exception ex) when (ex is COMException or TargetInvocationException or MissingMethodException or NotSupportedException)
        {
            _reconnectAllowed = false;
            _failed = true;
            SetState(SessionState.Failed, "The installed RDP control could not apply a required setting or begin the connection. No security setting was relaxed.", ComDispatch.ErrorCode(ex));
            ClearControlPasswords();
        }
    }

    public void Disconnect()
    {
        EnsureSta();
        if (_disposed) return;
        DisconnectCore();
    }
    private void DisconnectCore()
    {
        _deliberateDisconnect = true;
        _reconnectAllowed = false;
        _resizeTimer.Stop();
        if (!HasAttachedClient()) return;
        if (_advanced is not null) ComDispatch.TrySet(_advanced, "EnableAutoReconnect", false);
        if (State is SessionState.Disconnected or SessionState.Failed) { ClearControlPasswords(); return; }
        _nativeDisconnectPending = true;
        SetState(SessionState.Disconnecting, "Disconnecting. The remote user will not be signed out.");
        try { ComDispatch.Call(_client, "Disconnect"); }
        catch (Exception ex) when (ex is COMException or TargetInvocationException)
        {
            _nativeDisconnectPending = false;
            SetState(SessionState.Failed, "The disconnect request failed. Close this session window to release its local control.", ComDispatch.ErrorCode(ex));
        }
        ClearControlPasswords();
    }

    /// <summary>Queues a debounced remote-resolution request; true means queued, not confirmed by the host.</summary>
    public bool ResizeRemote(int width, int height)
    {
        EnsureSta();
        if (!_dynamicResolution || State != SessionState.Connected || width is < 200 or > 8192 || height is < 200 or > 8192) return false;
        _pendingSize = (width, height);
        _resizeTimer.Stop();
        _resizeTimer.Start();
        return true;
    }
    private void ApplyResize()
    {
        if (_client is null || State != SessionState.Connected || !_dynamicResolution) return;
        try
        {
            // Zero physical dimensions defer to the control; the base request is 100% remote DPI.
            // High-DPI negotiation remains unavailable until a separate per-monitor Windows test.
            ComDispatch.Call(_client, "UpdateSessionDisplaySettings", (uint)_pendingSize.Width, (uint)_pendingSize.Height, 0u, 0u, 0u, 100u, 100u);
        }
        catch (Exception ex) when (ex is COMException or TargetInvocationException or MissingMethodException)
        {
            _dynamicResolution = false;
            const string message = "The remote host rejected dynamic resizing. This session remains open at its previous remote resolution; Fit to window can scale its image.";
            _limitations.Add(message);
            SetState(State, message, ComDispatch.ErrorCode(ex));
        }
    }
    public bool SetFullScreen(bool fullScreen)
    {
        EnsureSta();
        if (_client is null || State != SessionState.Connected) return false;
        if (!_canFullScreen)
        {
            SetState(State, "Native full screen is unavailable until the Windows-generated interop wrapper passes ConnectionBarText setup. Keep using the resizable session window.");
            return false;
        }
        if (_fullScreen == fullScreen) return true;
        _fullScreen = fullScreen;
        try { ComDispatch.Set(_client, "FullScreen", fullScreen); return true; }
        catch (Exception ex) when (ex is COMException or TargetInvocationException or MissingMethodException)
        {
            _fullScreen = !fullScreen;
            SetState(State, "The native RDP control rejected the full-screen change. The session remains open.", ComDispatch.ErrorCode(ex));
            return false;
        }
    }
    public bool SendCtrlAltDelete()
    {
        EnsureSta();
        SetState(State, "Focus the remote desktop and press Ctrl+Alt+End for its Windows security screen. Toolbar injection has not passed Windows validation.");
        return false;
    }

    internal void HandleConnecting() { if (!_deliberateDisconnect && !_failed && !_nativeDisconnectPending) SetState(SessionState.Connecting, "Connecting…"); }
    internal void HandleAuthenticating() { if (!_deliberateDisconnect && !_failed && !_nativeDisconnectPending) SetState(SessionState.Authenticating, "Transport connected. Waiting for successful sign-in…"); }
    internal void HandleLoginComplete()
    {
        if (_deliberateDisconnect || _failed || _nativeDisconnectPending || State == SessionState.Disconnecting) { QueueDisconnect(); return; }
        _authenticationWarningPending = false;
        _loggedOn = true;
        _failed = false;
        SetState(SessionState.Connected, "Signed in. Requested features may be restricted by remote host policy.");
        if (_startedFullScreen) BeginInvoke(() => SetFullScreen(true));
    }
    internal void HandleDisconnected(int reason)
    {
        _nativeDisconnectPending = false;
        _authenticationWarningPending = false;
        _fullScreen = false;
        _resizeTimer.Stop();
        ClearControlPasswords();
        if (_failed) return;
        int extended = 0;
        try { if (_client is not null) extended = Convert.ToInt32(ComDispatch.Get(_client, "ExtendedDisconnectReason")); }
        catch (Exception ex) when (ex is COMException or TargetInvocationException or MissingMethodException) { }
        bool clean = _deliberateDisconnect || (_loggedOn && reason is 1 or 2 or 3 && extended is 0 or 1 or 2 or 11 or 12);
        _reconnectAllowed = false;
        var message = _deliberateDisconnect ? "Disconnected. SessionDock sent no remote sign-out request."
            : clean && extended is not (2 or 12) ? "The session ended. SessionDock sent no remote sign-out request."
            : _authenticationWarningSeen && !_loggedOn
                ? "Windows ended the connection without confirming sign-in after a security warning. The warning may have been cancelled or blocked by policy. Verify the server identity and certificate before manually retrying."
                : RdpFailureDescriptions.Describe(reason, extended, _usesGateway);
        if (extended != 0) message += $" Extended reason: {extended}.";
        SetState(clean ? SessionState.Disconnected : SessionState.Failed, message, reason);
    }
    internal void HandleFatalError(int code)
    {
        _reconnectAllowed = false;
        _failed = true;
        SetState(SessionState.Failed, "The RDP control reported a fatal error. Automatic retry is stopped.", code);
        QueueDisconnect();
    }
    internal void HandleWarning(int code) => SetState(State, "The RDP control reported a warning. Check the remote session before relying on requested features.", code);
    internal bool HandleAutoReconnect(int attempt)
    {
        if (_deliberateDisconnect || !_reconnectAllowed || _failed || !_loggedOn || attempt > 3) return false;
        SetState(SessionState.Reconnecting, $"Reconnecting after interruption (attempt {attempt} of at most 3).");
        return true;
    }
    internal void HandleAutoReconnected()
    {
        if (_deliberateDisconnect || !_reconnectAllowed || _failed || _nativeDisconnectPending) { QueueDisconnect(); return; }
        SetState(_loggedOn ? SessionState.Connected : SessionState.Authenticating, _loggedOn ? "The control restored the previously signed-in session." : "Reconnected transport; waiting for successful sign-in.");
    }
    internal void HandleAuthenticationWarning()
    {
        if (_deliberateDisconnect || _failed || _nativeDisconnectPending) return;
        _reconnectAllowed = false;
        if (_advanced is not null) ComDispatch.TrySet(_advanced, "EnableAutoReconnect", false);
        _authenticationWarningPending = _authenticationWarningSeen = true;
        SetState(SessionState.Authenticating, _certificatePolicy == RdpCertificatePolicy.Interactive
            ? "Windows is showing a security or certificate warning. Verify the server identity independently before proceeding; an unverified identity is a security risk. Windows policy may block access. Automatic retries are stopped."
            : "Windows is showing a security or certificate warning. Strict mode requires successful server authentication; untrusted certificates are rejected. Automatic retries are stopped.");
    }
    internal void HandleAuthenticationWarningDismissed()
    {
        if (!_authenticationWarningPending || _deliberateDisconnect || _failed || _nativeDisconnectPending) return;
        _authenticationWarningPending = false;
        // Dismissal supplies no approval/cancellation result. Only native login completion is success.
        if (!_loggedOn) SetState(SessionState.Authenticating, "Windows security warning closed. Waiting for Windows to confirm sign-in or disconnection; automatic retries remain stopped.");
    }
    internal void HandleLogonError(int code)
    {
        if (_deliberateDisconnect || _failed || _nativeDisconnectPending) return;
        // Negative arbitration notifications -2/-4/-5 and warning 3 are not bad credentials.
        if (code is -2 or -4 or -5 or 3) { SetState(SessionState.Authenticating, "Windows requires your attention in the remote sign-in screen.", code); return; }
        _reconnectAllowed = false;
        _failed = true;
        SetState(SessionState.Failed, code is 1 or unchecked((int)0xC0000224)
            ? "The account password must be changed. Automatic retry is stopped."
            : "Sign-in failed or account policy blocked access. Check the credentials and account status; automatic retry is stopped.", code);
        QueueDisconnect();
    }
    internal void HandleFullScreen(bool enter) { if (enter) FullScreenRequested?.Invoke(this, EventArgs.Empty); else LeaveFullScreenRequested?.Invoke(this, EventArgs.Empty); }
    internal void HandleMinimize() => MinimizeRequested?.Invoke(this, EventArgs.Empty);
    internal void HandleFocusReleased() => FocusReleased?.Invoke(this, EventArgs.Empty);
    private void QueueDisconnect()
    {
        if (_disposed || !IsHandleCreated || _nativeDisconnectPending) return;
        _nativeDisconnectPending = true;
        int generation = _generation;
        BeginInvoke(() =>
        {
            // A queued cleanup is tied to its original attempt and cannot cancel a later one.
            if (_disposed || generation != _generation) return;
            if (_advanced is not null) ComDispatch.TrySet(_advanced, "EnableAutoReconnect", false);
            try
            {
                if (_client is null || Convert.ToInt32(ComDispatch.Get(_client, "Connected")) == 0)
                    _nativeDisconnectPending = false;
                else ComDispatch.Call(_client, "Disconnect");
            }
            catch (Exception ex) when (ex is COMException or TargetInvocationException) { _nativeDisconnectPending = false; }
            ClearControlPasswords();
        });
    }
    private void ClearControlPasswords()
    {
        // Clearing credentials only after disconnect/failure preserves native reconnect semantics.
        if (!HasAttachedClient()) return;
        using var emptyPassword = new System.Security.SecureString();
        try { if (_client is not null) NativePasswordSetter.SetDesktop(_client, emptyPassword); }
        catch (Exception ex) when (ex is COMException or NotSupportedException) { }
        try { if (_transport is not null) NativePasswordSetter.SetGateway(_transport, emptyPassword); }
        catch (Exception ex) when (ex is COMException or NotSupportedException) { }
    }
    private void SetState(SessionState state, string message, int? code = null)
    {
        State = state;
        StateChanged?.Invoke(this, new RdpStateChangedEventArgs(state, message, code));
    }
    private static object? FirstAvailable(object target, params string[] members)
    {
        foreach (string member in members)
        {
            try { return ComDispatch.Get(target, member); }
            catch (Exception ex) when (ex is COMException or TargetInvocationException or MissingMethodException) { }
        }
        return null;
    }
    private void EnsureSta()
    {
        if (Environment.CurrentManagedThreadId != _threadId || Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
            throw new InvalidOperationException("RDP controls must be created, accessed, and disposed on the same Windows STA UI thread.");
    }
    [System.Diagnostics.CodeAnalysis.MemberNotNullWhen(true, nameof(_client))]
    private bool HasAttachedClient()
    {
        // A parent can release its child ActiveX objects before calling the child's Dispose.
        // GetOcx only reads AxHost's current RCW; checking it never invokes a released COM object.
        if (_client is not null && ReferenceEquals(_client, GetOcx())) return true;
        ForgetInterfaces();
        return false;
    }
    private void ForgetInterfaces()
    {
        _client = _advanced = _secured = _transport = null;
        _canFullScreen = _fullScreen = _nativeDisconnectPending = false;
    }
    protected override void Dispose(bool disposing)
    {
        if (!disposing) { base.Dispose(false); return; }
        if (_disposed) return;
        EnsureSta();
        // Mark before native callbacks so reentrant or repeated disposal cannot repeat cleanup.
        _disposed = true;
        try { DisconnectCore(); }
        finally
        {
            _resizeTimer.Dispose();
            ForgetInterfaces();
            // AxHost owns/releases its RCW. Always let it finish, even when native cleanup fails.
            base.Dispose(true);
        }
    }
}
