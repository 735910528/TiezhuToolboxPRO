using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Graphics.Imaging;
using WinRT;

namespace TiezhuToolbox.Modules.Capture;

/// <summary>
/// 后台截图：优先 PrintWindow（含 PW_RENDERFULLCONTENT），失败再走 Windows Graphics Capture。
/// 不把游戏窗口拉到前台，也不依赖屏幕上当前露出的像素。
/// </summary>
internal static class WindowBackgroundCapture
{
    private const uint PwRenderFullContent = 2;
    private static readonly object Sync = new();
    private static IDirect3DDevice? _d3dDevice;
    private static CachedWgc? _wgc;

    public static Bitmap Capture(IntPtr mainHwnd, IntPtr captureHwnd, GameWindowHelper.Point screenOrigin, int width, int height)
    {
        GameWindowHelper.EnsureShownWithoutActivate(mainHwnd);
        if (width < 16 || height < 16)
            throw new InvalidOperationException("游戏窗口客户区过小，请确认窗口未最小化且分辨率正常");

        var printed = TryPrintWindow(captureHwnd, width, height)
                      ?? (captureHwnd != mainHwnd ? TryPrintWindow(mainHwnd, width, height) : null);
        if (printed != null && !IsMostlyBlack(printed))
            return printed;
        printed?.Dispose();

        var captured = TryCaptureWithGraphicsCapture(mainHwnd, screenOrigin, width, height);
        if (captured != null)
            return captured;

        throw new InvalidOperationException(
            "后台截图失败。请保持游戏窗口化、不要最小化；也可改回「前台」模式。");
    }

    private static Bitmap? TryPrintWindow(IntPtr hwnd, int width, int height)
    {
        foreach (var flags in new uint[] { PwRenderFullContent, 0 })
        {
            var bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
            using var graphics = Graphics.FromImage(bitmap);
            var hdc = graphics.GetHdc();
            try
            {
                if (!PrintWindow(hwnd, hdc, flags))
                {
                    graphics.ReleaseHdc(hdc);
                    bitmap.Dispose();
                    continue;
                }
            }
            finally
            {
                try { graphics.ReleaseHdc(hdc); } catch { /* already released */ }
            }

            if (!IsMostlyBlack(bitmap))
                return bitmap;
            bitmap.Dispose();
        }

        return null;
    }

    private static Bitmap? TryCaptureWithGraphicsCapture(
        IntPtr mainHwnd, GameWindowHelper.Point screenOrigin, int width, int height)
    {
        if (!GraphicsCaptureSession.IsSupported())
            return null;

        try
        {
            lock (Sync)
            {
                var device = GetDirect3DDevice();
                var item = CreateItemForWindow(mainHwnd);
                if (_wgc == null || _wgc.Hwnd != mainHwnd || _wgc.ItemSize.Width != item.Size.Width
                    || _wgc.ItemSize.Height != item.Size.Height)
                {
                    _wgc?.Dispose();
                    _wgc = CachedWgc.Start(mainHwnd, device, item);
                }

                using var frame = _wgc.WaitForFrame();
                if (frame == null)
                    return null;

                var software = SoftwareBitmap.CreateCopyFromSurfaceAsync(frame.Surface)
                    .AsTask()
                    .GetAwaiter()
                    .GetResult();
                using (software)
                {
                    var bitmap = SoftwareBitmapToBitmap(software);
                    return CropToRegion(bitmap, mainHwnd, screenOrigin, width, height);
                }
            }
        }
        catch
        {
            _wgc?.Dispose();
            _wgc = null;
            return null;
        }
    }

    private static Bitmap CropToRegion(
        Bitmap source, IntPtr mainHwnd, GameWindowHelper.Point screenOrigin, int width, int height)
    {
        if (source.Width == width && source.Height == height)
            return Ensure24bpp(source);

        if (!GameWindowHelper.TryGetWindowRect(mainHwnd, out var windowRect))
            return Ensure24bpp(source);

        var crop = Rectangle.Intersect(
            new Rectangle(0, 0, source.Width, source.Height),
            new Rectangle(screenOrigin.X - windowRect.Left, screenOrigin.Y - windowRect.Top, width, height));
        if (crop.Width < 16 || crop.Height < 16)
            return Ensure24bpp(source);

        var cropped = source.Clone(crop, PixelFormat.Format24bppRgb);
        source.Dispose();
        return cropped;
    }

    private static Bitmap Ensure24bpp(Bitmap source)
    {
        if (source.PixelFormat == PixelFormat.Format24bppRgb)
            return source;

        var converted = new Bitmap(source.Width, source.Height, PixelFormat.Format24bppRgb);
        using (var graphics = Graphics.FromImage(converted))
            graphics.DrawImageUnscaled(source, 0, 0);
        source.Dispose();
        return converted;
    }

