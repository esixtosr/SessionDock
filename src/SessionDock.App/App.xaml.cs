using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using SessionDock.App.Views;
using SessionDock.App.ViewModels;
using SessionDock.Core;

namespace SessionDock.App;

public partial class App : System.Windows.Application
{
    private Mutex? _singleInstance;
    private LibraryViewModel? _library;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            var data = ResolveDataDirectory(e.Args);
            string mutexName = "Local\\SessionDock.Library";
            if (e.Args.Length > 0) mutexName += ".UiTest." + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(data)))[..20];
            _singleInstance = new Mutex(true, mutexName, out var created);
            if (!created) { MessageBox.Show("SessionDock is already running. Open its library from the taskbar or system tray.", "SessionDock"); Shutdown(); return; }
            Directory.CreateDirectory(data);
            _library = new LibraryViewModel(data);
            ApplyTheme(_library.Snapshot.Settings.Theme);
            var window = new MainWindow(_library); MainWindow = window; window.Show();
        }
        catch (Exception ex) { MessageBox.Show($"SessionDock could not open its local library. Your existing files were retained.\n\n{ex.Message}", "SessionDock", MessageBoxButton.OK, MessageBoxImage.Error); Shutdown(1); }
    }
    private static string ResolveDataDirectory(string[] arguments)
    {
        if (arguments.Length == 0) return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SessionDock");
        if (arguments.Length != 2 || arguments[0] != "--ui-test-data" || !Path.IsPathFullyQualified(arguments[1])) throw new ArgumentException("The only supported launch argument is --ui-test-data followed by an absolute SessionDock-UiTest-* directory below the temporary directory.");
        var data = Path.GetFullPath(arguments[1]).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!data.StartsWith(temp + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(data).StartsWith("SessionDock-UiTest-", StringComparison.Ordinal)) throw new ArgumentException("UI test storage must be a SessionDock-UiTest-* directory below the Windows temporary directory.");
        for (var ancestor = new DirectoryInfo(data); ancestor is not null && !string.Equals(ancestor.FullName.TrimEnd(Path.DirectorySeparatorChar), temp, StringComparison.OrdinalIgnoreCase); ancestor = ancestor.Parent)
            if (ancestor.Exists && (ancestor.Attributes & FileAttributes.ReparsePoint) != 0) throw new ArgumentException("UI test storage cannot traverse directory links.");
        return data;
    }
    public static void ApplyTheme(AppTheme theme)
    {
        var resources = Current.Resources;
        if (SystemParameters.HighContrast)
        {
            resources["WindowBrush"] = SystemColors.WindowBrush; resources["PanelBrush"] = SystemColors.WindowBrush;
            resources["InputBrush"] = SystemColors.WindowBrush; resources["TextBrush"] = SystemColors.WindowTextBrush;
            resources["MutedBrush"] = SystemColors.WindowTextBrush; resources["AccentBrush"] = SystemColors.HighlightBrush;
            return;
        }
        bool light = theme == AppTheme.Light || theme == AppTheme.System && IsSystemLight();
        string[] keys = ["WindowBrush", "PanelBrush", "InputBrush", "TextBrush", "MutedBrush", "AccentBrush"];
        string[] colors = light ? ["#F4F6F4", "#FFFFFF", "#FFFFFF", "#202A24", "#4F6256", "#177C41"] : ["#202323", "#2A2E2D", "#363C3A", "#F2F5F3", "#BAC6BF", "#79D39D"];
        for (int i = 0; i < keys.Length; i++) resources[keys[i]] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colors[i]));
    }
    private static bool IsSystemLight()
    {
        using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize");
        return key?.GetValue("AppsUseLightTheme") is not int mode || mode != 0;
    }
    protected override void OnExit(ExitEventArgs e) { _library?.Dispose(); _singleInstance?.Dispose(); base.OnExit(e); }
}
