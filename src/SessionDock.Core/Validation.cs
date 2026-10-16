using System.Globalization;
using System.Net;
using System.Net.Sockets;

namespace SessionDock.Core;

public sealed record Endpoint(string Host, int Port);

public static class EndpointParser
{
    public static bool TryParse(string? value, out Endpoint? endpoint, out string? error)
    {
        endpoint = null;
        error = null;
        var input = value?.Trim() ?? "";
        if (input.Length == 0) { error = "Enter a PC name or IP address."; return false; }
        if (input.Any(char.IsWhiteSpace) || input.Any(char.IsControl) || input.IndexOfAny(['/', '\\', '@', '?', '#', '%']) >= 0)
        { error = "Use a host name or IP address, without a URL, path, credentials, or spaces."; return false; }
        string host;
        string? portText = null;
        if (input.StartsWith('['))
        {
            var closing = input.IndexOf(']');
            if (closing < 0) { error = "Close the brackets around the IPv6 address."; return false; }
            host = input[1..closing];
            var suffix = input[(closing + 1)..];
            if (suffix.Length > 0)
            {
                if (!suffix.StartsWith(':')) { error = "Use [IPv6 address]:port."; return false; }
                portText = suffix[1..];
            }
            if (!IPAddress.TryParse(host, out var ipv6) || ipv6.AddressFamily != AddressFamily.InterNetworkV6)
            { error = "Enter a valid IPv6 address inside the brackets."; return false; }
            host = ipv6.ToString();
        }
        else
        {
            var colon = input.IndexOf(':');
            if (colon >= 0)
            {
                if (input.IndexOf(':', colon + 1) >= 0) { error = "Put IPv6 addresses in brackets, for example [::1]."; return false; }
                host = input[..colon];
                portText = input[(colon + 1)..];
            }
            else host = input;
            if (!TryNormalizeHost(host, out host)) { error = "Enter a valid DNS host name or IPv4 address."; return false; }
        }
        var port = 3389;
        if (portText is not null && (!int.TryParse(portText, NumberStyles.None, CultureInfo.InvariantCulture, out port) || port is < 1 or > 65535))
        { error = "Port must be a number from 1 to 65535."; return false; }
        endpoint = new Endpoint(host, port);
        return true;
    }

    public static string Format(string host, int port = 3389)
    {
        var displayHost = host.Contains(':') ? $"[{host.Trim('[', ']')}]" : host;
        return port == 3389 ? displayHost : $"{displayHost}:{port}";
    }

    private static bool TryNormalizeHost(string input, out string normalized)
    {
        normalized = "";
        if (input.Length == 0 || input.Contains('[') || input.Contains(']')) return false;
        if (input.All(c => char.IsAsciiDigit(c) || c == '.'))
        {
            var octets = input.Split('.');
            if (octets.Length != 4 || octets.Any(x => x.Length == 0 || x.Length > 3 || (x.Length > 1 && x[0] == '0') || !byte.TryParse(x, NumberStyles.None, CultureInfo.InvariantCulture, out _))) return false;
            normalized = input;
            return true;
        }
        if (input.EndsWith("..", StringComparison.Ordinal)) return false;
        try { normalized = new IdnMapping().GetAscii(input.TrimEnd('.')).ToLowerInvariant(); }
        catch (ArgumentException) { return false; }
        if (normalized.Length is < 1 or > 253) return false;
        return normalized.Split('.').All(label => label.Length is >= 1 and <= 63 && label[0] != '-' && label[^1] != '-' && label.All(c => char.IsAsciiLetterOrDigit(c) || c == '-'));
    }
}

