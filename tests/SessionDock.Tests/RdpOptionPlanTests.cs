using SessionDock.Core;
using SessionDock.Rdp.Windows;
using Xunit;

namespace SessionDock.Tests;

public sealed class RdpOptionPlanTests
{
    private static ConnectionProfile Profile() => new() { Host = "authorized-test.invalid" };
    private static object Value(IReadOnlyList<RdpOption> options, string member) => Assert.Single(options, x => x.Member == member).Value;

    [Theory]
    [InlineData(RdpCertificatePolicy.Strict, 1u)]
    [InlineData(RdpCertificatePolicy.Interactive, 2u)]
    public void CertificateModesPreserveCredSspAndEveryOtherOption(RdpCertificatePolicy policy, uint expected)
    {
        var profile = Profile();
        var baseline = RdpOptionPlan.Create(profile, null, 1200, 720);
        profile.CertificatePolicy = policy;
        var options = RdpOptionPlan.Create(profile, null, 1200, 720);
        Assert.Equal(expected, Value(options, "AuthenticationLevel"));
        Assert.NotEqual(0u, Value(options, "AuthenticationLevel"));
        Assert.Equal(true, Value(options, "EnableCredSspSupport"));
        Assert.Equal(baseline.Where(x => x.Member != "AuthenticationLevel"), options.Where(x => x.Member != "AuthenticationLevel"));
        Assert.Equal(policy, profile.DeepClone().CertificatePolicy);
    }
    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    [InlineData(999)]
    public void InvalidCertificatePolicyFailsValidationAndMapping(int value)
    {
        var profile = Profile(); profile.CertificatePolicy = (RdpCertificatePolicy)value;
        Assert.NotEmpty(ProfileValidator.Validate(profile));
        Assert.Throws<ArgumentOutOfRangeException>(() => RdpOptionPlan.Create(profile, null, 1200, 720));
    }
    [Theory]
    [InlineData(RdpCertificatePolicy.Strict)]
    [InlineData(RdpCertificatePolicy.Interactive)]
    public void ProfileExchangePreservesCertificatePolicy(RdpCertificatePolicy policy)
    {
        var profile = Profile(); profile.CertificatePolicy = policy;
        var json = ProfileExportCodec.Export(new() { Connections = [profile] });
        Assert.Equal(policy, Assert.Single(ProfileExportCodec.Import(json).Connections).CertificatePolicy);
        var rdp = RdpFileCodec.Export(profile);
        Assert.Contains("enablecredsspsupport:i:1", rdp);
        Assert.Equal(policy, RdpFileCodec.Review(rdp).Accept().CertificatePolicy);
    }
    [Fact]
    public void LegacyProfileExchangeDefaultsToStrict()
    {
        var json = ProfileExportCodec.Export(new() { Connections = [Profile()] });
        json = json.Replace("\"certificatePolicy\": \"Strict\",", "");
        Assert.Equal(RdpCertificatePolicy.Strict, Assert.Single(ProfileExportCodec.Import(json).Connections).CertificatePolicy);
        Assert.Equal(RdpCertificatePolicy.Strict, RdpFileCodec.Review("full address:s:test.invalid").Accept().CertificatePolicy);
        Assert.False(RdpFileCodec.Review("full address:s:test.invalid\nauthentication level:i:0").IsValid);
    }
    [Fact]
    public void DefaultMappingRequiresServerAuthenticationAndNla()
    {
        var options = RdpOptionPlan.Create(Profile(), null, 1200, 720);
        Assert.Equal(1u, Value(options, "AuthenticationLevel"));
        Assert.Equal(true, Value(options, "EnableCredSspSupport"));
        Assert.Equal(false, Value(options, "RedirectDrives"));
        Assert.Equal(false, Value(options, "RedirectPorts"));
        Assert.Equal(false, Value(options, "RedirectDevices"));
        Assert.Equal(false, Value(options, "RedirectPOSDevices"));
        Assert.DoesNotContain(options, x => x.Member.Contains("Password"));
    }
    [Fact]
    public void FixedResolutionIsIndependentOfScalingAndDynamicResolution()
    {
        var p = Profile();
        p.Display = new() { AutomaticResolution = false, Width = 1600, Height = 900, FitToWindow = true, DynamicResolution = false };
        var options = RdpOptionPlan.Create(p, null, 500, 400);
        Assert.Equal(1600, Value(options, "DesktopWidth"));
        Assert.Equal(900, Value(options, "DesktopHeight"));
        Assert.Equal(true, Value(options, "SmartSizing"));
    }
    [Fact]
    public void GatewayBypassUsesDocumentedDirectThenGatewayMode()
    {
        var gateway = new GatewayProfile { Host = "rdgateway.invalid" };
        var p = Profile(); p.GatewayId = gateway.Id; p.GatewayBypass = true;
        var options = RdpOptionPlan.Create(p, gateway, 1000, 700);
        Assert.Equal(2u, Value(options, "GatewayUsageMethod"));
        p.GatewayBypass = false;
        Assert.Equal(1u, Value(RdpOptionPlan.Create(p, gateway, 1000, 700), "GatewayUsageMethod"));
    }
    [Theory]
    [InlineData(SoundMode.Local, 0)]
    [InlineData(SoundMode.Remote, 1)]
    [InlineData(SoundMode.Off, 2)]
    public void SoundMapsToAdvancedSettings(SoundMode sound, int expected)
    {
        var p = Profile(); p.Redirection.Sound = sound;
        var option = Assert.Single(RdpOptionPlan.Create(p, null, 1000, 700), x => x.Member == "AudioRedirectionMode");
        Assert.Equal(RdpOptionTarget.Advanced, option.Target); Assert.Equal((uint)expected, option.Value);
    }
    [Theory]
    [InlineData(KeyboardRouting.Local, 0)]
    [InlineData(KeyboardRouting.RemoteWhenFocused, 1)]
    [InlineData(KeyboardRouting.RemoteInFullScreen, 2)]
    public void KeyboardMapsToNativeHookMode(KeyboardRouting routing, int expected)
    {
        var p = Profile(); p.Input.KeyboardRouting = routing;
        Assert.Equal(expected, Value(RdpOptionPlan.Create(p, null, 1000, 700), "KeyboardHookMode"));
    }
    [Fact]
    public void UnsupportedFolderScopeNeverBecomesWholeDriveSharing()
    {
        var p = Profile(); p.Redirection.RedirectFolders = true;
        p.Redirection.Folders.Add(new() { Path = @"C:\Scoped", ReadOnly = true });
        Assert.Throws<NotSupportedException>(() => RdpOptionPlan.Create(p, null, 1000, 700));
    }
    [Fact]
    public void UnsupportedSessionLocalMouseSwapFailsBeforeConnection()
    {
        var p = Profile(); p.SwapMouseButtons = true;
        Assert.Throws<NotSupportedException>(() => RdpOptionPlan.Create(p, null, 1000, 700));
    }
    [Fact]
    public void MismatchedGatewayCannotAccidentallyRouteConnection()
    {
        var p = Profile(); p.GatewayId = Guid.NewGuid();
        Assert.Throws<ArgumentException>(() => RdpOptionPlan.Create(p, new() { Host = "other.invalid" }, 1000, 700));
    }
    [Fact]
    public void ExplicitClipboardAndCaptureOffReachTheControl()
    {
        var p = Profile(); p.Redirection.Clipboard = ClipboardMode.Disabled; p.Redirection.Microphone = false;
        var options = RdpOptionPlan.Create(p, null, 1000, 700);
        Assert.Equal(false, Value(options, "RedirectClipboard"));
        Assert.Equal(false, Value(options, "AudioCaptureRedirectionMode"));
    }
}
