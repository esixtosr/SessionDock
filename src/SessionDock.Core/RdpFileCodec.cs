using System.Globalization;
using System.Text;

namespace SessionDock.Core;

public sealed record ImportIssue(string Property, string Message, bool IsError = false);

public sealed class RdpImportReview
{
    public ConnectionProfile Draft { get; }
    public IReadOnlyList<ImportIssue> Issues { get; }
    public string Username { get; }
    public string Domain { get; }
    public string RequestedGatewayHost { get; }
    public bool IsValid => Issues.All(x => !x.IsError);
    public bool HasRequestedRedirection { get; }
    private readonly RedirectionOptions requestedRedirection;

    internal RdpImportReview(ConnectionProfile draft, RedirectionOptions redirection, IEnumerable<ImportIssue> issues, string username, string domain, string gateway)
    {
        Draft = draft;
        requestedRedirection = redirection.DeepClone();
        HasRequestedRedirection = redirection.Printers || redirection.SmartCards || redirection.Microphone || redirection.Cameras || redirection.Clipboard != ClipboardMode.Disabled || redirection.Sound != SoundMode.Off;
        Issues = issues.ToArray(); Username = username; Domain = domain; RequestedGatewayHost = gateway;
    }

    /// <summary>Return a new saved-profile candidate; never connects or accesses credential storage.</summary>
    public ConnectionProfile Accept(bool approveRedirection = false, Guid? approvedGatewayId = null, bool approveDirectConnection = false)
    {
        if (!IsValid) throw new FormatException("Review and correct the import errors before saving this connection.");
        if (RequestedGatewayHost.Length > 0 && approvedGatewayId is null && Draft.GatewayId is null && !approveDirectConnection)
            throw new InvalidOperationException("This file requests an RD Gateway. Select an approved gateway profile or explicitly approve direct access before importing.");
        var profile = Draft.DeepClone();
        profile.GatewayId = approvedGatewayId ?? profile.GatewayId;
        profile.Redirection = approveRedirection ? requestedRedirection.DeepClone() : RedirectionOptions.Disabled();
        var errors = ProfileValidator.Validate(profile);
        if (errors.Count > 0) throw new FormatException(string.Join(Environment.NewLine, errors));
        return profile;
    }
}

