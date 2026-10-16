using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace SessionDock.Infrastructure;

public sealed record SavedPreview(DateTimeOffset CapturedAt, byte[] Png);

/// <summary>Optional, bounded, per-user DPAPI cache. No plaintext image files or export payloads.</summary>
public sealed class PreviewStore(string dataDirectory)
{
    private const int MaximumBytes = 1_048_576;
    private readonly string directory = Path.Combine(dataDirectory, "thumbnails");
    private string FilePath(Guid id) => Path.Combine(directory, id.ToString("N") + ".preview");

    public void Save(Guid id, SavedPreview preview)
    {
        if (id == Guid.Empty || preview.Png.Length is < 8 or > MaximumBytes) throw new ArgumentException("Invalid preview.");
        byte[] plain;
        using (var stream = new MemoryStream())
        {
            using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true))
            { writer.Write(1); writer.Write(preview.CapturedAt.UtcTicks); writer.Write(preview.Png.Length); writer.Write(preview.Png); }
            plain = stream.ToArray();
        }
        byte[] encrypted;
        try { encrypted = Protect(plain, decrypt: false); }
        finally { CryptographicOperations.ZeroMemory(plain); }
        Directory.CreateDirectory(directory);
        string temporary = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".tmp");
        try { File.WriteAllBytes(temporary, encrypted); File.Move(temporary, FilePath(id), overwrite: true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public SavedPreview? Load(Guid id)
    {
        try
        {
            string path = FilePath(id);
            if (!File.Exists(path) || new FileInfo(path).Length > MaximumBytes + 8192) return null;
            byte[] plain = Protect(File.ReadAllBytes(path), decrypt: true);
            try
            {
                using var reader = new BinaryReader(new MemoryStream(plain));
                if (reader.ReadInt32() != 1) return null;
                var at = new DateTimeOffset(reader.ReadInt64(), TimeSpan.Zero);
                int length = reader.ReadInt32();
                if (length is < 8 or > MaximumBytes || reader.BaseStream.Length - reader.BaseStream.Position != length) return null;
                return new(at, reader.ReadBytes(length));
            }
            finally { CryptographicOperations.ZeroMemory(plain); }
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or Win32Exception or ArgumentException)
        { return null; }
    }
    public void Delete(Guid id) { var path = FilePath(id); if (File.Exists(path)) File.Delete(path); }
    public void Clear()
    {
        if (!Directory.Exists(directory)) return;
        // Only files directly inside this app-owned cache; never follow directories recursively.
        foreach (var path in Directory.EnumerateFiles(directory)) File.Delete(path);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Blob { public int Length; public IntPtr Data; }
    private static byte[] Protect(byte[] bytes, bool decrypt)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Preview encryption requires Windows.");
        var input = new Blob { Length = bytes.Length, Data = Marshal.AllocHGlobal(bytes.Length) };
        Blob output = default;
        try
        {
            Marshal.Copy(bytes, 0, input.Data, bytes.Length);
            bool ok = decrypt
                ? CryptUnprotectData(ref input, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out output)
                : CryptProtectData(ref input, null, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out output);
            if (!ok) throw new Win32Exception(Marshal.GetLastWin32Error());
            if (output.Length is < 1 or > MaximumBytes + 8192) throw new InvalidDataException("Invalid protected preview size.");
            var result = new byte[output.Length]; Marshal.Copy(output.Data, result, 0, result.Length); return result;
        }
        finally
        {
            // Native buffers can contain an unencrypted image. Zero before releasing them.
            for (int i = 0; i < input.Length; i++) Marshal.WriteByte(input.Data, i, 0);
            Marshal.FreeHGlobal(input.Data);
            if (output.Data != IntPtr.Zero)
            {
                for (int i = 0; i < output.Length; i++) Marshal.WriteByte(output.Data, i, 0);
                LocalFree(output.Data);
            }
        }
    }
    [DllImport("crypt32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptProtectData(ref Blob input, string? description, IntPtr entropy, IntPtr reserved, IntPtr prompt, uint flags, out Blob output);
    [DllImport("crypt32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptUnprotectData(ref Blob input, IntPtr description, IntPtr entropy, IntPtr reserved, IntPtr prompt, uint flags, out Blob output);
    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr memory);
}