    private static unsafe Bitmap SoftwareBitmapToBitmap(SoftwareBitmap softwareBitmap)
    {
        using var converted = softwareBitmap.BitmapPixelFormat == BitmapPixelFormat.Bgra8
            ? null
            : SoftwareBitmap.Convert(softwareBitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore);
        var source = converted ?? softwareBitmap;
        var bitmap = new Bitmap(source.PixelWidth, source.PixelHeight, PixelFormat.Format32bppArgb);
        var data = bitmap.LockBits(
            new Rectangle(0, 0, bitmap.Width, bitmap.Height),
            ImageLockMode.WriteOnly,
            PixelFormat.Format32bppArgb);
        try
        {
            using var buffer = source.LockBuffer(BitmapBufferAccessMode.Read);
            using var reference = buffer.CreateReference();
            ((IMemoryBufferByteAccess)reference).GetBuffer(out var ptr, out _);
            var plane = buffer.GetPlaneDescription(0);
            for (var y = 0; y < plane.Height; y++)
            {
                Buffer.MemoryCopy(
                    ptr + plane.StartIndex + (y * plane.Stride),
                    (byte*)data.Scan0 + (y * data.Stride),
                    data.Stride,
                    (long)(plane.Width * 4));
            }
        }
        finally
        {
            bitmap.UnlockBits(data);
        }

        return Ensure24bpp(bitmap);
    }

