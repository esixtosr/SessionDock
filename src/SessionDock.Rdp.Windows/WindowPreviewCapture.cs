using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Windows.Graphics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace SessionDock.Rdp.Windows;

/// <summary>Compositor capture of this process's session window, cropped to its RDP child only.
/// No monitor capture or screen-pixel fallback. Dispose immediately when opt-in/login ends.</summary>
public sealed class WindowPreviewCapture : IDisposable
{
    private readonly IntPtr window;
    private readonly IDirect3DDevice device;
    private readonly Direct3D11CaptureFramePool pool;
    private readonly GraphicsCaptureSession session;
    private readonly object gate = new();
    private Direct3D11CaptureFrame? latest;
    private SizeInt32 size;
    private bool disposed;

    public WindowPreviewCapture(IntPtr hwnd)
    {
        GetWindowThreadProcessId(hwnd, out uint process);
        if (hwnd == IntPtr.Zero || process != Environment.ProcessId || GetAncestor(hwnd, 2) != hwnd)
            throw new ArgumentException("Capture requires an app-owned top-level window.");
        if (!GraphicsCaptureSession.IsSupported()) throw new NotSupportedException("Windows window capture is unavailable.");
        window = hwnd;
        var item = CreateItem(hwnd);
        size = item.Size;
        if (!Valid(size)) throw new NotSupportedException("Window capture size is unavailable.");
        device = CreateDevice();
        try
        {
            pool = Direct3D11CaptureFramePool.CreateFreeThreaded(device, DirectXPixelFormat.B8G8R8A8UIntNormalized, 2, size);
            try
            {
                session = pool.CreateCaptureSession(item);
                session.IsCursorCaptureEnabled = false;
                pool.FrameArrived += OnFrame;
                session.StartCapture(); // Windows retains its visible capture indicator.
            }
            catch { pool.Dispose(); throw; }
        }
        catch { device.Dispose(); throw; }
    }
    private static bool Valid(SizeInt32 value) => value.Width > 0 && value.Height > 0 && (long)value.Width * value.Height <= 16_777_216;
    private void OnFrame(Direct3D11CaptureFramePool sender, object args)
    {
        lock (gate)
        {
            if (disposed) return;
            try
            {
                var frame = sender.TryGetNextFrame();
                if (frame is null) return;
                if (frame.ContentSize.Width != size.Width || frame.ContentSize.Height != size.Height)
                {
                    size = frame.ContentSize; frame.Dispose(); latest?.Dispose(); latest = null;
                    if (Valid(size)) sender.Recreate(device, DirectXPixelFormat.B8G8R8A8UIntNormalized, 2, size);
                    return;
                }
                latest?.Dispose(); latest = frame;
            }
            catch (Exception ex) when (ex is COMException or ObjectDisposedException) { }
        }
    }
    public async Task<byte[]?> TryCaptureAsync(System.Windows.Forms.Control remote)
    {
        if (remote.IsDisposed || !remote.Visible || !remote.IsHandleCreated || IsIconic(window) || GetAncestor(remote.Handle, 2) != window) return null;
        // Only map known compositor bounds. Moved/resized/ambiguous geometry is discarded.
        Direct3D11CaptureFrame? frame;
        lock (gate) { if (disposed) return null; frame = latest; latest = null; }
        if (frame is null) return null;
        using (frame)
        {
            var content = frame.ContentSize;
            if (!Valid(content)) return null;
            if (!TryWindowBounds(content, out var bounds)) return null;
            var origin = remote.PointToScreen(Point.Empty);
            var crop = new Rectangle(origin.X - bounds.Left, origin.Y - bounds.Top, remote.ClientSize.Width, remote.ClientSize.Height);
            if (crop.Width < 32 || crop.Height < 32 || !new Rectangle(0, 0, content.Width, content.Height).Contains(crop)) return null;
            using var software = await SoftwareBitmap.CreateCopyFromSurfaceAsync(frame.Surface, BitmapAlphaMode.Ignore);
            if (disposed || !TryWindowBounds(content, out var after) || !bounds.Equals(after) || origin != remote.PointToScreen(Point.Empty) || remote.ClientSize != crop.Size) return null;
            using var converted = SoftwareBitmap.Convert(software, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore);
            uint length = checked((uint)(converted.PixelWidth * converted.PixelHeight * 4));
            var buffer = new global::Windows.Storage.Streams.Buffer(length);
            converted.CopyToBuffer(buffer);
            byte[] pixels = new byte[length];
            using (var reader = DataReader.FromBuffer(buffer)) reader.ReadBytes(pixels);
            try
            {
                using var full = new Bitmap(converted.PixelWidth, converted.PixelHeight, PixelFormat.Format32bppRgb);
                var data = full.LockBits(new Rectangle(0, 0, full.Width, full.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppRgb);
                try
                {
                    for (int y = 0; y < full.Height; y++) Marshal.Copy(pixels, y * full.Width * 4, data.Scan0 + y * data.Stride, full.Width * 4);
                }
                finally { full.UnlockBits(data); }
                using var cropped = full.Clone(crop, PixelFormat.Format32bppRgb);
                return RdpPreviewCapture.Encode(cropped);
            }
            finally { CryptographicOperations.ZeroMemory(pixels); }
        }
    }
    private bool TryWindowBounds(SizeInt32 content, out Rect bounds)
    {
        // WGC excludes compositor shadows; some themes include invisible resize borders.
        if (DwmGetWindowAttribute(window, 9, out bounds, Marshal.SizeOf<Rect>()) == 0 && bounds.Width == content.Width && bounds.Height == content.Height) return true;
        return GetWindowRect(window, out bounds) && bounds.Width == content.Width && bounds.Height == content.Height;
    }
    public void Dispose()
    {
        lock (gate) { if (disposed) return; disposed = true; latest?.Dispose(); latest = null; }
        try { pool.FrameArrived -= OnFrame; session.Dispose(); }
        finally { try { pool.Dispose(); } finally { device.Dispose(); } }
    }
    private static GraphicsCaptureItem CreateItem(IntPtr hwnd)
    {
        // Microsoft windows.graphics.capture.interop.h; no RDP COM members or guessed slots.
        Guid factoryId = typeof(ICaptureInterop).GUID;
        const string className = "Windows.Graphics.Capture.GraphicsCaptureItem";
        Marshal.ThrowExceptionForHR(WindowsCreateString(className, className.Length, out var name));
        IntPtr factory = IntPtr.Zero, item = IntPtr.Zero;
        try
        {
            Marshal.ThrowExceptionForHR(RoGetActivationFactory(name, ref factoryId, out factory));
            var interop = (ICaptureInterop)Marshal.GetObjectForIUnknown(factory);
            try
            {
                Guid itemId = new("79C3F95B-31F7-4EC2-A464-632EF5D30760");
                Marshal.ThrowExceptionForHR(interop.CreateForWindow(hwnd, ref itemId, out item));
                return WinRT.MarshalInterface<GraphicsCaptureItem>.FromAbi(item);
            }
            finally { Marshal.ReleaseComObject(interop); }
        }
        finally { if (item != IntPtr.Zero) Marshal.Release(item); if (factory != IntPtr.Zero) Marshal.Release(factory); WindowsDeleteString(name); }
    }
    private static IDirect3DDevice CreateDevice()
    {
        IntPtr native = IntPtr.Zero, context = IntPtr.Zero, dxgi = IntPtr.Zero, inspectable = IntPtr.Zero;
        try
        {
            int result = D3D11CreateDevice(IntPtr.Zero, 1, IntPtr.Zero, 0x20, IntPtr.Zero, 0, 7, out native, out _, out context);
            if (result < 0)
            {
                if (context != IntPtr.Zero) { Marshal.Release(context); context = IntPtr.Zero; }
                if (native != IntPtr.Zero) { Marshal.Release(native); native = IntPtr.Zero; }
                result = D3D11CreateDevice(IntPtr.Zero, 5, IntPtr.Zero, 0x20, IntPtr.Zero, 0, 7, out native, out _, out context); // WARP for hosts without hardware D3D
            }
            Marshal.ThrowExceptionForHR(result);
            Guid iid = new("54EC77FA-1377-44E6-8C32-88FD5F44C84C");
            Marshal.ThrowExceptionForHR(Marshal.QueryInterface(native, in iid, out dxgi));
            Marshal.ThrowExceptionForHR(CreateDirect3D11DeviceFromDXGIDevice(dxgi, out inspectable));
            return WinRT.MarshalInterface<IDirect3DDevice>.FromAbi(inspectable);
        }
        finally
        {
            if (inspectable != IntPtr.Zero) Marshal.Release(inspectable);
            if (dxgi != IntPtr.Zero) Marshal.Release(dxgi);
            if (context != IntPtr.Zero) Marshal.Release(context);
            if (native != IntPtr.Zero) Marshal.Release(native);
        }
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct Rect { public int Left, Top, Right, Bottom; public readonly int Width => Right - Left; public readonly int Height => Bottom - Top; }
    [ComImport, Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ICaptureInterop { [PreserveSig] int CreateForWindow(IntPtr hwnd, ref Guid iid, out IntPtr item); }
    [DllImport("combase.dll", CharSet = CharSet.Unicode)] private static extern int WindowsCreateString(string value, int length, out IntPtr result);
    [DllImport("combase.dll")] private static extern int WindowsDeleteString(IntPtr value);
    [DllImport("combase.dll")] private static extern int RoGetActivationFactory(IntPtr name, ref Guid iid, out IntPtr factory);
    [DllImport("d3d11.dll")] private static extern int D3D11CreateDevice(IntPtr adapter, int driver, IntPtr software, uint flags, IntPtr levels, uint count, uint sdk, out IntPtr device, out int level, out IntPtr context);
    [DllImport("d3d11.dll")] private static extern int CreateDirect3D11DeviceFromDXGIDevice(IntPtr device, out IntPtr result);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint process);
    [DllImport("user32.dll")] private static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsIconic(IntPtr hwnd);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out Rect rect, int size);
}