/// <summary>Strict PC-only .rdp allowlist. Shells, RemoteApps, password blobs, scripts, and drive access are never imported.</summary>
public static class RdpFileCodec
{
    public static RdpImportReview Review(string text)
    {
        var draft = new ConnectionProfile { Redirection = RedirectionOptions.Disabled() };
        var requested = RedirectionOptions.Disabled();
        var issues = new List<ImportIssue>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var username = ""; var domain = ""; var gateway = "";
        int? explicitPort = null;
        if (text.Length > 1_048_576) return new(draft, requested, [new("file", "RDP files larger than 1 MiB are not supported.", true)], "", "", "");
        foreach (var raw in text.TrimStart('\uFEFF').Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            var first = raw.IndexOf(':');
            var second = first >= 0 ? raw.IndexOf(':', first + 1) : -1;
            if (first < 1 || second != first + 2) { issues.Add(new("line", "Malformed setting: expected property:type:value.", true)); continue; }
            var key = raw[..first].Trim().ToLowerInvariant();
            var kind = raw[first + 1];
            var value = raw[(second + 1)..];
            if (!seen.Add(key)) { issues.Add(new(key, "Duplicate properties must be resolved before importing.", true)); continue; }
            try
            {
                switch (key)
                {
                    case "full address":
                        RequireType(kind, 's');
                        if (!EndpointParser.TryParse(value, out var endpoint, out var error)) throw new FormatException(error);
                        draft.Host = endpoint!.Host; draft.Port = endpoint.Port;
                        break;
                    case "server port": explicitPort = Integer(kind, value, 1, 65535); break;
                    case "username": RequireType(kind, 's'); username = SafeMetadata(value); break;
                    case "domain": RequireType(kind, 's'); domain = SafeMetadata(value); break;
                    case "desktopwidth": draft.Display.Width = Integer(kind, value, 200, 8192); draft.Display.AutomaticResolution = false; break;
                    case "desktopheight": draft.Display.Height = Integer(kind, value, 200, 8192); draft.Display.AutomaticResolution = false; break;
                    case "session bpp":
                        var bpp = Integer(kind, value, 16, 32);
                        if (bpp is not (16 or 24 or 32)) throw new FormatException("Supported color depths are 16, 24, and 32.");
                        draft.Display.ColorDepth = bpp; break;
                    case "screen mode id": draft.Display.StartFullScreen = Integer(kind, value, 1, 2) == 2; break;
                    case "use multimon": draft.Display.UseAllMonitors = Boolean(kind, value); break;
                    case "smart sizing": draft.Display.FitToWindow = Boolean(kind, value); break;
                    case "dynamic resolution": draft.Display.DynamicResolution = Boolean(kind, value); break;
                    case "autoreconnection enabled": draft.AutoReconnect = Boolean(kind, value); break;
                    case "administrative session": draft.AdminSession = Boolean(kind, value); break;
                    case "keyboardhook": draft.Input.KeyboardRouting = (KeyboardRouting)Integer(kind, value, 0, 2); break;
                    case "redirectprinters": requested.Printers = Boolean(kind, value); break;
                    case "redirectsmartcards": requested.SmartCards = Boolean(kind, value); break;
                    case "redirectclipboard": requested.Clipboard = Boolean(kind, value) ? ClipboardMode.Bidirectional : ClipboardMode.Disabled; break;
                    case "audiocapturemode": requested.Microphone = Boolean(kind, value); break;
                    case "audiomode": requested.Sound = (SoundMode)Integer(kind, value, 0, 2); break;
                    case "gatewayhostname":
                        RequireType(kind, 's');
                        if (value.Length > 0 && !EndpointParser.TryParse(value, out _, out _)) throw new FormatException("The RD Gateway address is invalid.");
                        gateway = value.Trim();
                        if (gateway.Length > 0) issues.Add(new(key, "Select an approved gateway profile before saving; imported gateway credentials are never used."));
                        break;
                    case "gatewayusagemethod":
                    case "gatewaycredentialssource":
                    case "gatewayprofileusagemethod":
                        issues.Add(new(key, "Gateway options require review in a saved gateway profile and were not applied.")); break;
                    case "drivestoredirect":
                    case "redirectdrives":
                    case "camerastoredirect":
                    case "devicestoredirect":
                    case "usbdevicestoredirect":
                        issues.Add(new(key, "This device-selection property is unavailable and was not applied. Folder selection is never expanded into whole-drive access.")); break;
                    case "alternate shell":
                    case "shell working directory":
                        issues.Add(new(key, "Program execution is blocked in the saved-PC release.")); break;
                    default:
                        if (key.Contains("password", StringComparison.OrdinalIgnoreCase) || key.Contains("secret", StringComparison.OrdinalIgnoreCase) || key.Contains("token", StringComparison.OrdinalIgnoreCase))
                            issues.Add(new("secret", "A secret-bearing property was ignored; no secret was retained."));
                        else if (key.StartsWith("remoteapplication", StringComparison.OrdinalIgnoreCase))
                            issues.Add(new(key, "Published application settings are deferred and were not applied."));
                        else issues.Add(new(key, "Unknown or unsupported property was not applied."));
                        break;
                }
            }
            catch (FormatException ex) { issues.Add(new(key, ex.Message, true)); }
        }
        if (explicitPort is { } port)
        {
            if (draft.Port != 3389 && draft.Port != port) issues.Add(new("server port", "Server port conflicts with the full address.", true));
            else draft.Port = port;
        }
        if (!seen.Contains("full address")) issues.Add(new("full address", "The file must include a full address.", true));
        foreach (var error in ProfileValidator.Validate(draft)) issues.Add(new("connection", error, true));
        return new(draft, requested, issues, username, domain, gateway);
    }

