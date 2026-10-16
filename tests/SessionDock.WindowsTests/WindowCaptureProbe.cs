using SessionDock.Rdp.Windows;

internal static class WindowCaptureProbe
{
    public static bool Run()
    {
        bool passed = false;
        using var window = new Form { Text = "Synthetic compositor capture test", Width = 400, Height = 280, StartPosition = FormStartPosition.Manual, Location = new Point(100, 100), ShowInTaskbar = false };
        using var pattern = new NoPrintPattern { Dock = DockStyle.Fill };
        window.Controls.Add(pattern);
        window.Shown += async (_, _) =>
        {
            try
            {
                using var capture = new WindowPreviewCapture(window.Handle);
                await Task.Delay(500);
                var image = await capture.TryCaptureAsync(pattern) ?? throw new InvalidOperationException("Compositor did not capture the synthetic surface.");
                CheckPixels(image);
                using var cover = new Form { BackColor = Color.Magenta, Width = 460, Height = 340, StartPosition = FormStartPosition.Manual, Location = new Point(80, 80), ShowInTaskbar = false };
                cover.Show(); window.Text = "Synthetic compositor updated while covered"; pattern.Refresh(); await Task.Delay(500);
                var covered = await capture.TryCaptureAsync(pattern) ?? throw new InvalidOperationException("Obscured compositor capture returned no image.");
                CheckPixels(covered);
                window.WindowState = FormWindowState.Minimized; await Task.Delay(150);
                if (await capture.TryCaptureAsync(pattern) is not null) throw new InvalidOperationException("Minimized capture was accepted.");
                Console.WriteLine("PASS: compositor captured a synthetic surface, cropped out window chrome, excluded an overlapping window, and rejected minimization.");
                passed = true;
            }
            catch (Exception ex) { Console.Error.WriteLine("FAIL: compositor probe: " + ex.GetType().Name + ": " + ex.Message); }
            finally { window.Close(); }
        };
        Application.Run(window); return passed;
    }
    private static void CheckPixels(byte[] png)
    {
        using var stream = new MemoryStream(png); using var bitmap = new Bitmap(stream);
        if (bitmap.Width > 480 || bitmap.Height > 300 || bitmap.GetPixel(20, 20).ToArgb() != Color.Blue.ToArgb() || bitmap.GetPixel(bitmap.Width - 20, 20).ToArgb() != Color.Gold.ToArgb())
            throw new InvalidOperationException("Compositor crop contains unexpected pixels.");
    }
    private sealed class NoPrintPattern : Control
    {
        protected override void OnPaint(PaintEventArgs e) { e.Graphics.Clear(Color.Blue); e.Graphics.FillRectangle(Brushes.Gold, Width / 2, 0, Width / 2, Height); }
        protected override void WndProc(ref Message message)
        {
            if (message.Msg is 0x317 or 0x318) { message.Result = IntPtr.Zero; return; } // WM_PRINT/WM_PRINTCLIENT deliberately unsupported
            base.WndProc(ref message);
        }
    }
}
