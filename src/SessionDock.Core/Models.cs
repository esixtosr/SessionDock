using System.Security;
using System.Text.Json.Serialization;

namespace SessionDock.Core;

public enum SessionState { Disconnected, Connecting, Authenticating, Connected, Reconnecting, Disconnecting, Failed }
public enum CapabilityStatus { Supported, Conditional, Unavailable, Deferred }
public enum LibraryView { Grid, List }
public enum LibrarySort { Name, LastConnected, Created }
public enum CardSize { Small, Medium, Large }
public enum AppTheme { System, Dark, Light }
public enum ClipboardMode { Disabled, Bidirectional }
public enum SoundMode { Local, Remote, Off }
public enum KeyboardRouting { Local, RemoteWhenFocused, RemoteInFullScreen }
public enum ConnectionBarBehavior { System, AlwaysVisible, AutoHide }

public sealed record ConnectionProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Host { get; set; } = "";
    public int Port { get; set; } = 3389;
    public string FriendlyName { get; set; } = "";
    public Guid GroupId { get; set; } = Group.DefaultId;
    public Guid? CredentialId { get; set; }
    public Guid? GatewayId { get; set; }
    public bool Favorite { get; set; }
    public bool GatewayBypass { get; set; }
    public bool AutoReconnect { get; set; } = true;
    public bool AdminSession { get; set; }
    public bool SwapMouseButtons { get; set; }
    public DisplayOptions Display { get; set; } = new();
    public RedirectionOptions Redirection { get; set; } = new();
    public InputOptions Input { get; set; } = new();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastConnectedAt { get; set; }
    [JsonIgnore]
    public string DisplayName => string.IsNullOrWhiteSpace(FriendlyName) ? Host : FriendlyName;
    [JsonIgnore]
    public string Endpoint => EndpointParser.Format(Host, Port);
    public ConnectionProfile DeepClone() => this with { Display = Display with { }, Redirection = Redirection.DeepClone(), Input = Input with { } };
}

public sealed record DisplayOptions
{
    public bool AutomaticResolution { get; set; } = true;
    public int Width { get; set; } = 1920;
    public int Height { get; set; } = 1080;
    public bool UseAllMonitors { get; set; }
    public bool StartFullScreen { get; set; }
    public bool FitToWindow { get; set; } = true;
    public int ColorDepth { get; set; } = 32;
    public bool HighDpi { get; set; }
    public bool DynamicResolution { get; set; } = true;
}

public sealed record RedirectionOptions
{
    public bool Printers { get; set; }
    public bool SmartCards { get; set; }
    public bool Microphone { get; set; }
    public bool Cameras { get; set; }
    public ClipboardMode Clipboard { get; set; } = ClipboardMode.Bidirectional;
    public SoundMode Sound { get; set; } = SoundMode.Local;
    public bool RedirectFolders { get; set; }
    public List<RedirectedFolder> Folders { get; set; } = [];
    public RedirectionOptions DeepClone() => this with { Folders = Folders.Select(x => x with { }).ToList() };
    public static RedirectionOptions Disabled() => new() { Clipboard = ClipboardMode.Disabled, Sound = SoundMode.Off };
}

public sealed record RedirectedFolder
{
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public bool ReadOnly { get; set; }
}

