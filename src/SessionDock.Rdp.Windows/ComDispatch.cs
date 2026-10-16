using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Security;

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

    // Avoid creating an immutable managed string for passwords. Invoke copies the BSTR into the
    // control; the caller's temporary BSTR, VARIANT, and argument buffers are always zeroed/freed.
    public static void SetSecret(object target, string member, SecureString secret)
    {
        var dispatch = (IDispatchNative)target;
        Guid empty = Guid.Empty;
        int[] ids = new int[1];
        Marshal.ThrowExceptionForHR(dispatch.GetIDsOfNames(ref empty, [member], 1, 0, ids));
        IntPtr bstr = IntPtr.Zero, variant = IntPtr.Zero, named = IntPtr.Zero;
        int variantSize = IntPtr.Size == 8 ? 24 : 16;
        try
        {
            bstr = Marshal.SecureStringToBSTR(secret);
            variant = Marshal.AllocCoTaskMem(variantSize);
            for (int i = 0; i < variantSize; i++) Marshal.WriteByte(variant, i, 0);
            Marshal.WriteInt16(variant, 8); // VT_BSTR
            Marshal.WriteIntPtr(variant, 8, bstr);
            named = Marshal.AllocCoTaskMem(sizeof(int));
            Marshal.WriteInt32(named, -3); // DISPID_PROPERTYPUT
            var args = new DISPPARAMS { rgvarg = variant, rgdispidNamedArgs = named, cArgs = 1, cNamedArgs = 1 };
            Marshal.ThrowExceptionForHR(dispatch.Invoke(ids[0], ref empty, 0, 4, ref args, IntPtr.Zero, out _, out _));
        }
        finally
        {
            if (variant != IntPtr.Zero) { for (int i = 0; i < variantSize; i++) Marshal.WriteByte(variant, i, 0); Marshal.FreeCoTaskMem(variant); }
            if (named != IntPtr.Zero) Marshal.FreeCoTaskMem(named);
            if (bstr != IntPtr.Zero) Marshal.ZeroFreeBSTR(bstr);
        }
    }

    [ComImport, Guid("00020400-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDispatchNative
    {
        [PreserveSig] int GetTypeInfoCount(out uint count);
        [PreserveSig] int GetTypeInfo(uint index, uint lcid, out IntPtr info);
        [PreserveSig] int GetIDsOfNames(ref Guid iid, [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPWStr)] string[] names, uint count, uint lcid, [Out] int[] ids);
        [PreserveSig] int Invoke(int id, ref Guid iid, uint lcid, ushort flags, ref DISPPARAMS args, IntPtr result, out EXCEPINFO exception, out uint argumentError);
    }
}
