using CommunityToolkit.Mvvm.ComponentModel;
using SessionDock.Core;
using SessionDock.Infrastructure;

namespace SessionDock.App.ViewModels;

public sealed class LibraryViewModel : ObservableObject, IDisposable
{
    public SqliteProfileStore Store { get; }
    public WindowsCredentialVault Vault { get; } = new();
    public string DataDirectory { get; }
    public LibrarySnapshot Snapshot { get; private set; }
    public Dictionary<Guid, SessionState> SessionStates { get; } = [];
    public List<string> Notifications { get; } = [];
    public int OpenSessionCount { get; set; }
    private readonly LocalDiagnostics _diagnostics;
    private AppSettings _persistedSettings;
    private string _query = "";
    public string Query { get => _query; set => SetProperty(ref _query, value); }
    public bool FavoritesOnly { get; set; }
    public event EventHandler? Changed;
    public LibraryViewModel(string dataDirectory)
    {
        DataDirectory = dataDirectory;
        _diagnostics = new LocalDiagnostics(System.IO.Path.Combine(dataDirectory, "logs"));
        Store = new SqliteProfileStore(System.IO.Path.Combine(dataDirectory, "library.db"));
        Store.Initialize(); Snapshot = Store.Load(); _persistedSettings = Snapshot.Settings.DeepClone();
    }
    public void Reload() { Snapshot = Store.Load(); _persistedSettings = Snapshot.Settings.DeepClone(); OnPropertyChanged(nameof(Snapshot)); Changed?.Invoke(this, EventArgs.Empty); }
    public void SaveSettings()
    {
        try { Store.SaveSettings(Snapshot.Settings); }
        catch { Snapshot.Settings = _persistedSettings.DeepClone(); Changed?.Invoke(this, EventArgs.Empty); throw; }
        _persistedSettings = Snapshot.Settings.DeepClone(); Changed?.Invoke(this, EventArgs.Empty);
    }
    public string GroupName(Guid id) => Snapshot.Groups.FirstOrDefault(g => g.Id == id)?.Name ?? "Saved Devices";
    public string CredentialLabel(Guid? id) => Snapshot.Credentials.FirstOrDefault(c => c.Id == id)?.Label ?? "Ask when required";
    public SessionState State(Guid id) => SessionStates.GetValueOrDefault(id, SessionState.Disconnected);
    public IEnumerable<ConnectionProfile> Filtered()
    {
        var source = Snapshot.Connections.Where(c => !FavoritesOnly || c.Favorite);
        if (!string.IsNullOrWhiteSpace(Query)) source = source.Where(c => new[] { c.DisplayName, c.Host, GroupName(c.GroupId), CredentialLabel(c.CredentialId) }.Any(s => s.Contains(Query.Trim(), StringComparison.CurrentCultureIgnoreCase)));
        var settings = Snapshot.Settings;
        IOrderedEnumerable<ConnectionProfile> ordered = settings.Sort switch
        {
            LibrarySort.LastConnected => settings.SortDescending ? source.OrderByDescending(c => c.LastConnectedAt) : source.OrderBy(c => c.LastConnectedAt),
            LibrarySort.Created => settings.SortDescending ? source.OrderByDescending(c => c.CreatedAt) : source.OrderBy(c => c.CreatedAt),
            _ => settings.SortDescending ? source.OrderByDescending(c => c.DisplayName, StringComparer.CurrentCultureIgnoreCase) : source.OrderBy(c => c.DisplayName, StringComparer.CurrentCultureIgnoreCase)
        };
        return ordered.ThenBy(c => c.Id);
    }
    public void SetSessionState(Guid id, SessionState state, string message, int? code = null)
    {
        var previous = State(id);
        SessionStates[id] = state;
        try
        {
            _diagnostics.Session(id, state, code);
            if (state == SessionState.Connected && previous != SessionState.Connected) { Store.MarkConnected(id, DateTimeOffset.UtcNow); Snapshot = Store.Load(); }
        }
        catch (Exception)
        {
            Notifications.Insert(0, "Session status changed, but local status history could not be saved. The live connection is retained.");
        }
        if (state == SessionState.Failed) Notifications.Insert(0, $"{DateTimeOffset.Now:g} — {message}");
        if (Notifications.Count > 100) Notifications.RemoveRange(100, Notifications.Count - 100);
        try { Changed?.Invoke(this, EventArgs.Empty); }
        catch (Exception) { Notifications.Insert(0, "The library view could not refresh. The session remains available in its own window."); }
    }
    public void Dispose() => Store.Dispose();
}
