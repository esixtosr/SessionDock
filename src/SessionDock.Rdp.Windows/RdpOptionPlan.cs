using SessionDock.Core;

namespace SessionDock.Rdp.Windows;

public enum RdpOptionTarget { Client, Advanced, Secured, Transport }
public sealed record RdpOption(RdpOptionTarget Target, string Member, object Value);

/// <summary>Pure, password-free mapping. Requests for unimplemented features fail before connecting.</summary>
public static class RdpOptionPlan
{
    public static IReadOnlyList<RdpOption> Create(ConnectionProfile profile, GatewayProfile? gateway, int viewportWidth, int viewportHeight)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (profile.Port is < 1 or > 65535) throw new ArgumentOutOfRangeException(nameof(profile), "Port must be 1–65535.");
        if (!EndpointParser.TryParse(profile.Endpoint, out _, out var error)) throw new ArgumentException(error, nameof(profile));
        if (profile.SwapMouseButtons) throw new NotSupportedException("Session-local mouse swapping is unavailable; local Windows mouse settings are never changed.");
        if (profile.Display.UseAllMonitors) throw new NotSupportedException("Multiple-monitor redirection is awaiting Windows interop and host verification.");
        if (profile.Display.HighDpi) throw new NotSupportedException("Remote DPI negotiation is not yet verified. Windows scales the local application normally.");
        if (profile.Redirection.RedirectFolders) throw new NotSupportedException("Exact folder sharing and read-only enforcement are unavailable. No containing drive will be shared.");
        if (profile.Redirection.Cameras) throw new NotSupportedException("Camera redirection is unavailable in this adapter.");
        if (profile.GatewayId.HasValue && gateway is null) throw new ArgumentException("The selected RD Gateway was not supplied.", nameof(gateway));
        if (gateway is not null && gateway.Id != profile.GatewayId) throw new ArgumentException("The supplied RD Gateway does not match the connection.", nameof(gateway));
        if (!Enum.IsDefined(profile.Redirection.Sound) || !Enum.IsDefined(profile.Redirection.Clipboard) || !Enum.IsDefined(profile.Input.KeyboardRouting)) throw new ArgumentException("An RDP option is outside its supported range.", nameof(profile));
        int width = profile.Display.AutomaticResolution ? Math.Clamp(viewportWidth, 200, 8192) : profile.Display.Width;
        int height = profile.Display.AutomaticResolution ? Math.Clamp(viewportHeight, 200, 8192) : profile.Display.Height;
        if (width is < 200 or > 8192 || height is < 200 or > 8192) throw new ArgumentOutOfRangeException(nameof(profile), "RDP desktop dimensions must be 200–8192 pixels.");
        if (profile.Display.ColorDepth is not (16 or 24 or 32)) throw new ArgumentOutOfRangeException(nameof(profile), "Supported color-depth requests are 16, 24, and 32 bit.");
        uint authenticationLevel = profile.CertificatePolicy switch
        {
            RdpCertificatePolicy.Strict => 1u,
            RdpCertificatePolicy.Interactive => 2u,
            _ => throw new ArgumentOutOfRangeException(nameof(profile), "Unsupported RDP certificate authentication policy.")
        };
        List<RdpOption> result =
        [
            new(RdpOptionTarget.Client, "Server", profile.Host),
            new(RdpOptionTarget.Client, "DesktopWidth", width),
            new(RdpOptionTarget.Client, "DesktopHeight", height),
            new(RdpOptionTarget.Client, "ColorDepth", profile.Display.ColorDepth),
            new(RdpOptionTarget.Advanced, "RDPPort", profile.Port),
            // Strict by default; optional native prompts never imply automatic approval.
            new(RdpOptionTarget.Advanced, "AuthenticationLevel", authenticationLevel),
            new(RdpOptionTarget.Advanced, "EnableCredSspSupport", true),
            new(RdpOptionTarget.Advanced, "EnableAutoReconnect", profile.AutoReconnect),
            new(RdpOptionTarget.Advanced, "MaxReconnectAttempts", 3),
            new(RdpOptionTarget.Advanced, "ConnectToAdministerServer", profile.AdminSession),
            new(RdpOptionTarget.Advanced, "SmartSizing", profile.Display.FitToWindow),
            new(RdpOptionTarget.Advanced, "RedirectClipboard", profile.Redirection.Clipboard == ClipboardMode.Bidirectional),
            new(RdpOptionTarget.Advanced, "RedirectPrinters", profile.Redirection.Printers),
            new(RdpOptionTarget.Advanced, "RedirectSmartCards", profile.Redirection.SmartCards),
            new(RdpOptionTarget.Advanced, "AudioCaptureRedirectionMode", profile.Redirection.Microphone),
            new(RdpOptionTarget.Advanced, "RedirectDrives", false),
            new(RdpOptionTarget.Advanced, "RedirectPorts", false),
            new(RdpOptionTarget.Advanced, "RedirectDevices", false),
            new(RdpOptionTarget.Advanced, "RedirectPOSDevices", false),
            new(RdpOptionTarget.Advanced, "ContainerHandledFullScreen", 1),
            new(RdpOptionTarget.Advanced, "DisplayConnectionBar", true),
            new(RdpOptionTarget.Advanced, "AudioRedirectionMode", (uint)profile.Redirection.Sound),
            new(RdpOptionTarget.Secured, "KeyboardHookMode", (int)profile.Input.KeyboardRouting)
        ];
        if (gateway is not null)
        {
            if (string.IsNullOrWhiteSpace(gateway.Host) || gateway.Host.Any(char.IsControl)) throw new ArgumentException("RD Gateway server is invalid.", nameof(gateway));
            result.Add(new(RdpOptionTarget.Transport, "GatewayHostname", gateway.Host));
            // DETECT means direct first, then gateway; this is the engine's documented bypass behavior.
            result.Add(new(RdpOptionTarget.Transport, "GatewayUsageMethod", profile.GatewayBypass ? 2u : 1u));
            result.Add(new(RdpOptionTarget.Transport, "GatewayProfileUsageMethod", 1u));
            result.Add(new(RdpOptionTarget.Transport, "GatewayCredsSource", 0u));
        }
        return result;
    }
}
