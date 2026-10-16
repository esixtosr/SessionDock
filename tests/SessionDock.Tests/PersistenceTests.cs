using Microsoft.Data.Sqlite;
using SessionDock.Core;
using SessionDock.Infrastructure;

namespace SessionDock.Tests;

public sealed class PersistenceTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "SessionDockTests-" + Guid.NewGuid().ToString("N"));
    private readonly string path;
    public PersistenceTests() { Directory.CreateDirectory(directory); path = Path.Combine(directory, "library.db"); }
    private SqliteProfileStore Create()
    { var store = new SqliteProfileStore(path); store.Initialize(); return store; }
    private void Sql(string commandText)
    {
        using var db = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString());
        db.Open(); using var command = db.CreateCommand(); command.CommandText = commandText; command.ExecuteNonQuery();
    }
    private static long Scalar(string database, string sql)
    {
        using var db = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = database, Pooling = false }.ToString());
        db.Open(); using var command = db.CreateCommand(); command.CommandText = sql; return Convert.ToInt64(command.ExecuteScalar());
    }
    [Fact] public void NormalFirstRunIsEmptyAndHasReferencePresets()
    {
        using var store = Create(); var library = store.Load();
        Assert.Empty(library.Connections); Assert.Empty(library.Credentials); Assert.Empty(library.Gateways);
        Assert.Equal(Group.DefaultId, Assert.Single(library.Groups).Id); Assert.Equal(23, library.Resolutions.Count);
        Assert.False(library.Settings.ScreenshotThumbnails);
    }
    [Fact] public void ProfilesAndViewSettingsSurviveFreshStoreInstance()
    {
        var connection = new ConnectionProfile { Host = "pc.example.invalid", FriendlyName = "Testing", Favorite = true, Port = 3399, AdminSession = true, Display = new() { Width = 2560, Height = 1440, AutomaticResolution = false }, Input = new() { KeyboardRouting = KeyboardRouting.Local } };
        using (var store = Create())
        {
            store.SaveConnection(connection);
            store.SaveSettings(new() { View = LibraryView.List, Sort = LibrarySort.Created, SortDescending = true, GroupConnections = false, Theme = AppTheme.Light, CardSize = CardSize.Large });
        }
        using var reopened = Create(); var library = reopened.Load(); var loaded = Assert.Single(library.Connections);
        Assert.Equal(connection.Id, loaded.Id); Assert.Equal(connection.Endpoint, loaded.Endpoint); Assert.Equal(connection.Display, loaded.Display);
        Assert.True(loaded.Favorite); Assert.True(loaded.AdminSession); Assert.Equal(connection.Input, loaded.Input);
        Assert.Equal(LibraryView.List, library.Settings.View); Assert.Equal(LibrarySort.Created, library.Settings.Sort); Assert.True(library.Settings.SortDescending); Assert.False(library.Settings.GroupConnections);
    }
    [Fact] public void MigratesVersionOneAndCreatesRecoverablePriorSchemaBackup()
    {
        using (var store = Create()) store.SaveConnection(new() { Host = "pc.example.invalid" });
        Sql("DROP TABLE thumbnails; DROP INDEX ix_connections_group; DROP INDEX ix_connections_credential; DROP INDEX ix_connections_gateway; PRAGMA user_version=1;");
        using var migrated = Create(); Assert.Single(migrated.Load().Connections);
        Assert.Equal(2, Scalar(path, "PRAGMA user_version;"));
        Assert.Equal(1, Scalar(path, "SELECT count(*) FROM sqlite_master WHERE name='thumbnails';"));
        var backup = Assert.Single(Directory.GetFiles(directory, "*.bak"));
        Assert.Equal(1, Scalar(backup, "PRAGMA user_version;")); Assert.Equal(1, Scalar(backup, "SELECT count(*) FROM connections;"));
    }
    [Fact] public void RefusesFutureSchemaWithoutRewritingIt()
    {
        using (var store = Create()) store.SaveConnection(new() { Host = "pc.example.invalid" });
        Sql("PRAGMA user_version=987;");
        using var future = new SqliteProfileStore(path);
        Assert.Throws<InvalidDataException>(() => future.Initialize());
        Assert.Equal(987, Scalar(path, "PRAGMA user_version;")); Assert.Equal(1, Scalar(path, "SELECT count(*) FROM connections;"));
    }
    [Fact] public void ReviewedImportedPcAndGatewaySaveTogether()
    {
        using var store = Create();
        var gateway = new GatewayProfile { Label = "Imported gateway", Host = "gateway.example.invalid" };
        var connection = new ConnectionProfile { Host = "pc.example.invalid", GatewayId = gateway.Id };
        store.SaveConnectionWithGateway(connection, gateway);
        using var reopened = Create(); var library = reopened.Load();
        Assert.Equal(gateway.Id, Assert.Single(library.Gateways).Id);
        Assert.Equal(gateway.Id, Assert.Single(library.Connections).GatewayId);
    }
    [Fact] public void FailedImportedPcDoesNotLeaveAnOrphanGateway()
    {
        using var store = Create();
        var gateway = new GatewayProfile { Label = "Staged gateway", Host = "gateway.example.invalid" };
        var connection = new ConnectionProfile { Host = "pc.example.invalid", GatewayId = gateway.Id, GroupId = Guid.NewGuid() };
        Assert.Throws<SqliteException>(() => store.SaveConnectionWithGateway(connection, gateway));
        using var reopened = Create(); var library = reopened.Load();
        Assert.Empty(library.Connections); Assert.Empty(library.Gateways);
    }
    [Fact] public void ForeignKeyFailureRollsBackProfileEdit()
    {
        using var store = Create(); var profile = new ConnectionProfile { Host = "pc.example.invalid" }; store.SaveConnection(profile);
        var invalid = profile.DeepClone(); invalid.CredentialId = Guid.NewGuid(); invalid.FriendlyName = "Must not persist";
        Assert.Throws<SqliteException>(() => store.SaveConnection(invalid));
        var loaded = Assert.Single(store.Load().Connections); Assert.Null(loaded.CredentialId); Assert.Empty(loaded.FriendlyName);
    }
    [Fact] public void GroupDeletionRehomesProfilesAcrossRestart()
    {
        using var store = Create(); var group = new Group { Name = "Testing" }; store.SaveGroup(group);
        store.SaveConnection(new() { Host = "pc.example.invalid", GroupId = group.Id }); store.DeleteGroup(group.Id);
        using var reopened = Create(); var library = reopened.Load();
        Assert.Equal(Group.DefaultId, Assert.Single(library.Connections).GroupId); Assert.Single(library.Groups);
        Assert.Throws<InvalidOperationException>(() => store.DeleteGroup(Group.DefaultId));
    }
    [Fact] public void RemovingSharedProfilesClearsReferencesInDatabasePayloadView()
    {
        using var store = Create(); var credential = new CredentialProfile { Label = "Test account", Username = @"DOMAIN\test" }; store.SaveCredential(credential);
        var gateway = new GatewayProfile { Label = "Test gateway", Host = "gateway.example.invalid", CredentialId = credential.Id }; store.SaveGateway(gateway);
        store.SaveConnection(new() { Host = "pc.example.invalid", CredentialId = credential.Id, GatewayId = gateway.Id });
        store.DeleteCredential(credential.Id);
        var afterCredential = store.Load(); Assert.Null(Assert.Single(afterCredential.Connections).CredentialId); Assert.Null(Assert.Single(afterCredential.Gateways).CredentialId);
        store.DeleteGateway(gateway.Id); Assert.Null(Assert.Single(store.Load().Connections).GatewayId);
    }
    [Fact] public void StaleProfileSaveCannotEraseOrRollBackActualLoginTimestamp()
    {
        using var store = Create(); var profile = new ConnectionProfile { Host = "pc.example.invalid" }; store.SaveConnection(profile);
        var at = DateTimeOffset.Parse("2026-10-14T12:00:00Z"); store.MarkConnected(profile.Id, at);
        profile.FriendlyName = "Edited while connected"; store.SaveConnection(profile);
        Assert.Equal(at, Assert.Single(store.Load().Connections).LastConnectedAt);
        profile.LastConnectedAt = at.AddHours(-1); store.SaveConnection(profile); store.MarkConnected(profile.Id, at.AddMinutes(-1));
        Assert.Equal(at, Assert.Single(store.Load().Connections).LastConnectedAt);
        store.MarkConnected(profile.Id, at.AddMinutes(1)); Assert.Equal(at.AddMinutes(1), Assert.Single(store.Load().Connections).LastConnectedAt);
    }
    [Fact] public void SettingsValidationFailureLeavesPreviousSettingsIntact()
    {
        using var store = Create(); store.SaveSettings(new() { View = LibraryView.List });
        Assert.Throws<ArgumentException>(() => store.SaveSettings(new() { View = (LibraryView)999 }));
        Assert.Equal(LibraryView.List, store.Load().Settings.View);
    }
    [Fact] public void BackupCanBeOpenedAndContainsNoSecretColumn()
    {
        using var store = Create(); store.SaveConnection(new() { Host = "pc.example.invalid" });
        var backup = Path.Combine(directory, "backup.db"); store.CreateBackup(backup);
        using var restored = new SqliteProfileStore(backup); restored.Initialize(); Assert.Single(restored.Load().Connections);
        Assert.Equal(0, Scalar(backup, "SELECT count(*) FROM pragma_table_info('credentials') WHERE lower(name) LIKE '%password%' OR lower(name) LIKE '%secret%';"));
        Assert.Throws<ArgumentException>(() => store.CreateBackup(path)); Assert.Throws<IOException>(() => store.CreateBackup(backup));
    }
    [Fact] public void InvalidReplacementDoesNotDeleteCurrentLibrary()
    {
        using var store = Create(); store.SaveConnection(new() { Host = "pc.example.invalid" });
        var invalid = new LibrarySnapshot { Connections = [new() { Host = "new.example.invalid", GroupId = Guid.NewGuid() }] };
        Assert.Throws<ArgumentException>(() => store.ReplaceLibrary(invalid));
        Assert.Equal("pc.example.invalid", Assert.Single(store.Load().Connections).Host);
    }
    public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
}