    public static string Export(ConnectionProfile profile, GatewayProfile? gateway = null)
    {
        var errors = ProfileValidator.Validate(profile);
        if (errors.Count > 0) throw new ArgumentException(string.Join(Environment.NewLine, errors), nameof(profile));
        if (profile.GatewayId is { } id && (gateway is null || gateway.Id != id)) throw new InvalidOperationException("The selected RD Gateway must be supplied to export this connection safely.");
        if (profile.Redirection.RedirectFolders || profile.Redirection.Cameras || profile.SwapMouseButtons || profile.Display.HighDpi)
            throw new InvalidOperationException("Folder redirection, cameras, session-local mouse swapping, or custom DPI cannot be represented by this export. Use the application-profile export to preserve these requested settings.");
        var output = new StringBuilder();
        void S(string key, string value)
        {
            if (value.Any(char.IsControl)) throw new ArgumentException("Export values cannot contain control characters.");
            output.Append(key).Append(":s:").Append(value).Append("\r\n");
        }
        void I(string key, int value) => output.Append(key).Append(":i:").Append(value.ToString(CultureInfo.InvariantCulture)).Append("\r\n");
        void B(string key, bool value) => I(key, value ? 1 : 0);
        S("full address", profile.Endpoint);
        if (!profile.Display.AutomaticResolution) { I("desktopwidth", profile.Display.Width); I("desktopheight", profile.Display.Height); }
        I("screen mode id", profile.Display.StartFullScreen ? 2 : 1);
        I("session bpp", profile.Display.ColorDepth);
        B("use multimon", profile.Display.UseAllMonitors);
        B("smart sizing", profile.Display.FitToWindow);
        B("dynamic resolution", profile.Display.DynamicResolution);
        B("autoreconnection enabled", profile.AutoReconnect);
        B("administrative session", profile.AdminSession);
        I("keyboardhook", (int)profile.Input.KeyboardRouting);
        B("redirectprinters", profile.Redirection.Printers);
        B("redirectsmartcards", profile.Redirection.SmartCards);
        B("redirectclipboard", profile.Redirection.Clipboard == ClipboardMode.Bidirectional);
        B("audiocapturemode", profile.Redirection.Microphone);
        I("audiomode", (int)profile.Redirection.Sound);
        B("redirectcomports", false);
        B("redirectlocation", false);
        B("redirectwebauthn", false);
        S("devicestoredirect", "");
        S("usbdevicestoredirect", "");
        S("drivestoredirect", "");
        S("camerastoredirect", "");
        if (profile.GatewayId is not null && gateway is not null)
        {
            if (!EndpointParser.TryParse(gateway.Host, out _, out _)) throw new ArgumentException("The gateway address is invalid.");
            S("gatewayhostname", gateway.Host);
            I("gatewayusagemethod", profile.GatewayBypass ? 2 : 1);
            I("gatewayprofileusagemethod", 1);
            I("promptcredentialonce", 0);
            I("gatewaycredentialssource", 4); // User selects the source at logon; never export a credential blob.
        }
        return output.ToString();
    }
    private static void RequireType(char actual, char expected) { if (actual != expected) throw new FormatException($"Expected RDP value type '{expected}'."); }
    private static bool Boolean(char type, string value) => Integer(type, value, 0, 1) == 1;
    private static int Integer(char type, string value, int min, int max)
    {
        RequireType(type, 'i');
        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number < min || number > max) throw new FormatException($"Value must be an integer from {min} to {max}.");
        return number;
    }
    private static string SafeMetadata(string value)
    {
        if (value.Length > 256 || value.Any(char.IsControl)) throw new FormatException("Account metadata must contain at most 256 printable characters.");
        return value;
    }
}
