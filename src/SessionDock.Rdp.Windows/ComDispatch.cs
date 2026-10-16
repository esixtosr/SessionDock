using System.Reflection;
using System.Runtime.InteropServices;

namespace SessionDock.Rdp.Windows;

internal static class ComDispatch
{
    public static object Get(object target, string member) => target.GetType().InvokeMember(member, BindingFlags.GetProperty, null, target, null)!;
    public static void Set(object target, string member, object value) => target.GetType().InvokeMember(member, BindingFlags.SetProperty, null, target, [value]);
    public static object? Call(object target, string member, params object[] args) => target.GetType().InvokeMember(member, BindingFlags.InvokeMethod, null, target, args);
    public static bool TrySet(object target, string member, object value)
    {
        try { Set(target, member, value); return true; }
        catch (Exception ex) when (ex is COMException or TargetInvocationException or MissingMethodException) { return false; }
    }
    public static int ErrorCode(Exception ex) => ex is TargetInvocationException { InnerException: { } inner } ? inner.HResult : ex.HResult;

}
