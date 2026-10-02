// Records Shturmap's own window for the website's hero clip, and nothing else: Windows.Graphics.Capture on that one
// window (found by its process), without the mouse cursor, cropped to the window's client area. Frames go to ffmpeg
// at a steady frame rate as a lossless video; the wall-clock time of the first frame is written next to it, so the
// demo's log times can be turned into cut points (tools\fake-raid.ps1 -Demo does that).
// Usage: record-window --seconds 16 --out capture.mkv [--fps 30] [--ffmpeg <ffmpeg.exe>] [--process Shturmap]
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Graphics.Imaging;

string Arg(string name, string fallback) => Array.IndexOf(args, name) is var i and >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
var seconds = double.Parse(Arg("--seconds", "16"), CultureInfo.InvariantCulture);
var fps = int.Parse(Arg("--fps", "30"), CultureInfo.InvariantCulture);
var output = Path.GetFullPath(Arg("--out", "capture.mkv"));
var ffmpeg = Arg("--ffmpeg", "ffmpeg");
var processName = Arg("--process", "Shturmap");

Native.SetProcessDpiAwarenessContext(new IntPtr(-4)); // per-monitor v2: window metrics in physical pixels
if (!GraphicsCaptureSession.IsSupported())
    return Fail("Windows.Graphics.Capture isn't supported on this system.");

// Shturmap's main window, and only that.
var hwnd = IntPtr.Zero;
for (var wait = 0; wait < 100 && hwnd == IntPtr.Zero; wait++)
{
    foreach (var p in Process.GetProcessesByName(processName))
    {
        if (p.MainWindowHandle != IntPtr.Zero && p.MainWindowTitle.StartsWith("Shturmap", StringComparison.Ordinal))
            hwnd = p.MainWindowHandle;
    }
    if (hwnd == IntPtr.Zero)
        Thread.Sleep(100);
}
if (hwnd == IntPtr.Zero)
    return Fail($"No {processName} window found.");

// The capture is the window's visible frame; the client area sits inside it (below the title bar).
Native.DwmGetWindowAttribute(hwnd, 9 /* DWMWA_EXTENDED_FRAME_BOUNDS */, out var frame, Marshal.SizeOf<Native.Rect>());
Native.GetClientRect(hwnd, out var client);
var origin = new Native.PointInt();
Native.ClientToScreen(hwnd, ref origin);
int cropX = origin.X - frame.Left, cropY = origin.Y - frame.Top, width = client.Right & ~1, height = client.Bottom & ~1;
Console.WriteLine($"Recording window 0x{hwnd:X} client area {width}x{height} at +{cropX},+{cropY} for {seconds} s at {fps} fps");

var device = Native.CreateDevice();
var item = Native.CaptureItemForWindow(hwnd);
using var pool = Direct3D11CaptureFramePool.CreateFreeThreaded(device, DirectXPixelFormat.B8G8R8A8UIntNormalized, 2, item.Size);
using var session = pool.CreateCaptureSession(item);
session.IsCursorCaptureEnabled = false;
try
{
    session.IsBorderRequired = false; // Windows 11 draws a frame around a window being captured; not needed here
}
catch (Exception)
{
}

byte[]? latest = null;
var gate = new object();
pool.FrameArrived += (sender, _) =>
{
    using var captured = sender.TryGetNextFrame();
    if (captured is null)
        return;
    using var bitmap = SoftwareBitmap.CreateCopyFromSurfaceAsync(captured.Surface, BitmapAlphaMode.Premultiplied).AsTask().GetAwaiter().GetResult();
    var full = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4];
    bitmap.CopyToBuffer(full.AsBuffer());
    if (cropX + width > bitmap.PixelWidth || cropY + height > bitmap.PixelHeight)
        return; // the window changed size; keep the last good frame
    var cropped = new byte[width * height * 4];
    for (var y = 0; y < height; y++)
        Buffer.BlockCopy(full, ((cropY + y) * bitmap.PixelWidth + cropX) * 4, cropped, y * width * 4, width * 4);
    lock (gate)
        latest = cropped;
};
session.StartCapture();

// A frame arrives only when the window changes; the output repeats the newest one at a steady rate.
var started = Stopwatch.StartNew();
while (Volatile.Read(ref latest) is null && started.ElapsedMilliseconds < 5000)
    Thread.Sleep(5);
