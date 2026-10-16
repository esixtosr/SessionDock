using System.Text.Json.Nodes;
using SessionDock.Core;

namespace SessionDock.Tests;

public sealed class RdpImportTests
{
    [Fact] public void SharingIsDisabledUntilExplicitApproval()
    {
        var review = RdpFileCodec.Review("full address:s:pc.example.invalid\nredirectclipboard:i:1\nredirectprinters:i:1\naudiocapturemode:i:1\naudiomode:i:0");
        Assert.True(review.IsValid); Assert.True(review.HasRequestedRedirection);
        var safe = review.Accept(); Assert.Equal(ClipboardMode.Disabled, safe.Redirection.Clipboard); Assert.False(safe.Redirection.Printers); Assert.False(safe.Redirection.Microphone);
        var approved = review.Accept(true); Assert.True(approved.Redirection.Printers); Assert.True(approved.Redirection.Microphone); Assert.Equal(SoundMode.Local, approved.Redirection.Sound);
    }
    [Fact] public void CommandAndSecretPropertiesAreReportedAndNeverApplied()
    {
        var review = RdpFileCodec.Review("full address:s:pc.example.invalid\nalternate shell:s:do-not-run\nremoteapplicationcmdline:s:do-not-run\npassword 51:b:deadbeef\ndrivestoredirect:s:*\nunknown:s:value");
        Assert.True(review.IsValid); Assert.Equal(5, review.Issues.Count);
        Assert.DoesNotContain(review.Issues, i => i.Message.Contains("deadbeef") || i.Message.Contains("do-not-run"));
        var exported = RdpFileCodec.Export(review.Accept(true));
        Assert.DoesNotContain("deadbeef", exported); Assert.DoesNotContain("do-not-run", exported); Assert.Contains("drivestoredirect:s:\r\n", exported);
    }
    [Theory]
    [InlineData("redirectclipboard:i:2")][InlineData("redirectclipboard:s:1")]
    [InlineData("desktopwidth:i:8193")][InlineData("keyboardhook:i:3")][InlineData("audiomode:i:-1")]
    [InlineData("session bpp:i:17")][InlineData("server port:i:0")]
    public void RejectsInvalidTypesAndRanges(string setting)
    {
        var review = RdpFileCodec.Review("full address:s:pc.example.invalid\n" + setting);
        Assert.False(review.IsValid); Assert.Throws<FormatException>(() => review.Accept());
    }
    [Fact] public void RejectsDuplicateProperties()
    { Assert.False(RdpFileCodec.Review("full address:s:pc.example.invalid\nFULL ADDRESS:s:other.example.invalid").IsValid); }
    [Fact] public void ImportedGatewayCannotSilentlyBecomeDirectAccess()
    {
        var review = RdpFileCodec.Review("full address:s:pc.example.invalid\ngatewayhostname:s:gateway.example.invalid");
        Assert.Throws<InvalidOperationException>(() => review.Accept());
        var id = Guid.NewGuid(); Assert.Equal(id, review.Accept(approvedGatewayId: id).GatewayId);
    }
    [Fact] public void ExportRequiresExactReferencedGateway()
    {
        var gateway = new GatewayProfile { Label = "Test", Host = "gateway.example.invalid" };
        var profile = new ConnectionProfile { Host = "pc.example.invalid", GatewayId = gateway.Id };
        Assert.Throws<InvalidOperationException>(() => RdpFileCodec.Export(profile));
        Assert.Contains("gatewayhostname:s:gateway.example.invalid", RdpFileCodec.Export(profile, gateway));
    }
    [Fact] public void PreservesDomainUserAndUpnWithoutSplitting()
    {
        Assert.Equal(@"DOMAIN\user", RdpFileCodec.Review("full address:s:pc.example.invalid\nusername:s:DOMAIN\\user").Username);
        Assert.Equal("user@example.invalid", RdpFileCodec.Review("full address:s:pc.example.invalid\nusername:s:user@example.invalid").Username);
    }
    [Fact] public void RoundTripsSupportedDisplayAndInputSettings()
    {
        var profile = new ConnectionProfile { Host = "2001:db8::20", Port = 3399, AdminSession = true, Display = new() { AutomaticResolution = false, Width = 2560, Height = 1440, StartFullScreen = true, DynamicResolution = false }, Input = new() { KeyboardRouting = KeyboardRouting.RemoteWhenFocused } };
        var loaded = RdpFileCodec.Review(RdpFileCodec.Export(profile)).Accept(true);
        Assert.Equal(profile.Endpoint, loaded.Endpoint); Assert.Equal(profile.Display, loaded.Display); Assert.Equal(profile.Input, loaded.Input); Assert.True(loaded.AdminSession);
    }
}

