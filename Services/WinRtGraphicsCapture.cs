using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using Windows.Graphics.Imaging;
using WinRT;

namespace Corkboard.Services;

/// <summary>Windows.Graphics.Capture：对 GPU/游戏画面比 BitBlt 可靠。失败时由调用方回退。</summary>
internal static class WinRtGraphicsCapture
{
    private const int D3D11_SDK_VERSION = 7;
    private const int D3D_DRIVER_TYPE_HARDWARE = 1;
    private const uint D3D11_CREATE_DEVICE_BGRA_SUPPORT = 0x20;
    private static readonly Guid GraphicsCaptureItemIid = new("79C3F95B-31F7-4EC2-A464-632EF5D30760");

    [DllImport("d3d11.dll")]
    private static extern int D3D11CreateDevice(
        IntPtr pAdapter, int driverType, IntPtr software, uint flags,
        IntPtr pFeatureLevels, int featureLevels, int sdkVersion,
        out IntPtr ppDevice, out int pFeatureLevel, out IntPtr ppImmediateContext);

    [DllImport("d3d11.dll", EntryPoint = "CreateDirect3D11DeviceFromDXGIDevice")]
    private static extern int CreateDirect3D11DeviceFromDXGIDevice(IntPtr dxgiDevice, out IntPtr graphicsDevice);

    [ComImport]
    [Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IGraphicsCaptureItemInterop
    {
        int CreateForWindow(IntPtr window, ref Guid iid, out IntPtr result);
        int CreateForMonitor(IntPtr monitor, ref Guid iid, out IntPtr result);
    }

    [ComImport]
    [Guid("5B0D3235-4DBA-4D44-865E-8F1D0E4FD04D")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMemoryBufferByteAccess
    {
        void GetBuffer(out IntPtr buffer, out uint capacity);
    }

    private static IDirect3DDevice? _device;

    public static async Task<BitmapSource?> CaptureMonitorAsync(IntPtr hMonitor, TimeSpan timeout)
    {
        try
        {
            DispatcherQueueHelper.EnsureWinRTQueue();
            var device = GetDevice();
            if (device is null) return null;
            var item = CreateItemForMonitor(hMonitor);
            if (item is null) return null;

            var pool = Direct3D11CaptureFramePool.Create(
                device, DirectXPixelFormat.B8G8R8A8UIntNormalized, 1, item.Size);
            using var session = pool.CreateCaptureSession(item);
            session.IsCursorCaptureEnabled = false;

            var tcs = new TaskCompletionSource<Direct3D11CaptureFrame>(TaskCreationOptions.RunContinuationsAsynchronously);
            pool.FrameArrived += (_, _) =>
            {
                try
                {
                    var frame = pool.TryGetNextFrame();
                    if (frame != null) tcs.TrySetResult(frame);
                }
                catch (Exception ex) { tcs.TrySetException(ex); }
            };
            session.StartCapture();

            Direct3D11CaptureFrame captured;
            using (var cts = new System.Threading.CancellationTokenSource(timeout))
            {
                await using var reg = cts.Token.Register(() => tcs.TrySetCanceled());
                try { captured = await tcs.Task.ConfigureAwait(true); }
                catch { pool.Dispose(); return null; }
            }

            using (captured)
            {
                var software = await SoftwareBitmap.CreateCopyFromSurfaceAsync(captured.Surface);
                pool.Dispose();
                return ToBitmapSource(software);
            }
        }
        catch
        {
            return null;
        }
    }

    private static GraphicsCaptureItem? CreateItemForMonitor(IntPtr hMonitor)
    {
        var factory = ActivationFactory.Get(typeof(GraphicsCaptureItem).FullName!);
        var interop = factory.AsInterface<IGraphicsCaptureItemInterop>();
        var iid = GraphicsCaptureItemIid;
        Marshal.ThrowExceptionForHR(interop.CreateForMonitor(hMonitor, ref iid, out var ptr));
        try { return MarshalInspectable<GraphicsCaptureItem>.FromAbi(ptr); }
        finally { Marshal.Release(ptr); }
    }

    private static IDirect3DDevice? GetDevice()
    {
        if (_device != null) return _device;
        int hr = D3D11CreateDevice(IntPtr.Zero, D3D_DRIVER_TYPE_HARDWARE, IntPtr.Zero,
            D3D11_CREATE_DEVICE_BGRA_SUPPORT, IntPtr.Zero, 0, D3D11_SDK_VERSION,
            out var device, out _, out var context);
        if (hr < 0 || device == IntPtr.Zero) return null;
        if (context != IntPtr.Zero) Marshal.Release(context);
        try
        {
            var dxgiIid = new Guid("54ec77fa-1377-44e6-8c32-88fd5f339c3c"); // IDXGIDevice
            Marshal.ThrowExceptionForHR(Marshal.QueryInterface(device, ref dxgiIid, out var dxgi));
            try
            {
                Marshal.ThrowExceptionForHR(CreateDirect3D11DeviceFromDXGIDevice(dxgi, out var inspectable));
                _device = MarshalInspectable<IDirect3DDevice>.FromAbi(inspectable);
                Marshal.Release(inspectable);
            }
            finally { Marshal.Release(dxgi); }
        }
        finally { Marshal.Release(device); }
        return _device;
    }

    private static BitmapSource ToBitmapSource(SoftwareBitmap software)
    {
        SoftwareBitmap sb = software;
        if (software.BitmapPixelFormat != BitmapPixelFormat.Bgra8
            || software.BitmapAlphaMode != BitmapAlphaMode.Premultiplied)
        {
            sb = SoftwareBitmap.Convert(software, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
            software.Dispose();
        }
        try
        {
            int w = sb.PixelWidth;
            int h = sb.PixelHeight;
            var wb = new WriteableBitmap(w, h, 96, 96, PixelFormats.Bgra32, null);
            using (var buffer = sb.LockBuffer(BitmapBufferAccessMode.Read))
            using (var reference = buffer.CreateReference())
            {
                var access = (IMemoryBufferByteAccess)reference;
                access.GetBuffer(out var data, out _);
                var plane = buffer.GetPlaneDescription(0);
                int dstStride = wb.BackBufferStride;
                var pixels = new byte[dstStride * h];
                int copy = Math.Min(plane.Stride, dstStride);
                for (int y = 0; y < h; y++)
                    Marshal.Copy(data + y * plane.Stride, pixels, y * dstStride, copy);
                wb.WritePixels(new Int32Rect(0, 0, w, h), pixels, dstStride, 0);
            }
            wb.Freeze();
            return wb;
        }
        finally
        {
            sb.Dispose();
        }
    }
}