if (latest is null)
    return Fail("No frame arrived from the window.");

using var encoder = Process.Start(new ProcessStartInfo(ffmpeg,
    $"-hide_banner -loglevel error -y -f rawvideo -pix_fmt bgra -s {width}x{height} -r {fps} -i - -c:v libx264rgb -preset ultrafast -qp 0 \"{output}\"")
{
    RedirectStandardInput = true,
    UseShellExecute = false,
})!;
var input = encoder.StandardInput.BaseStream;
var firstFrame = DateTime.Now;
var clock = Stopwatch.StartNew();
var total = (int)Math.Round(seconds * fps);
for (var i = 0; i < total; i++)
{
    var due = i * 1000.0 / fps;
    while (clock.Elapsed.TotalMilliseconds < due)
        Thread.Sleep(1);
    byte[] frameBytes;
    lock (gate)
        frameBytes = latest!;
    input.Write(frameBytes, 0, frameBytes.Length);
}
input.Close();
encoder.WaitForExit();
session.Dispose();
File.WriteAllText(Path.ChangeExtension(output, ".start.txt"), firstFrame.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture));
Console.WriteLine($"Wrote {output} ({total} frames, first at {firstFrame:HH:mm:ss.fff})");
return encoder.ExitCode;

static int Fail(string message)
{
    Console.Error.WriteLine(message);
    return 1;
}

static class Native
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Rect
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PointInt
    {
        public int X, Y;
    }

    [DllImport("user32.dll")]
    public static extern bool SetProcessDpiAwarenessContext(IntPtr context);

    [DllImport("user32.dll")]
    public static extern bool GetClientRect(IntPtr hwnd, out Rect rect);

    [DllImport("user32.dll")]
    public static extern bool ClientToScreen(IntPtr hwnd, ref PointInt point);

    [DllImport("dwmapi.dll")]
    public static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out Rect value, int size);

    [DllImport("d3d11.dll", ExactSpelling = true)]
    private static extern int D3D11CreateDevice(IntPtr adapter, int driverType, IntPtr software, uint flags, IntPtr featureLevels,
        uint featureLevelCount, uint sdkVersion, out IntPtr device, out int featureLevel, out IntPtr context);

    [DllImport("d3d11.dll", ExactSpelling = true)]
    private static extern int CreateDirect3D11DeviceFromDXGIDevice(IntPtr dxgiDevice, out IntPtr graphicsDevice);

    [ComImport, Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IGraphicsCaptureItemInterop
    {
        IntPtr CreateForWindow(IntPtr window, ref Guid iid);

        IntPtr CreateForMonitor(IntPtr monitor, ref Guid iid);
    }

    /// <summary>A Direct3D device for the frame pool (hardware, BGRA).</summary>
    public static IDirect3DDevice CreateDevice()
    {
        Marshal.ThrowExceptionForHR(D3D11CreateDevice(IntPtr.Zero, 1, IntPtr.Zero, 0x20, IntPtr.Zero, 0, 7, out var d3d, out _, out var context));
        var dxgiId = new Guid("54ec77fa-1377-44e6-8c32-88fd5f44c84c");
        Marshal.ThrowExceptionForHR(Marshal.QueryInterface(d3d, in dxgiId, out var dxgi));
        Marshal.ThrowExceptionForHR(CreateDirect3D11DeviceFromDXGIDevice(dxgi, out var inspectable));
        var device = WinRT.MarshalInterface<IDirect3DDevice>.FromAbi(inspectable);
        Marshal.Release(inspectable);
        Marshal.Release(dxgi);
        Marshal.Release(context);
        Marshal.Release(d3d);
        return device;
    }

    /// <summary>The capture item for one window.</summary>
    public static GraphicsCaptureItem CaptureItemForWindow(IntPtr hwnd)
    {
        var factory = WinRT.ActivationFactory.Get("Windows.Graphics.Capture.GraphicsCaptureItem");
        var interop = factory.AsInterface<IGraphicsCaptureItemInterop>();
        var itemId = new Guid("79C3F95B-31F7-4EC2-A464-632EF5D30760");
        var pointer = interop.CreateForWindow(hwnd, ref itemId);
        var item = GraphicsCaptureItem.FromAbi(pointer);
        Marshal.Release(pointer);
        return item;
    }
}
