using System.Reflection;

namespace SessionDock.App;

internal static class AppBuild
{
    private static readonly string Information = typeof(AppBuild).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "Unknown";
    public static string Version => Information.Split('+')[0];
    public static string Build => Information.Contains('+') ? Information.Split('+', 2)[1] : "Local build";
    public const string DownloadsUrl = "https://github.com/esixtosr/SessionDock/actions/workflows/windows.yml?query=branch%3Amain";
    public static string Details => $"SessionDock {Version}\nBuild: {Build}\nWindows: {Environment.OSVersion.Version}\nArchitecture: {System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}\nRunning from: {Environment.ProcessPath}";
}
