using SessionDock.Infrastructure;
using SessionDock.Rdp.Windows;

internal static class PreviewProbe
{
    public static bool Run()
    {
        string directory = Path.Combine(Path.GetTempPath(), "SessionDockPreviewProbe-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            using var source = new Form { Width = 360, Height = 240, ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(100, 100), Text = "Synthetic preview test" };
            using var pattern = new Pattern { Dock = DockStyle.Fill };
            source.Controls.Add(pattern); source.Show(); source.Refresh(); pattern.Refresh();
            var png = RdpPreviewCapture.TryCapture(pattern) ?? throw new InvalidOperationException("Synthetic control capture failed.");
            using var cover = new Form { Width = 420, Height = 300, ShowInTaskbar = false, BackColor = Color.Magenta, StartPosition = FormStartPosition.Manual, Location = new Point(80, 80) };
            cover.Show(); cover.Refresh();
            var covered = RdpPreviewCapture.TryCapture(pattern) ?? throw new InvalidOperationException("Obscured control capture failed.");
            if (!png.SequenceEqual(covered)) throw new InvalidOperationException("Capture changed under an overlapping window.");
            using (var image = new Bitmap(new MemoryStream(png)))
            {
                if (image.Width > 480 || image.Height > 300 || image.GetPixel(20, 20).ToArgb() != Color.Blue.ToArgb()) throw new InvalidOperationException("Unexpected synthetic capture pixels or bounds.");
            }
            using var blank = new Bitmap(320, 180);
            if (RdpPreviewCapture.Encode(blank) is not null) throw new InvalidOperationException("Empty frame was accepted.");
            source.Hide();
            if (RdpPreviewCapture.TryCapture(pattern) is not null) throw new InvalidOperationException("Hidden surface was captured.");
            var store = new PreviewStore(directory); var id = Guid.NewGuid(); var other = Guid.NewGuid();
            var at = DateTimeOffset.UtcNow;
            store.Save(id, new(at, png)); store.Save(other, new(at.AddSeconds(1), covered));
            var reopened = new PreviewStore(directory);
            var loaded = reopened.Load(id) ?? throw new InvalidOperationException("Protected preview did not reload.");
            if (loaded.CapturedAt != at || !loaded.Png.SequenceEqual(png)) throw new InvalidOperationException("Preview did not round-trip.");
            string file = Path.Combine(directory, "thumbnails", id.ToString("N") + ".preview");
            if (File.ReadAllBytes(file).SequenceEqual(png)) throw new InvalidOperationException("Plain image was stored.");
            store.Delete(id);
            if (store.Load(id) is not null || store.Load(other) is null) throw new InvalidOperationException("Per-profile deletion affected the wrong image.");
            string otherFile = Path.Combine(directory, "thumbnails", other.ToString("N") + ".preview");
            File.WriteAllBytes(otherFile, [1, 2, 3]);
            if (store.Load(other) is not null) throw new InvalidOperationException("Corrupt cache was accepted.");
            store.Clear();
            if (Directory.EnumerateFiles(Path.Combine(directory, "thumbnails")).Any()) throw new InvalidOperationException("Clear left cached files.");
            Console.WriteLine("PASS: synthetic preview bounds, blank/hidden rejection, obscured-window isolation, DPAPI reload, separate profile deletion, corrupt-cache fallback and clear.");
            return true;
        }
        catch (Exception ex) { Console.Error.WriteLine("FAIL: synthetic preview probe: " + ex.GetType().Name + ": " + ex.Message); return false; }
        finally { Directory.Delete(directory, recursive: true); }
    }
    private sealed class Pattern : Control
    {
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Color.Blue);
            e.Graphics.FillRectangle(Brushes.Gold, Width / 2, 0, Width / 2, Height);
        }
    }
}
