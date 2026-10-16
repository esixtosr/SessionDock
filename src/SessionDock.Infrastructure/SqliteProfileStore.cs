using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using SessionDock.Core;

namespace SessionDock.Infrastructure;

/// <summary>Short transactions; profiles contain metadata and options, never authentication secrets.</summary>
public sealed class SqliteProfileStore : IDisposable
{
    public const int SchemaVersion = 2;
    public string DatabasePath { get; }
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = false };
    public SqliteProfileStore(string databasePath) => DatabasePath = Path.GetFullPath(databasePath);

    private SqliteConnection Open()
    {
        var db = new SqliteConnection(new SqliteConnectionStringBuilder
        { DataSource = DatabasePath, ForeignKeys = true, Pooling = false, DefaultTimeout = 5 }.ToString());
        db.Open();
        return db;
    }

    public void Initialize()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath)!);
        using var db = Open();
        int version = Convert.ToInt32(Scalar(db, "PRAGMA user_version;"), CultureInfo.InvariantCulture);
        if (version > SchemaVersion) throw new InvalidDataException("This library was created by a newer SessionDock. Update the application before opening it.");
        if (version > 0 && version < SchemaVersion)
        {
            string backupPath = DatabasePath + $".before-v{SchemaVersion}.{DateTime.UtcNow:yyyyMMddHHmmssfff}.bak";
            using var backup = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = backupPath, Pooling = false }.ToString());
            backup.Open();
            db.BackupDatabase(backup);
        }
        Execute(db, "PRAGMA journal_mode=WAL;");
        using var tx = db.BeginTransaction();
        if (version < 1)
        {
            Execute(db, """
                CREATE TABLE groups (id TEXT PRIMARY KEY, name TEXT NOT NULL COLLATE NOCASE UNIQUE, sort_order INTEGER NOT NULL, expanded INTEGER NOT NULL);
                CREATE TABLE credentials (id TEXT PRIMARY KEY, label TEXT NOT NULL, username TEXT NOT NULL, domain TEXT NOT NULL);
                CREATE TABLE gateways (id TEXT PRIMARY KEY, label TEXT NOT NULL, host TEXT NOT NULL, credential_id TEXT NULL REFERENCES credentials(id) ON DELETE SET NULL);
                CREATE TABLE connections (id TEXT PRIMARY KEY, group_id TEXT NOT NULL REFERENCES groups(id), credential_id TEXT NULL REFERENCES credentials(id) ON DELETE SET NULL,
                    gateway_id TEXT NULL REFERENCES gateways(id) ON DELETE SET NULL, last_connected TEXT NULL, payload TEXT NOT NULL);
                CREATE TABLE settings (id INTEGER PRIMARY KEY CHECK(id=1), payload TEXT NOT NULL);
                CREATE TABLE resolutions (id TEXT PRIMARY KEY, width INTEGER NOT NULL, height INTEGER NOT NULL, sort_order INTEGER NOT NULL, UNIQUE(width,height));
                """, tx);
            SaveGroup(db, tx, Group.CreateDefault());
            WriteSettings(db, tx, new AppSettings());
            WriteResolutions(db, tx, ResolutionPreset.Defaults());
        }
        if (version < 2)
            Execute(db, """
                CREATE INDEX ix_connections_group ON connections(group_id);
                CREATE INDEX ix_connections_credential ON connections(credential_id);
                CREATE INDEX ix_connections_gateway ON connections(gateway_id);
                CREATE TABLE thumbnails (id TEXT PRIMARY KEY, connection_id TEXT NOT NULL REFERENCES connections(id) ON DELETE CASCADE, file_name TEXT NOT NULL, captured_at TEXT NOT NULL);
                """, tx);
        Execute(db, $"PRAGMA user_version={SchemaVersion};", tx);
        tx.Commit();
    }

    public LibrarySnapshot Load()
    {
        using var db = Open();
        using var tx = db.BeginTransaction(deferred: true);
        var result = new LibrarySnapshot
        {
            Groups = Read(db, tx, "SELECT id,name,sort_order,expanded FROM groups ORDER BY sort_order,name;", r => new Group { Id = Guid.Parse(r.GetString(0)), Name = r.GetString(1), Order = r.GetInt32(2), IsExpanded = r.GetBoolean(3) }),
            Credentials = Read(db, tx, "SELECT id,label,username,domain FROM credentials ORDER BY label;", r => new CredentialProfile { Id = Guid.Parse(r.GetString(0)), Label = r.GetString(1), Username = r.GetString(2), Domain = r.GetString(3) }),
            Gateways = Read(db, tx, "SELECT id,label,host,credential_id FROM gateways ORDER BY label;", r => new GatewayProfile { Id = Guid.Parse(r.GetString(0)), Label = r.GetString(1), Host = r.GetString(2), CredentialId = NullableGuid(r, 3) }),
            Resolutions = Read(db, tx, "SELECT id,width,height FROM resolutions ORDER BY sort_order;", r => new ResolutionPreset { Id = Guid.Parse(r.GetString(0)), Width = r.GetInt32(1), Height = r.GetInt32(2) }),
            Settings = Deserialize<AppSettings>((string)Scalar(db, "SELECT payload FROM settings WHERE id=1;", tx)!)
        };
        result.Connections = Read(db, tx, "SELECT payload,group_id,credential_id,gateway_id,last_connected FROM connections;", r =>
        {
            var p = Deserialize<ConnectionProfile>(r.GetString(0));
            // Foreign-key columns are authoritative after shared-profile deletion/reassignment.
            p.GroupId = Guid.Parse(r.GetString(1)); p.CredentialId = NullableGuid(r, 2); p.GatewayId = NullableGuid(r, 3);
            p.LastConnectedAt = r.IsDBNull(4) ? null : DateTimeOffset.Parse(r.GetString(4), CultureInfo.InvariantCulture);
            return p;
        });
        tx.Commit();
        var errors = LibraryValidator.Validate(result);
        if (errors.Count > 0) throw new InvalidDataException("The stored library needs recovery: " + string.Join(" ", errors));
        return result;
    }

    public void SaveConnection(ConnectionProfile profile)
    {
        ThrowInvalid(ProfileValidator.Validate(profile));
        using var db = Open(); using var tx = db.BeginTransaction();
        WriteConnection(db, tx, profile);
        tx.Commit();
    }
    public void SaveConnectionWithGateway(ConnectionProfile profile, GatewayProfile gateway)
    {
        ThrowInvalid(ProfileValidator.Validate(profile));
        if (profile.GatewayId != gateway.Id) throw new ArgumentException("The new PC and staged gateway must reference the same gateway ID.");
        if (gateway.Id == Guid.Empty || string.IsNullOrWhiteSpace(gateway.Label) || !EndpointParser.TryParse(gateway.Host, out _, out _) || gateway.Host.Contains(':'))
            throw new ArgumentException("Enter a gateway label and DNS hostname or IPv4 address without a port.");
        using var db = Open(); using var tx = db.BeginTransaction();
        WriteGateway(db, tx, gateway); WriteConnection(db, tx, profile); tx.Commit();
    }
    private static void WriteConnection(SqliteConnection db, SqliteTransaction tx, ConnectionProfile p) => Execute(db, """
        INSERT INTO connections(id,group_id,credential_id,gateway_id,last_connected,payload) VALUES($id,$group,$credential,$gateway,$last,$payload)
        ON CONFLICT(id) DO UPDATE SET group_id=excluded.group_id,credential_id=excluded.credential_id,gateway_id=excluded.gateway_id,
            last_connected=CASE WHEN excluded.last_connected IS NULL THEN connections.last_connected
                WHEN connections.last_connected IS NULL OR julianday(excluded.last_connected)>julianday(connections.last_connected) THEN excluded.last_connected
                ELSE connections.last_connected END,payload=excluded.payload;
        """, tx, ("$id", p.Id), ("$group", p.GroupId), ("$credential", p.CredentialId), ("$gateway", p.GatewayId), ("$last", p.LastConnectedAt?.ToString("O")), ("$payload", Serialize(p)));

    public void DeleteConnection(Guid id) { using var db = Open(); Execute(db, "DELETE FROM connections WHERE id=$id;", null, ("$id", id)); }
    public void MarkConnected(Guid id, DateTimeOffset at) { using var db = Open(); Execute(db, "UPDATE connections SET last_connected=$at WHERE id=$id AND (last_connected IS NULL OR julianday($at)>julianday(last_connected));", null, ("$at", at.ToString("O")), ("$id", id)); }

    public void SaveGroup(Group group)
    {
        if (group.Id == Guid.Empty || string.IsNullOrWhiteSpace(group.Name) || group.Name.Trim().Length > 100 || group.Name.Any(char.IsControl)) throw new ArgumentException("Enter a group name of 1–100 printable characters.");
        if (group.Id == Group.DefaultId && (group.Name != "Saved Devices" || group.Order != 0)) throw new ArgumentException("The default group's name and fallback position are protected.");
        using var db = Open(); using var tx = db.BeginTransaction(); SaveGroup(db, tx, group); tx.Commit();
    }
    private static void SaveGroup(SqliteConnection db, SqliteTransaction tx, Group group) => Execute(db, """
        INSERT INTO groups(id,name,sort_order,expanded) VALUES($id,$name,$order,$expanded)
        ON CONFLICT(id) DO UPDATE SET name=excluded.name,sort_order=excluded.sort_order,expanded=excluded.expanded;
        """, tx, ("$id", group.Id), ("$name", group.Name.Trim()), ("$order", group.Order), ("$expanded", group.IsExpanded));
    public void DeleteGroup(Guid id)
    {
        if (id == Group.DefaultId) throw new InvalidOperationException("Saved Devices is the permanent fallback group.");
        using var db = Open(); using var tx = db.BeginTransaction();
        Execute(db, "UPDATE connections SET group_id=$fallback WHERE group_id=$id;", tx, ("$fallback", Group.DefaultId), ("$id", id));
        Execute(db, "DELETE FROM groups WHERE id=$id;", tx, ("$id", id)); tx.Commit();
    }
    public void SaveCredential(CredentialProfile credential)
    {
        if (credential.Id == Guid.Empty || string.IsNullOrWhiteSpace(credential.Label) || string.IsNullOrWhiteSpace(credential.Username)) throw new ArgumentException("Credential label and username are required.");
        using var db = Open(); using var tx = db.BeginTransaction(); WriteCredential(db, tx, credential); tx.Commit();
    }
    private static void WriteCredential(SqliteConnection db, SqliteTransaction tx, CredentialProfile c) => Execute(db, """
        INSERT INTO credentials(id,label,username,domain) VALUES($id,$label,$username,$domain)
        ON CONFLICT(id) DO UPDATE SET label=excluded.label,username=excluded.username,domain=excluded.domain;
        """, tx, ("$id", c.Id), ("$label", c.Label.Trim()), ("$username", c.Username), ("$domain", c.Domain));
    public void DeleteCredential(Guid id) { using var db = Open(); Execute(db, "DELETE FROM credentials WHERE id=$id;", null, ("$id", id)); }

    public void SaveGateway(GatewayProfile gateway)
    {
        if (gateway.Id == Guid.Empty || string.IsNullOrWhiteSpace(gateway.Label)) throw new ArgumentException("A gateway label is required.");
        if (!EndpointParser.TryParse(gateway.Host, out _, out _) || gateway.Host.Contains(':')) throw new ArgumentException("Use the RD Gateway's DNS hostname or IPv4 address, without a port.");
        using var db = Open(); using var tx = db.BeginTransaction(); WriteGateway(db, tx, gateway); tx.Commit();
    }
    private static void WriteGateway(SqliteConnection db, SqliteTransaction tx, GatewayProfile g) => Execute(db, """
        INSERT INTO gateways(id,label,host,credential_id) VALUES($id,$label,$host,$credential)
        ON CONFLICT(id) DO UPDATE SET label=excluded.label,host=excluded.host,credential_id=excluded.credential_id;
        """, tx, ("$id", g.Id), ("$label", g.Label.Trim()), ("$host", g.Host), ("$credential", g.CredentialId));
    /// <summary>The caller must explicitly confirm affected connections will switch to direct access.</summary>
    public void DeleteGateway(Guid id) { using var db = Open(); Execute(db, "DELETE FROM gateways WHERE id=$id;", null, ("$id", id)); }

    public void SaveSettings(AppSettings settings)
    {
        ThrowInvalid(LibraryValidator.Validate(new LibrarySnapshot { Settings = settings }));
        using var db = Open(); using var tx = db.BeginTransaction(); WriteSettings(db, tx, settings); tx.Commit();
    }
    private static void WriteSettings(SqliteConnection db, SqliteTransaction tx, AppSettings settings) => Execute(db, "INSERT INTO settings(id,payload) VALUES(1,$payload) ON CONFLICT(id) DO UPDATE SET payload=excluded.payload;", tx, ("$payload", Serialize(settings)));
    public void SaveResolutions(IEnumerable<ResolutionPreset> resolutions)
    {
        var values = resolutions.ToList();
        ThrowInvalid(LibraryValidator.Validate(new LibrarySnapshot { Resolutions = values }));
        using var db = Open(); using var tx = db.BeginTransaction(); WriteResolutions(db, tx, values); tx.Commit();
    }
    private static void WriteResolutions(SqliteConnection db, SqliteTransaction tx, IEnumerable<ResolutionPreset> values)
    {
        Execute(db, "DELETE FROM resolutions;", tx); int i = 0;
        foreach (var r in values) Execute(db, "INSERT INTO resolutions(id,width,height,sort_order) VALUES($id,$width,$height,$order);", tx,
            ("$id", r.Id), ("$width", r.Width), ("$height", r.Height), ("$order", i++));
    }

    /// <summary>Replace from a reviewed metadata-only import; secrets remain outside the database.</summary>
    public void ReplaceLibrary(LibrarySnapshot snapshot)
    {
        ThrowInvalid(LibraryValidator.Validate(snapshot));
        using var db = Open(); using var tx = db.BeginTransaction();
        Execute(db, "DELETE FROM connections; DELETE FROM gateways; DELETE FROM credentials; DELETE FROM groups;", tx);
        foreach (var g in snapshot.Groups) SaveGroup(db, tx, g);
        foreach (var c in snapshot.Credentials) WriteCredential(db, tx, c);
        foreach (var g in snapshot.Gateways) WriteGateway(db, tx, g);
        foreach (var p in snapshot.Connections) WriteConnection(db, tx, p);
        WriteSettings(db, tx, snapshot.Settings); WriteResolutions(db, tx, snapshot.Resolutions); tx.Commit();
    }

    public void CreateBackup(string destination)
    {
        string full = Path.GetFullPath(destination);
        if (string.Equals(full, DatabasePath, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Choose a different backup path.");
        if (File.Exists(full)) throw new IOException("A file already exists at the backup destination.");
        using var source = Open();
        using var backup = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = full, Pooling = false }.ToString());
        backup.Open(); source.BackupDatabase(backup);
    }

    public void DeleteThumbnailMetadata() { using var db = Open(); Execute(db, "DELETE FROM thumbnails;"); }
    private static Guid? NullableGuid(SqliteDataReader r, int ordinal) => r.IsDBNull(ordinal) ? null : Guid.Parse(r.GetString(ordinal));
    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Json);
    private static T Deserialize<T>(string value) => JsonSerializer.Deserialize<T>(value, Json) ?? throw new InvalidDataException("A stored record is empty.");
    private static void ThrowInvalid(IReadOnlyList<string> errors) { if (errors.Count > 0) throw new ArgumentException(string.Join(Environment.NewLine, errors)); }
    private static List<T> Read<T>(SqliteConnection db, SqliteTransaction tx, string sql, Func<SqliteDataReader, T> map)
    {
        using var cmd = db.CreateCommand(); cmd.Transaction = tx; cmd.CommandText = sql;
        using var reader = cmd.ExecuteReader(); var items = new List<T>(); while (reader.Read()) items.Add(map(reader)); return items;
    }
    private static object? Scalar(SqliteConnection db, string sql, SqliteTransaction? tx = null)
    { using var cmd = db.CreateCommand(); cmd.Transaction = tx; cmd.CommandText = sql; return cmd.ExecuteScalar(); }
    private static void Execute(SqliteConnection db, string sql, SqliteTransaction? tx = null, params (string Name, object? Value)[] values)
    {
        using var cmd = db.CreateCommand(); cmd.Transaction = tx; cmd.CommandText = sql;
        foreach (var (name, value) in values) cmd.Parameters.AddWithValue(name, value is Guid id ? id.ToString("D") : value ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }
    public void Dispose() { /* Connections are short-lived; no retained database handles. */ }
}