public sealed class ProfileExportTests
{
    private static LibrarySnapshot Example()
    {
        var library = new LibrarySnapshot();
        var credential = new CredentialProfile { Label = "Account", Username = "user@example.invalid" };
        library.Credentials.Add(credential);
        library.Connections.Add(new() { Host = "pc.example.invalid", CredentialId = credential.Id, Redirection = new() { Microphone = true, Clipboard = ClipboardMode.Bidirectional } });
        return library;
    }
    [Fact] public void VersionedExportContainsMetadataButNoSecretFields()
    {
        var library = Example(); var json = ProfileExportCodec.Export(library);
        Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secureString", json, StringComparison.OrdinalIgnoreCase);
        var imported = ProfileExportCodec.Import(json);
        Assert.Equal(library.Connections[0].Id, imported.Connections[0].Id); Assert.NotEqual(library.Credentials[0].Id, imported.Credentials[0].Id);
        Assert.Equal(imported.Credentials[0].Id, imported.Connections[0].CredentialId);
        Assert.False(imported.Connections[0].Redirection.Microphone); Assert.Equal(ClipboardMode.Disabled, imported.Connections[0].Redirection.Clipboard);
        Assert.True(ProfileExportCodec.Import(json, true).Connections[0].Redirection.Microphone);
    }
    [Fact] public void ImportsCannotEnableThumbnailStorageTrayOrGlobalHotkeys()
    {
        var library = Example(); library.Settings.ScreenshotThumbnails = true; library.Settings.TrayEnabled = true; library.Settings.QuickLauncherHotkey = "Ctrl+Alt+R";
        var imported = ProfileExportCodec.Import(ProfileExportCodec.Export(library), true);
        Assert.False(imported.Settings.ScreenshotThumbnails); Assert.False(imported.Settings.TrayEnabled); Assert.Empty(imported.Settings.QuickLauncherHotkey);
    }
    [Fact] public void RejectsFutureVersionUnexpectedSecretFieldsAndMissingReferences()
    {
        var root = JsonNode.Parse(ProfileExportCodec.Export(Example()))!;
        root["schemaVersion"] = 2; Assert.Throws<FormatException>(() => ProfileExportCodec.Import(root.ToJsonString()));
        root["schemaVersion"] = 1; root["password"] = "fixture-marker"; Assert.Throws<FormatException>(() => ProfileExportCodec.Import(root.ToJsonString()));
        root.AsObject().Remove("password"); root["library"]!["credentials"] = new JsonArray(); Assert.Throws<FormatException>(() => ProfileExportCodec.Import(root.ToJsonString()));
    }
    [Theory]
    [InlineData("{}")]
    [InlineData("{\"schemaVersion\":1}")]
    [InlineData("{\"schemaVersion\":1,\"library\":null}")]
    [InlineData("{\"schemaVersion\":1,\"library\":\"not-a-library\"}")]
    [InlineData("[]")]
    public void MalformedExportsProduceActionableFormatErrors(string json)
    { Assert.Throws<FormatException>(() => ProfileExportCodec.Import(json)); }
    [Fact] public void UntrustedChangedHostCannotReuseExistingVaultIds()
    {
        var original = Example();
        original.Gateways.Add(new() { Label = "Gateway", Host = "gateway.example.invalid", CredentialId = original.Credentials[0].Id });
        var root = JsonNode.Parse(ProfileExportCodec.Export(original))!;
        root["library"]!["connections"]![0]!["host"] = "changed.example.invalid";
        var imported = ProfileExportCodec.Import(root.ToJsonString());
        Assert.Equal("changed.example.invalid", imported.Connections[0].Host);
        Assert.NotEqual(original.Credentials[0].Id, imported.Connections[0].CredentialId);
        Assert.Equal(imported.Credentials[0].Id, imported.Connections[0].CredentialId);
        Assert.Equal(imported.Credentials[0].Id, imported.Gateways[0].CredentialId);
    }
    [Theory]
    [InlineData("Other group", 0)]
    [InlineData("Saved Devices", 5)]
    public void ImportedFallbackGroupCannotBreakProtectedGroupOperations(string name, int order)
    {
        var root = JsonNode.Parse(ProfileExportCodec.Export(Example()))!;
        root["library"]!["groups"]![0]!["name"] = name;
        root["library"]!["groups"]![0]!["order"] = order;
        Assert.Throws<FormatException>(() => ProfileExportCodec.Import(root.ToJsonString()));
    }
    [Fact] public void RejectsNullNestedObjectsAndDuplicateKeys()
    {
        var json = ProfileExportCodec.Export(Example());
        Assert.Throws<FormatException>(() => ProfileExportCodec.Import(json.Replace("\"display\": {", "\"display\": null, \"ignored\": {")));
        Assert.Throws<FormatException>(() => ProfileExportCodec.Import(json.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 1, \"schemaVersion\": 1")));
    }
}