public static class ProfileValidator
{
    public static IReadOnlyList<string> Validate(ConnectionProfile profile)
    {
        var errors = new List<string>();
        if (profile.Id == Guid.Empty) errors.Add("The connection ID is missing.");
        if (string.IsNullOrWhiteSpace(profile.Host)) errors.Add("Enter a PC name or IP address.");
        else if (!EndpointParser.TryParse(EndpointParser.Format(profile.Host, profile.Port), out _, out var endpointError)) errors.Add(endpointError!);
        if (profile.Port is < 1 or > 65535) errors.Add("Port must be from 1 to 65535.");
        if (profile.FriendlyName is null || profile.FriendlyName.Length > 200 || profile.FriendlyName.Any(char.IsControl)) errors.Add("Friendly name must contain at most 200 printable characters.");
        if (profile.GroupId == Guid.Empty) errors.Add("Select a group.");
        if (profile.Display is null || profile.Redirection is null || profile.Input is null) { errors.Add("Connection settings are missing."); return errors; }
        errors.AddRange(ValidateResolution(profile.Display.Width, profile.Display.Height));
        if (profile.Display.ColorDepth is not (16 or 24 or 32)) errors.Add("Color depth must be 16, 24, or 32 bits; engine support is checked when connecting.");
        if (!Enum.IsDefined(profile.Input.KeyboardRouting)) errors.Add("Keyboard routing is invalid.");
        if (!Enum.IsDefined(profile.Redirection.Clipboard) || !Enum.IsDefined(profile.Redirection.Sound)) errors.Add("Clipboard or sound mode is invalid.");
        errors.AddRange(ValidateFolders(profile.Redirection));
        return errors;
    }

    public static IReadOnlyList<string> ValidateResolution(int width, int height) => width is < 200 or > 8192 || height is < 200 or > 8192
        ? ["Resolution dimensions must be from 200 to 8192 pixels; the session engine may impose additional limits."] : [];

    public static IReadOnlyList<string> ValidateFolders(RedirectionOptions redirection, bool inspectFilesystem = false)
    {
        var errors = new List<string>();
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (redirection.Folders is null) return ["Folder settings are missing."];
        foreach (var folder in redirection.Folders)
        {
            if (string.IsNullOrWhiteSpace(folder.Name) || string.IsNullOrWhiteSpace(folder.Path)) { errors.Add("Each redirected folder needs a name and path."); continue; }
            var comparable = folder.Path.Replace('/', '\\').TrimEnd('\\');
            if (!paths.Add(comparable)) errors.Add($"The folder '{folder.Name}' is listed more than once.");
            if (inspectFilesystem)
            {
                if (!Directory.Exists(folder.Path)) errors.Add($"The folder '{folder.Name}' does not exist or is inaccessible.");
                else
                {
                    try { using var files = Directory.EnumerateFileSystemEntries(folder.Path).GetEnumerator(); files.MoveNext(); }
                    catch (Exception e) when (e is IOException or UnauthorizedAccessException) { errors.Add($"The folder '{folder.Name}' cannot be read."); }
                }
            }
        }
        return errors;
    }
}

