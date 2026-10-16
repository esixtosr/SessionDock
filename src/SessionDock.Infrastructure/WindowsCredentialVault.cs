using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security;

namespace SessionDock.Infrastructure;

/// <summary>Only app-owned generic credentials; never TERMSRV or other applications' entries.</summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsCredentialVault
{
    private const uint Generic = 1, LocalMachinePersistence = 2;
    private const int NotFound = 1168;
    private static string Target(Guid id)
    {
        if (id == Guid.Empty) throw new ArgumentException("A stable credential ID is required.", nameof(id));
        return $"SessionDock/credential/{id:D}";
    }

    public void Save(Guid id, string username, SecureString password)
    {
        ArgumentNullException.ThrowIfNull(password);
        if (password.Length == 0) throw new ArgumentException("Use Delete to deliberately clear a saved password.");
        if (password.Length > 1280) throw new ArgumentException("This password exceeds Windows Credential Manager's size limit.");
        var pointer = Marshal.SecureStringToCoTaskMemUnicode(password);
        try
        {
            var credential = new NativeCredential
            {
                Type = Generic, TargetName = Target(id), UserName = username,
                CredentialBlob = pointer, CredentialBlobSize = checked((uint)password.Length * 2),
                Persist = LocalMachinePersistence
            };
            if (!CredWriteW(ref credential, 0)) throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows could not save the credential.");
        }
        finally { Marshal.ZeroFreeCoTaskMemUnicode(pointer); }
    }

    public SecureString? Read(Guid id)
    {
        if (!CredReadW(Target(id), Generic, 0, out var pointer))
        {
            int error = Marshal.GetLastWin32Error();
            if (error == NotFound) return null;
            throw new Win32Exception(error, "Windows could not read the saved credential.");
        }
        NativeCredential credential = default;
        try
        {
            credential = Marshal.PtrToStructure<NativeCredential>(pointer);
            if (credential.CredentialBlobSize % 2 != 0 || credential.CredentialBlobSize > 2560)
                throw new InvalidDataException("The saved credential has an invalid format.");
            var password = new SecureString();
            try
            {
                for (int offset = 0; offset < credential.CredentialBlobSize; offset += 2)
                    password.AppendChar((char)Marshal.ReadInt16(credential.CredentialBlob, offset));
                password.MakeReadOnly();
                return password;
            }
            catch { password.Dispose(); throw; }
        }
        finally
        {
            // The returned structure and blob belong to the caller until CredFree.
            if (credential.CredentialBlob != IntPtr.Zero)
                for (int i = 0; i < credential.CredentialBlobSize; i++) Marshal.WriteByte(credential.CredentialBlob, i, 0);
            CredFree(pointer);
        }
    }

    public void Delete(Guid id)
    {
        if (CredDeleteW(Target(id), Generic, 0)) return;
        int error = Marshal.GetLastWin32Error();
        if (error != NotFound) throw new Win32Exception(error, "Windows could not remove the credential.");
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NativeCredential
    {
        public uint Flags, Type;
        [MarshalAs(UnmanagedType.LPWStr)] public string? TargetName;
        [MarshalAs(UnmanagedType.LPWStr)] public string? Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist, AttributeCount;
        public IntPtr Attributes;
        [MarshalAs(UnmanagedType.LPWStr)] public string? TargetAlias;
        [MarshalAs(UnmanagedType.LPWStr)] public string? UserName;
    }
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredWriteW(ref NativeCredential credential, uint flags);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredReadW(string target, uint type, uint flags, out IntPtr credential);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredDeleteW(string target, uint type, uint flags);
    [DllImport("advapi32.dll", ExactSpelling = true)]
    private static extern void CredFree(IntPtr buffer);
}