public sealed record InputOptions { public KeyboardRouting KeyboardRouting { get; set; } = KeyboardRouting.RemoteInFullScreen; }
public sealed record CredentialProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Label { get; set; } = "";
    public string Username { get; set; } = "";
    public string Domain { get; set; } = "";
}
public sealed record GatewayProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Label { get; set; } = "";
    public string Host { get; set; } = "";
    public Guid? CredentialId { get; set; }
}
public sealed record Group
{
    public static Guid DefaultId { get; } = Guid.Parse("994e5773-e3a4-4bca-9250-802239a2a349");
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public int Order { get; set; }
    public bool IsExpanded { get; set; } = true;
    public static Group CreateDefault() => new() { Id = DefaultId, Name = "Saved Devices", Order = 0 };
}
public sealed record ResolutionPreset
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int Width { get; set; }
    public int Height { get; set; }
    [JsonIgnore]
    public string Label => $"{Width} × {Height}";
    public static List<ResolutionPreset> Defaults() => new (int Width, int Height)[]
    {
        (640,480),(800,600),(1024,600),(1024,768),(1152,864),(1280,720),(1280,800),
        (1280,960),(1280,1024),(1366,768),(1400,1050),(1440,900),(1600,900),
        (1600,1000),(1600,1200),(1680,1050),(1920,1080),(1920,1200),(2048,1152),
        (2048,1536),(2560,1080),(2560,1440),(2880,1800)
    }.Select(x => new ResolutionPreset { Width = x.Width, Height = x.Height }).ToList();
}
public sealed record AppSettings
{
    public LibraryView View { get; set; } = LibraryView.Grid;
    public LibrarySort Sort { get; set; } = LibrarySort.Name;
    public bool SortDescending { get; set; }
    public bool GroupConnections { get; set; } = true;
    public CardSize CardSize { get; set; } = CardSize.Medium;
    public AppTheme Theme { get; set; } = AppTheme.Dark;
    public bool TrayEnabled { get; set; }
    public bool ScreenshotThumbnails { get; set; }
    public bool SmartTips { get; set; } = true;
    public bool NewVersionNotices { get; set; }
    public bool UseSystemHttpProxy { get; set; } = true;
    public bool PreferUdp { get; set; } = true;
    public ConnectionBarBehavior ConnectionBar { get; set; } = ConnectionBarBehavior.AlwaysVisible;
    public string QuickLauncherHotkey { get; set; } = "";
    public bool ImportedRdpRedirection { get; set; }
    public bool ImportedFoldersReadOnly { get; set; }
    public DisplayOptions DefaultDisplay { get; set; } = new();
    public RedirectionOptions DefaultRedirection { get; set; } = new();
    public InputOptions DefaultInput { get; set; } = new();
    public AppSettings DeepClone() => this with { DefaultDisplay = DefaultDisplay with { }, DefaultRedirection = DefaultRedirection.DeepClone(), DefaultInput = DefaultInput with { } };
}
public sealed record LibrarySnapshot
{
    public List<ConnectionProfile> Connections { get; set; } = [];
    public List<CredentialProfile> Credentials { get; set; } = [];
    public List<GatewayProfile> Gateways { get; set; } = [];
    public List<Group> Groups { get; set; } = [Group.CreateDefault()];
    public List<ResolutionPreset> Resolutions { get; set; } = ResolutionPreset.Defaults();
    public AppSettings Settings { get; set; } = new();
    public LibrarySnapshot DeepClone() => this with
    {
        Connections = Connections.Select(x => x.DeepClone()).ToList(),
        Credentials = Credentials.Select(x => x with { }).ToList(),
        Gateways = Gateways.Select(x => x with { }).ToList(),
        Groups = Groups.Select(x => x with { }).ToList(),
        Resolutions = Resolutions.Select(x => x with { }).ToList(),
        Settings = Settings.DeepClone()
    };
}
public sealed record ThumbnailMetadata
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ConnectionId { get; set; }
    public string FileName { get; set; } = "";
    public DateTimeOffset CapturedAt { get; set; }
}
public sealed record Capability(string Name, CapabilityStatus Status, string Explanation);
public sealed class SessionCredentials : IDisposable
{
    public string Username { get; }
    public string Domain { get; }
    public SecureString Password { get; }
    public SessionCredentials(string username, SecureString password, string? domain = null)
    {
        Username = username;
        Domain = domain ?? "";
        Password = password.Copy();
        Password.MakeReadOnly();
    }
    public void Dispose() => Password.Dispose();
}
