using System.Security;
using SessionDock.Core;

namespace SessionDock.Tests;

public sealed class EndpointTests
{
    [Theory]
    [InlineData("pc01", "pc01", 3389)]
    [InlineData(" Pc01.Example.Invalid ", "pc01.example.invalid", 3389)]
    [InlineData("pc01.example.invalid:3390", "pc01.example.invalid", 3390)]
    [InlineData("192.0.2.10", "192.0.2.10", 3389)]
    [InlineData("192.0.2.10:65535", "192.0.2.10", 65535)]
    [InlineData("[2001:db8::1]", "2001:db8::1", 3389)]
    [InlineData("[2001:db8::1]:3391", "2001:db8::1", 3391)]
    [InlineData("büro.example.invalid", "xn--bro-hoa.example.invalid", 3389)]
    public void ParsesSupportedHostForms(string input, string host, int port)
    {
        Assert.True(EndpointParser.TryParse(input, out var endpoint, out var error), error);
        Assert.Equal(host, endpoint!.Host); Assert.Equal(port, endpoint.Port);
    }
    [Theory]
    [InlineData("")][InlineData("rdp://pc")][InlineData("pc:0")][InlineData("pc:65536")]
    [InlineData("pc:-2")][InlineData("pc:")][InlineData("pc:+3389")][InlineData("pc\n.example")]
    [InlineData("pc/command")][InlineData("name@pc")][InlineData("192.0.2.999")]
    [InlineData("127.1")][InlineData("192.000.2.1")][InlineData("-host")][InlineData("host..name")]
    [InlineData("[192.0.2.1]")][InlineData("[2001:db8::1")][InlineData("2001:db8::1")]
    [InlineData("[2001:db8::1]junk")][InlineData("[2001:db8::1]:0")]
    public void RejectsInvalidOrAmbiguousEndpoints(string input)
    { Assert.False(EndpointParser.TryParse(input, out var endpoint, out var error)); Assert.Null(endpoint); Assert.NotEmpty(error!); }

    [Fact] public void ProfileRejectsInjectedNewlinesBeforeExport()
    { Assert.NotEmpty(ProfileValidator.Validate(new() { Host = "pc\r\nalternate shell:s:example" })); }
}

