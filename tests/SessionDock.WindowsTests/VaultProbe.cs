using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using SessionDock.Infrastructure;

internal sealed record VaultProbeResult(bool RoundTripPassed, bool DeletionVerified, int? ErrorCode, int? CleanupErrorCode);

internal static class VaultProbe
{
    // Writes one unpredictable, app-owned generic credential, then always removes it.
    // The secret, temporary target ID, and values never enter console output or reports.
    public static VaultProbeResult Run()
    {
        var vault = new WindowsCredentialVault();
        var id = Guid.NewGuid();
        using var secret = new SecureString();
        for (int i = 0; i < 64; i++) secret.AppendChar((char)RandomNumberGenerator.GetInt32(33, 127));
        secret.MakeReadOnly();
        bool matches = false, deleted = false;
        int? error = null, cleanupError = null;
        try
        {
            vault.Save(id, "SessionDock temporary validation", secret);
            using var read = vault.Read(id);
            matches = read is not null && Equal(secret, read);
        }
        catch (Exception ex) { error = ex.HResult; }
        finally
        {
            try
            {
                vault.Delete(id);
                using var remaining = vault.Read(id);
                deleted = remaining is null;
            }
            catch (Exception ex) { cleanupError = ex.HResult; }
        }
        return new(matches, deleted, error, cleanupError);
    }
    private static bool Equal(SecureString first, SecureString second)
    {
        if (first.Length != second.Length) return false;
        IntPtr left = IntPtr.Zero, right = IntPtr.Zero;
        try
        {
            left = Marshal.SecureStringToCoTaskMemUnicode(first);
            right = Marshal.SecureStringToCoTaskMemUnicode(second);
            int difference = 0;
            for (int i = 0; i < first.Length * 2; i++) difference |= Marshal.ReadByte(left, i) ^ Marshal.ReadByte(right, i);
            return difference == 0;
        }
        finally
        {
            if (left != IntPtr.Zero) Marshal.ZeroFreeCoTaskMemUnicode(left);
            if (right != IntPtr.Zero) Marshal.ZeroFreeCoTaskMemUnicode(right);
        }
    }
}
