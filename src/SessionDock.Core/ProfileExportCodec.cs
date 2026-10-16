using System.Text.Json;
using System.Text.Json.Serialization;

namespace SessionDock.Core;

/// <summary>Versioned, metadata-only application library exchange. Credentials remain in the operating-system vault.</summary>
public static class ProfileExportCodec
{
    private sealed record ExportEnvelope(int SchemaVersion, LibrarySnapshot Library);
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        MaxDepth = 32,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };
    public static string Export(LibrarySnapshot library)
    {
        var errors = LibraryValidator.Validate(library);
        if (errors.Count > 0) throw new ArgumentException(string.Join(Environment.NewLine, errors), nameof(library));
        return JsonSerializer.Serialize(new ExportEnvelope(1, library), Options);
    }
    public static LibrarySnapshot Import(string json, bool approveRedirection = false)
    {
        if (json.Length > 8_388_608) throw new FormatException("Profile export is larger than the supported 8 MiB limit.");
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 32 });
            RejectDuplicatesAndNulls(document.RootElement);
            var envelope = JsonSerializer.Deserialize<ExportEnvelope>(json, Options) ?? throw new FormatException("The profile export is empty.");
            if (envelope.SchemaVersion != 1) throw new FormatException("This profile-export version is not supported.");
            if (envelope.Library is null) throw new FormatException("The profile export is missing its library.");
            var errors = LibraryValidator.Validate(envelope.Library);
            if (errors.Count > 0) throw new FormatException(string.Join(Environment.NewLine, errors));
            var library = envelope.Library.DeepClone();
            // Untrusted metadata must never select an existing local vault entry by supplying its GUID.
            // Preserve references within the imported library while requiring freshly entered secrets.
            var credentialIds = library.Credentials.ToDictionary(x => x.Id, _ => Guid.NewGuid());
            foreach (var credential in library.Credentials) credential.Id = credentialIds[credential.Id];
            foreach (var profile in library.Connections)
                if (profile.CredentialId is { } id) profile.CredentialId = credentialIds[id];
            foreach (var gateway in library.Gateways)
                if (gateway.CredentialId is { } id) gateway.CredentialId = credentialIds[id];
            library.Settings.ScreenshotThumbnails = false;
            library.Settings.TrayEnabled = false;
            library.Settings.QuickLauncherHotkey = "";
            if (!approveRedirection)
            {
                foreach (var profile in library.Connections) profile.Redirection = RedirectionOptions.Disabled();
                library.Settings.DefaultRedirection = RedirectionOptions.Disabled();
                library.Settings.ImportedRdpRedirection = false;
            }
            return library;
        }
        catch (JsonException) { throw new FormatException("The profile export contains invalid JSON, unexpected fields, or unsupported setting values."); }
    }
    private static void RejectDuplicatesAndNulls(JsonElement element, string? property = null)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var child in element.EnumerateObject())
            {
                if (!names.Add(child.Name)) throw new FormatException("The profile export contains duplicate properties.");
                RejectDuplicatesAndNulls(child.Value, child.Name);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var child in element.EnumerateArray()) RejectDuplicatesAndNulls(child);
        else if (element.ValueKind == JsonValueKind.Null && property is not ("credentialId" or "gatewayId" or "lastConnectedAt"))
            throw new FormatException("Required profile data cannot be null.");
    }
}