public sealed class LibraryLogicTests
{
    [Fact] public void DefaultsDoNotRewriteExistingProfilesOrShareMutableLists()
    {
        var settings = new AppSettings();
        settings.DefaultRedirection.Folders.Add(new() { Name = "Work", Path = @"C:\Work" });
        var first = LibraryOperations.NewConnection(settings);
        settings.DefaultDisplay.Width = 2560;
        settings.DefaultRedirection.Folders[0].Name = "Changed";
        var second = LibraryOperations.NewConnection(settings);
        Assert.Equal(1920, first.Display.Width); Assert.Equal(2560, second.Display.Width);
        Assert.Equal("Work", first.Redirection.Folders[0].Name);
    }
    [Fact] public void EditingDraftDoesNotWriteToSource()
    {
        var source = new ConnectionProfile { Host = "pc.example.invalid" };
        source.Redirection.Folders.Add(new() { Name = "Work", Path = @"C:\Work" });
        var draft = source.DeepClone(); draft.Host = "changed.example.invalid";
        draft.Redirection.Folders[0].ReadOnly = true;
        Assert.Equal("pc.example.invalid", source.Host); Assert.False(source.Redirection.Folders[0].ReadOnly);
    }
    [Fact] public void GroupDeletionPreservesConnectionsAndDefaultGroup()
    {
        var library = new LibrarySnapshot(); var group = new Group { Name = "Lab" }; library.Groups.Add(group);
        var connection = new ConnectionProfile { Host = "pc.example.invalid", GroupId = group.Id }; library.Connections.Add(connection);
        LibraryOperations.DeleteGroup(library, group.Id);
        Assert.Single(library.Connections); Assert.Equal(Group.DefaultId, connection.GroupId);
        Assert.Throws<InvalidOperationException>(() => LibraryOperations.DeleteGroup(library, Group.DefaultId));
    }
    [Fact] public void CredentialDeletionMovesPcAndGatewayReferencesToPrompt()
    {
        var library = new LibrarySnapshot(); var credential = new CredentialProfile { Label = "Test", Username = "user" };
        library.Credentials.Add(credential);
        library.Connections.Add(new() { Host = "pc.example.invalid", CredentialId = credential.Id });
        library.Gateways.Add(new() { Host = "gateway.example.invalid", Label = "Gateway", CredentialId = credential.Id });
        LibraryOperations.DeleteCredential(library, credential.Id);
        Assert.Empty(library.Credentials); Assert.Null(library.Connections[0].CredentialId); Assert.Null(library.Gateways[0].CredentialId);
    }
    [Fact] public void HasAllTwentyThreeVisibleResolutionPresets()
    {
        var presets = ResolutionPreset.Defaults();
        Assert.Equal(23, presets.Count); Assert.Contains(presets, p => p.Width == 2880 && p.Height == 1800);
        Assert.Equal(23, presets.Select(x => (x.Width, x.Height)).Distinct().Count());
    }
    [Theory][InlineData(199, 1080)][InlineData(1920, 8193)]
    public void RejectsResolutionOutsideDocumentedRange(int width, int height)
    { Assert.NotEmpty(ProfileValidator.ValidateResolution(width, height)); }
    [Fact] public void RemovedPresetDoesNotChangeConnectionResolution()
    {
        var library = new LibrarySnapshot(); var connection = new ConnectionProfile { Host = "pc.example.invalid", Display = new() { AutomaticResolution = false, Width = 2560, Height = 1440 } };
        library.Connections.Add(connection); library.Resolutions.RemoveAll(x => x.Width == 2560);
        Assert.Equal(2560, connection.Display.Width); Assert.Empty(LibraryValidator.Validate(library));
    }
    [Fact] public void FolderPathsAreComparedWithWindowsSemantics()
    {
        var options = new RedirectionOptions { Folders = [new() { Name = "A", Path = @"C:\Work\" }, new() { Name = "B", Path = "c:/work" }] };
        Assert.NotEmpty(ProfileValidator.ValidateFolders(options));
    }
    [Fact] public void CredentialWrapperOwnsSeparateSecret()
    {
        using var original = new SecureString(); original.AppendChar('x');
        using var credentials = new SessionCredentials(@"DOMAIN\user", original);
        original.Clear(); Assert.Equal(1, credentials.Password.Length); Assert.Equal(@"DOMAIN\user", credentials.Username);
    }
}

public sealed class SessionStateTests
{
    [Fact] public void TransportConnectionDoesNotProveLogon()
    {
        var state = SessionStateReducer.Apply(new(), SessionSignal.BeginConnect);
        state = SessionStateReducer.Apply(state, SessionSignal.TransportConnected);
        Assert.Equal(SessionState.Authenticating, state.State); Assert.False(state.HasAuthenticated);
        state = SessionStateReducer.Apply(state, SessionSignal.LoginComplete);
        Assert.Equal(SessionState.Connected, state.State); Assert.True(state.HasAuthenticated);
    }
    [Fact] public void UnsolicitedLoginEventCannotManufactureConnectedState()
    { Assert.Equal(SessionState.Disconnected, SessionStateReducer.Apply(new(), SessionSignal.LoginComplete).State); }
    [Fact] public void DeliberateDisconnectCancelsReconnectionAndLateLogon()
    {
        var state = new SessionStateSnapshot(SessionState.Connected, HasAuthenticated: true, ReconnectAllowed: true);
        state = SessionStateReducer.Apply(state, SessionSignal.ConnectionLost);
        Assert.Equal(SessionState.Reconnecting, state.State);
        state = SessionStateReducer.Apply(state, SessionSignal.UserDisconnect);
        state = SessionStateReducer.Apply(state, SessionSignal.LoginComplete);
        Assert.Equal(SessionState.Disconnecting, state.State); Assert.False(state.ReconnectAllowed);
        Assert.Equal(SessionState.Disconnected, SessionStateReducer.Apply(state, SessionSignal.Disconnected).State);
    }
    [Theory][InlineData(SessionSignal.AuthenticationRejected)][InlineData(SessionSignal.CertificateDecisionRequired)]
    public void UserActionRequiredStopsRetries(SessionSignal signal)
    {
        var state = SessionStateReducer.Apply(new(SessionState.Authenticating, ReconnectAllowed: true), signal);
        Assert.Equal(SessionState.Failed, state.State); Assert.False(state.ReconnectAllowed);
        Assert.Equal(SessionState.Failed, SessionStateReducer.Apply(state, SessionSignal.ReconnectStarted).State);
    }
    [Fact] public void DisabledAutoReconnectRemainsDisabledAfterLoss()
    {
        var state = SessionStateReducer.Apply(new(SessionState.Connected, true, false, false), SessionSignal.ConnectionLost, false);
        Assert.Equal(SessionState.Failed, state.State); Assert.False(state.ReconnectAllowed);
    }
}
