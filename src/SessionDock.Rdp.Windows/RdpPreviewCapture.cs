using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace SessionDock.Rdp.Windows;

/// <summary>Requests only the app-owned native control's rendering. Never reads screen pixels.</summary>
public static class RdpPreviewCapture
{
    public static byte[]? TryCapture(System.Windows.Forms.Control control)
    {
        if (!control.IsHandleCreated || control.IsDisposed || !control.Visible || control.ClientSize.Width < 32 || control.ClientSize.Height < 32) return null;
        int width = control.ClientSize.Width, height = control.ClientSize.Height;
        // Bound temporary bitmap memory. Huge or minimized surfaces retain the previous preview.
        if ((long)width * height > 16_777_216) return null;
        try
        {
            using var source = new Bitmap(width, height, PixelFormat.Format32bppRgb);
            using (var graphics = Graphics.FromImage(source))
            {
                var dc = graphics.GetHdc();
                try { if (!PrintWindow(control.Handle, dc, 1)) return null; } // PW_CLIENTONLY
                finally { graphics.ReleaseHdc(dc); }
            }
            return Encode(source);
        }
        catch (Exception ex) when (ex is ArgumentException or ExternalException or InvalidOperationException or OutOfMemoryException)
        { return null; }
    }

    // Also exercised with synthetic images by the no-network Windows probe.
    public static byte[]? Encode(Bitmap source)
    {
        var scale = Math.Min(1d, Math.Min(480d / source.Width, 300d / source.Height));
        using var small = new Bitmap(Math.Max(1, (int)(source.Width * scale)), Math.Max(1, (int)(source.Height * scale)), PixelFormat.Format24bppRgb);
        using (var graphics = Graphics.FromImage(small))
        {
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.DrawImage(source, 0, 0, small.Width, small.Height);
        }
        // Some ActiveX renderers report success but supply an empty/uniform bitmap.
        int first = small.GetPixel(0, 0).ToArgb(); bool varied = false;
        for (int y = 0; y < small.Height && !varied; y += 4)
            for (int x = 0; x < small.Width; x += 4)
                if (small.GetPixel(x, y).ToArgb() != first) { varied = true; break; }
        if (!varied) return null;
        using var stream = new MemoryStream(); small.Save(stream, ImageFormat.Png);
        return stream.ToArray();
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);
}