public static class LibraryValidator
{
    public static IReadOnlyList<string> Validate(LibrarySnapshot library)
    {
        var errors = new List<string>();
        if (library.Connections is null || library.Groups is null || library.Credentials is null || library.Gateways is null || library.Resolutions is null || library.Settings is null)
            return ["Library collections or settings are missing."];
        if (library.Groups.Count(x => x.Id == Group.DefaultId) != 1) errors.Add("The Saved Devices fallback group is required exactly once.");
        if (library.Groups.Any(x => x.Id == Group.DefaultId && (x.Name != "Saved Devices" || x.Order != 0)))
            errors.Add("The fallback group must remain Saved Devices in the first group position.");
        CheckIds(library.Connections.Select(x => x.Id), "connections", errors);
        CheckIds(library.Groups.Select(x => x.Id), "groups", errors);
        CheckIds(library.Credentials.Select(x => x.Id), "credentials", errors);
        CheckIds(library.Gateways.Select(x => x.Id), "gateways", errors);
        CheckIds(library.Resolutions.Select(x => x.Id), "resolutions", errors);
        var groups = library.Groups.Select(x => x.Id).ToHashSet();
        var credentials = library.Credentials.Select(x => x.Id).ToHashSet();
        var gateways = library.Gateways.Select(x => x.Id).ToHashSet();
        foreach (var connection in library.Connections)
        {
            errors.AddRange(ProfileValidator.Validate(connection));
            if (!groups.Contains(connection.GroupId)) errors.Add("A connection refers to a missing group.");
            if (connection.CredentialId is { } credential && !credentials.Contains(credential)) errors.Add("A connection refers to a missing credential profile.");
            if (connection.GatewayId is { } gateway && !gateways.Contains(gateway)) errors.Add("A connection refers to a missing gateway.");
        }
        foreach (var credential in library.Credentials)
            if (string.IsNullOrWhiteSpace(credential.Label) || string.IsNullOrWhiteSpace(credential.Username)) errors.Add("Credential profiles need a label and username.");
        foreach (var gateway in library.Gateways)
        {
            if (string.IsNullOrWhiteSpace(gateway.Label) || !EndpointParser.TryParse(gateway.Host, out _, out _) || gateway.Host.Contains(':')) errors.Add("Gateway profiles need a label and DNS hostname or IPv4 server address without a port; custom gateway ports and IPv6 gateways are not available in this build.");
            if (gateway.CredentialId is { } credential && !credentials.Contains(credential)) errors.Add("A gateway refers to a missing credential profile.");
        }
        if (library.Groups.Any(x => string.IsNullOrWhiteSpace(x.Name) || x.Name.Trim().Length > 100 || x.Name.Any(char.IsControl)))
            errors.Add("Every group needs a name of 1 to 100 printable characters.");
        if (library.Groups.Select(x => x.Name?.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != library.Groups.Count) errors.Add("Group names must be unique.");
        if (library.Resolutions.Select(x => (x.Width, x.Height)).Distinct().Count() != library.Resolutions.Count) errors.Add("Resolution presets must be unique.");
        foreach (var resolution in library.Resolutions) errors.AddRange(ProfileValidator.ValidateResolution(resolution.Width, resolution.Height));
        var settings = library.Settings;
        if (!Enum.IsDefined(settings.View) || !Enum.IsDefined(settings.Sort) || !Enum.IsDefined(settings.CardSize) || !Enum.IsDefined(settings.Theme) || !Enum.IsDefined(settings.ConnectionBar)) errors.Add("An application setting has an invalid value.");
        if (settings.DefaultDisplay is null || settings.DefaultRedirection is null || settings.DefaultInput is null) errors.Add("New-connection defaults are missing.");
        else errors.AddRange(ProfileValidator.Validate(new ConnectionProfile { Host = "validation.invalid", Display = settings.DefaultDisplay, Redirection = settings.DefaultRedirection, Input = settings.DefaultInput }));
        return errors;
    }
    private static void CheckIds(IEnumerable<Guid> ids, string label, List<string> errors)
    {
        var all = ids.ToList();
        if (all.Contains(Guid.Empty) || all.Distinct().Count() != all.Count) errors.Add($"The {label} contain an empty or duplicate ID.");
    }
}

public static class LibraryOperations
{
    public static ConnectionProfile NewConnection(AppSettings settings) => new()
    { Display = settings.DefaultDisplay with { }, Redirection = settings.DefaultRedirection.DeepClone(), Input = settings.DefaultInput with { } };

    public static void DeleteGroup(LibrarySnapshot library, Guid groupId)
    {
        if (groupId == Group.DefaultId) throw new InvalidOperationException("Saved Devices is the fallback group and cannot be deleted.");
        foreach (var profile in library.Connections.Where(x => x.GroupId == groupId)) profile.GroupId = Group.DefaultId;
        library.Groups.RemoveAll(x => x.Id == groupId);
    }
    public static void DeleteCredential(LibrarySnapshot library, Guid credentialId)
    {
        foreach (var profile in library.Connections.Where(x => x.CredentialId == credentialId)) profile.CredentialId = null;
        foreach (var gateway in library.Gateways.Where(x => x.CredentialId == credentialId)) gateway.CredentialId = null;
        library.Credentials.RemoveAll(x => x.Id == credentialId);
    }
    public static void DeleteGateway(LibrarySnapshot library, Guid gatewayId)
    {
        foreach (var profile in library.Connections.Where(x => x.GatewayId == gatewayId)) { profile.GatewayId = null; profile.GatewayBypass = false; }
        library.Gateways.RemoveAll(x => x.Id == gatewayId);
    }
}
