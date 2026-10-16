using System.Reflection;
using System.Runtime.InteropServices;

namespace SessionDock.Rdp.Windows;

/// <summary>Uses only the wrapper generated from Windows' own type library. No guessed IUnknown vtables.</summary>
internal static class GeneratedInteropBridge
{
    public static bool TrySetAuthenticationDialogParent(object control, IntPtr parent)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Interop.MSTSCLib.dll");
        if (parent == IntPtr.Zero || !File.Exists(path)) return false;
        try
        {
            var type = Assembly.LoadFrom(path).GetType("MSTSCLib.IMsRdpClientNonScriptable2", throwOnError: true)!;
            if (type.GUID != new Guid("17A5E535-4072-4FA4-AF32-C8D0D47345E9")) return false;
            var property = type.GetProperty("UIParentWindowHandle");
            // HWND must be pointer-sized in the wrapper generated from the installed typelib.
            if (property is null || !property.CanWrite || property.PropertyType != typeof(IntPtr)) return false;
            property.SetValue(control, parent);
            return true;
        }
        catch (Exception ex) when (ex is IOException or BadImageFormatException or TypeLoadException or ReflectionTypeLoadException or TargetInvocationException or COMException or ArgumentException or InvalidCastException)
        { return false; }
    }
    public static bool TrySetConnectionBarText(object control)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Interop.MSTSCLib.dll");
        if (!File.Exists(path)) return false;
        try
        {
            var assembly = Assembly.LoadFrom(path);
            var type = assembly.GetType("MSTSCLib.IMsRdpClientNonScriptable3", throwOnError: true)!;
            if (type.GUID != new Guid("B3378D90-0728-45C7-8ED7-B6159FB92219")) return false;
            type.GetProperty("ConnectionBarText")!.SetValue(control, "SessionDock — Ctrl+Alt+Break exits full screen");
            return true;
        }
        catch (Exception ex) when (ex is IOException or BadImageFormatException or TypeLoadException or ReflectionTypeLoadException or TargetInvocationException or COMException or ArgumentException or InvalidCastException or NullReferenceException)
        { return false; }
    }
}