    private static bool IsMostlyBlack(Bitmap bitmap)
    {
        var data = bitmap.LockBits(
            new Rectangle(0, 0, bitmap.Width, bitmap.Height),
            ImageLockMode.ReadOnly,
            bitmap.PixelFormat);
        try
        {
            var bpp = Image.GetPixelFormatSize(bitmap.PixelFormat) / 8;
            if (bpp < 3)
                return false;

            var stride = data.Stride;
            var scanned = 0;
            var black = 0;
            unsafe
            {
                var scan0 = (byte*)data.Scan0;
                for (var y = 0; y < bitmap.Height; y += 8)
                {
                    var row = scan0 + (y * stride);
                    for (var x = 0; x < bitmap.Width; x += 8)
                    {
                        var pixel = row + (x * bpp);
                        scanned++;
                        if (pixel[0] < 8 && pixel[1] < 8 && pixel[2] < 8)
                            black++;
                    }
                }
            }

            return scanned > 0 && black * 100 >= scanned * 98;
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }

    private static GraphicsCaptureItem CreateItemForWindow(IntPtr hwnd)
    {
        var classId = "Windows.Graphics.Capture.GraphicsCaptureItem";
        WindowsCreateString(classId, classId.Length, out var hstring);
        try
        {
            var factoryIid = new Guid("00000035-0000-0000-C000-000000000046");
            Marshal.ThrowExceptionForHR(RoGetActivationFactory(hstring, in factoryIid, out var factory));
            try
            {
                var interopIid = typeof(IGraphicsCaptureItemInterop).GUID;
                Marshal.ThrowExceptionForHR(Marshal.QueryInterface(factory, in interopIid, out var interopPtr));
                try
                {
                    var interop = (IGraphicsCaptureItemInterop)Marshal.GetObjectForIUnknown(interopPtr);
                    var itemIid = new Guid("79C3F95B-31F7-4EC2-A464-632EF5D30760");
                    Marshal.ThrowExceptionForHR(interop.CreateForWindow(hwnd, ref itemIid, out var itemPtr));
                    try
                    {
                        return GraphicsCaptureItem.FromAbi(itemPtr);
                    }
                    finally
                    {
                        Marshal.Release(itemPtr);
                    }
                }
                finally
                {
                    Marshal.Release(interopPtr);
                }
            }
            finally
            {
                Marshal.Release(factory);
            }
        }
        finally
        {
            WindowsDeleteString(hstring);
        }
    }

    private static IDirect3DDevice GetDirect3DDevice()
    {
        if (_d3dDevice != null)
            return _d3dDevice;

        Marshal.ThrowExceptionForHR(D3D11CreateDevice(
            IntPtr.Zero,
            1, // D3D_DRIVER_TYPE_HARDWARE
            IntPtr.Zero,
            0x20, // D3D11_CREATE_DEVICE_BGRA_SUPPORT
            IntPtr.Zero,
            0,
            7, // D3D11_SDK_VERSION
            out var d3dDevice,
            out _,
            out var context));
        try
        {
            var dxgiIid = new Guid("54ec77fa-1377-44e6-8c32-88fd5f44c84c");
            Marshal.ThrowExceptionForHR(Marshal.QueryInterface(d3dDevice, in dxgiIid, out var dxgiDevice));
            try
            {
                Marshal.ThrowExceptionForHR(CreateDirect3D11DeviceFromDXGIDevice(dxgiDevice, out var inspectable));
                try
                {
                    _d3dDevice = MarshalInspectable<IDirect3DDevice>.FromAbi(inspectable);
                    return _d3dDevice;
                }
                finally
                {
                    Marshal.Release(inspectable);
                }
            }
            finally
            {
                Marshal.Release(dxgiDevice);
            }
        }
        finally
        {
            Marshal.Release(context);
            Marshal.Release(d3dDevice);
        }
    }

    private sealed class CachedWgc : IDisposable
    {
        private readonly Direct3D11CaptureFramePool _framePool;
        private readonly GraphicsCaptureSession _session;
        private readonly ManualResetEventSlim _arrived = new(false);
        private Direct3D11CaptureFrame? _frame;

        public IntPtr Hwnd { get; }
        public Windows.Graphics.SizeInt32 ItemSize { get; }

        private CachedWgc(
            IntPtr hwnd,
            Windows.Graphics.SizeInt32 itemSize,
            Direct3D11CaptureFramePool framePool,
            GraphicsCaptureSession session)
        {
            Hwnd = hwnd;
            ItemSize = itemSize;
            _framePool = framePool;
            _session = session;
            _framePool.FrameArrived += OnFrameArrived;
            _session.StartCapture();
        }

        public static CachedWgc Start(IntPtr hwnd, IDirect3DDevice device, GraphicsCaptureItem item)
        {
            var framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(
                device,
                DirectXPixelFormat.B8G8R8A8UIntNormalized,
                2,
                item.Size);
            var session = framePool.CreateCaptureSession(item);
            try { session.IsCursorCaptureEnabled = false; } catch { /* 旧系统无此属性 */ }
            return new CachedWgc(hwnd, item.Size, framePool, session);
        }

        public Direct3D11CaptureFrame? WaitForFrame()
        {
            var existing = Interlocked.Exchange(ref _frame, null);
            if (existing != null)
                return existing;

            _arrived.Reset();
            if (!_arrived.Wait(1500))
                return Interlocked.Exchange(ref _frame, null);
            return Interlocked.Exchange(ref _frame, null);
        }

        private void OnFrameArrived(Direct3D11CaptureFramePool sender, object args)
        {
            var next = sender.TryGetNextFrame();
            if (next == null)
                return;

            var old = Interlocked.Exchange(ref _frame, next);
            old?.Dispose();
            _arrived.Set();
        }

        public void Dispose()
        {
            _framePool.FrameArrived -= OnFrameArrived;
            _frame?.Dispose();
            _session.Dispose();
            _framePool.Dispose();
            _arrived.Dispose();
        }
    }

    [ComImport]
    [Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IGraphicsCaptureItemInterop
    {
        [PreserveSig]
        int CreateForWindow(IntPtr window, ref Guid iid, out IntPtr result);

        [PreserveSig]
        int CreateForMonitor(IntPtr monitor, ref Guid iid, out IntPtr result);
    }

    [ComImport]
    [Guid("5B0D3235-4DBA-4D44-865E-8F1D0E4FD04D")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private unsafe interface IMemoryBufferByteAccess
    {
        void GetBuffer(out byte* buffer, out uint capacity);
    }

    [DllImport("user32.dll")]
    private static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);

    [DllImport("combase.dll", PreserveSig = true)]
    private static extern int WindowsCreateString(
        [MarshalAs(UnmanagedType.LPWStr)] string sourceString, int length, out IntPtr hstring);

    [DllImport("combase.dll", PreserveSig = true)]
    private static extern int WindowsDeleteString(IntPtr hstring);

    [DllImport("combase.dll", PreserveSig = true)]
    private static extern int RoGetActivationFactory(IntPtr activatableClassId, in Guid iid, out IntPtr factory);

    [DllImport("d3d11.dll")]
    private static extern int D3D11CreateDevice(
        IntPtr pAdapter,
        int driverType,
        IntPtr software,
        uint flags,
        IntPtr pFeatureLevels,
        uint featureLevels,
        uint sdkVersion,
        out IntPtr ppDevice,
        out int pFeatureLevel,
        out IntPtr ppImmediateContext);

    [DllImport("d3d11.dll", EntryPoint = "CreateDirect3D11DeviceFromDXGIDevice")]
    private static extern int CreateDirect3D11DeviceFromDXGIDevice(IntPtr dxgiDevice, out IntPtr graphicsDevice);
}
