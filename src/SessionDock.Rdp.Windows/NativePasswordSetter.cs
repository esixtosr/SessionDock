using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Security;

namespace SessionDock.Rdp.Windows;

/// <summary>Calls documented BSTR setters using the installed type library's verified native ABI.</summary>
internal static class NativePasswordSetter
{
    private static readonly Lazy<Setter> Desktop = new(() => Resolve(
        new Guid("C1E6743A-41C1-4A74-832A-0DD06C1C7A0E"), "ClearTextPassword"));
    private static readonly Lazy<Setter> Gateway = new(() => Resolve(
        new Guid("67341688-D606-4C73-A5D2-2E0489009319"), "GatewayPassword"));

    public static void SetDesktop(object client, SecureString password) => Desktop.Value.Invoke(client, password);
    public static void SetGateway(object transport, SecureString password) => Gateway.Value.Invoke(transport, password);

    private sealed record Setter(Guid InterfaceId, int VtableOffset)
    {
        public void Invoke(object target, SecureString password)
        {
            IntPtr unknown = IntPtr.Zero, instance = IntPtr.Zero, bstr = IntPtr.Zero;
            try
            {
                unknown = Marshal.GetIUnknownForObject(target);
                var iid = InterfaceId;
                Marshal.ThrowExceptionForHR(Marshal.QueryInterface(unknown, in iid, out instance));
                var method = Marshal.ReadIntPtr(Marshal.ReadIntPtr(instance), VtableOffset);
                if (method == IntPtr.Zero) throw new NotSupportedException("The installed password setter has no native entry point.");
                var setter = Marshal.GetDelegateForFunctionPointer<BstrPropertyPut>(method);
                bstr = Marshal.SecureStringToBSTR(password);
                Marshal.ThrowExceptionForHR(setter(instance, bstr));
            }
            finally
            {
                if (bstr != IntPtr.Zero) Marshal.ZeroFreeBSTR(bstr);
                if (instance != IntPtr.Zero) Marshal.Release(instance);
                if (unknown != IntPtr.Zero) Marshal.Release(unknown);
                GC.KeepAlive(target);
            }
        }
    }

    private static Setter Resolve(Guid interfaceId, string member)
    {
        if (!OperatingSystem.IsWindows() || IntPtr.Size != 8)
            throw new NotSupportedException("The password bridge requires 64-bit Windows.");
        ITypeLib? library = null;
        ITypeInfo? original = null, native = null;
        try
        {
            // No downloaded typelib or user-supplied path. REGKIND_NONE avoids registry changes.
            Marshal.ThrowExceptionForHR(LoadTypeLibEx(Path.Combine(Environment.SystemDirectory, "mstscax.dll"), 2, out library));
            library.GetTypeInfoOfGuid(ref interfaceId, out original);
            var attribute = ReadAttributes(original);
            native = original;
            if (attribute.typekind == TYPEKIND.TKIND_DISPATCH && (attribute.wTypeFlags & TYPEFLAGS.TYPEFLAG_FDUAL) != 0)
            {
                original.GetRefTypeOfImplType(-1, out int reference);
                original.GetRefTypeInfo(reference, out native);
                attribute = ReadAttributes(native);
            }
            if (attribute.guid != interfaceId || attribute.typekind != TYPEKIND.TKIND_INTERFACE)
                throw new NotSupportedException("The installed password interface does not expose the expected native ABI.");
            for (int index = 0; index < attribute.cFuncs; index++)
            {
                native.GetFuncDesc(index, out var descriptor);
                try
                {
                    var function = Marshal.PtrToStructure<FUNCDESC>(descriptor);
                    if (function.invkind != INVOKEKIND.INVOKE_PROPERTYPUT) continue;
                    string[] names = new string[1];
                    native.GetNames(function.memid, names, 1, out int count);
                    if (count != 1 || !string.Equals(names[0], member, StringComparison.Ordinal)) continue;
                    if (function.cParams != 1 || function.cParamsOpt != 0 ||
                        function.funckind is not (FUNCKIND.FUNC_PUREVIRTUAL or FUNCKIND.FUNC_VIRTUAL) ||
                        function.callconv != CALLCONV.CC_STDCALL ||
                        function.elemdescFunc.tdesc.vt != (short)VarEnum.VT_HRESULT ||
                        function.oVft < 3 * IntPtr.Size || function.oVft % IntPtr.Size != 0 ||
                        function.oVft >= attribute.cbSizeVft || function.lprgelemdescParam == IntPtr.Zero)
                        throw new NotSupportedException("The installed password setter has an unexpected native signature.");
                    var parameter = Marshal.PtrToStructure<ELEMDESC>(function.lprgelemdescParam);
                    if (parameter.tdesc.vt != (short)VarEnum.VT_BSTR ||
                        parameter.desc.paramdesc.wParamFlags != PARAMFLAG.PARAMFLAG_FIN)
                        throw new NotSupportedException("The installed password setter does not accept one input BSTR.");
                    return new(interfaceId, function.oVft);
                }
                finally { native.ReleaseFuncDesc(descriptor); }
            }
            throw new NotSupportedException("The installed password interface is missing its required setter.");
        }
        finally
        {
            // Release only short-lived metadata RCWs. AxHost still owns the client RCW.
            if (native is not null && !ReferenceEquals(native, original)) Marshal.ReleaseComObject(native);
            if (original is not null) Marshal.ReleaseComObject(original);
            if (library is not null) Marshal.ReleaseComObject(library);
        }
    }

    private static TYPEATTR ReadAttributes(ITypeInfo info)
    {
        info.GetTypeAttr(out var pointer);
        try { return Marshal.PtrToStructure<TYPEATTR>(pointer); }
        finally { info.ReleaseTypeAttr(pointer); }
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int BstrPropertyPut(IntPtr instance, IntPtr password);

    [DllImport("oleaut32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    private static extern int LoadTypeLibEx(string fileName, int registrationKind, out ITypeLib typeLibrary);
}
