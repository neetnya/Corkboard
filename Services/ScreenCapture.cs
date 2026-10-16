using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace Corkboard.Services;

/// <summary>屏幕捕获：优先 Windows.Graphics.Capture（游戏/GPU 窗口），失败回退 BitBlt。</summary>
public static class ScreenCapture
{
    [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hWnd);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr hdc);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int w, int h);
    [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);
    [DllImport("gdi32.dll")] private static extern bool BitBlt(IntPtr hdcDest, int x, int y, int w, int h, IntPtr hdcSrc, int sx, int sy, uint rop);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr obj);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr hdc);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hWnd, IntPtr hdc);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT pt);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromPoint(POINT pt, uint flags);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromRect(ref RECT lprc, uint flags);
    [DllImport("user32.dll")] private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    private const uint SRCCOPY = 0x00CC0020;
    private const uint CAPTUREBLT = 0x40000000;
    private const uint MONITOR_DEFAULTTONEAREST = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    /// <summary>捕获屏幕物理像素坐标区域。失败返回 null，不落盘。</summary>
    public static async Task<BitmapSource?> CaptureRegionAsync(int x, int y, int w, int h)
    {
        if (w <= 0 || h <= 0) return null;
        var rect = new RECT { Left = x, Top = y, Right = x + w, Bottom = y + h };
        var hMon = MonitorFromRect(ref rect, MONITOR_DEFAULTTONEAREST);
        if (hMon != IntPtr.Zero && TryGetMonitorRect(hMon, out var mon) && Contains(mon, rect))
        {
            var full = await WinRtGraphicsCapture.CaptureMonitorAsync(hMon, TimeSpan.FromMilliseconds(800));
            if (full != null)
                return Crop(full, x - mon.Left, y - mon.Top, w, h);
        }
        return CaptureRegionGdi(x, y, w, h);
    }

    /// <summary>捕获鼠标所在显示器（物理像素）。</summary>
    public static Task<BitmapSource?> CapturePointerScreenAsync()
    {
        if (!GetCursorPos(out var pt))
            return CaptureRegionAsync(0, 0, 1, 1);
        var hMon = MonitorFromPoint(pt, MONITOR_DEFAULTTONEAREST);
        if (hMon != IntPtr.Zero && TryGetMonitorRect(hMon, out var mon))
            return CaptureMonitorAsync(hMon, mon);
        return CaptureRegionAsync(pt.X, pt.Y, 1, 1);
    }

    private static async Task<BitmapSource?> CaptureMonitorAsync(IntPtr hMon, RECT mon)
    {
        var full = await WinRtGraphicsCapture.CaptureMonitorAsync(hMon, TimeSpan.FromMilliseconds(800));
        if (full != null) return full;
        return CaptureRegionGdi(mon.Left, mon.Top, mon.Right - mon.Left, mon.Bottom - mon.Top);
    }

    private static bool TryGetMonitorRect(IntPtr hMon, out RECT rect)
    {
        var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        if (!GetMonitorInfo(hMon, ref info))
        {
            rect = default;
            return false;
        }
        rect = info.rcMonitor;
        return true;
    }

    private static bool Contains(RECT mon, RECT region)
        => region.Left >= mon.Left && region.Top >= mon.Top
           && region.Right <= mon.Right && region.Bottom <= mon.Bottom;

    private static BitmapSource? Crop(BitmapSource src, int x, int y, int w, int h)
    {
        x = Math.Clamp(x, 0, Math.Max(0, src.PixelWidth - 1));
        y = Math.Clamp(y, 0, Math.Max(0, src.PixelHeight - 1));
        w = Math.Clamp(w, 1, src.PixelWidth - x);
        h = Math.Clamp(h, 1, src.PixelHeight - y);
        var cropped = new CroppedBitmap(src, new Int32Rect(x, y, w, h));
        cropped.Freeze();
        return cropped;
    }

    private static BitmapSource? CaptureRegionGdi(int x, int y, int w, int h)
    {
        IntPtr hdc = GetDC(IntPtr.Zero);
        if (hdc == IntPtr.Zero) return null;
        IntPtr mem = CreateCompatibleDC(hdc);
        IntPtr bmp = CreateCompatibleBitmap(hdc, w, h);
        if (mem == IntPtr.Zero || bmp == IntPtr.Zero)
        {
            if (bmp != IntPtr.Zero) DeleteObject(bmp);
            if (mem != IntPtr.Zero) DeleteDC(mem);
            ReleaseDC(IntPtr.Zero, hdc);
            return null;
        }
        IntPtr old = SelectObject(mem, bmp);
        bool ok = BitBlt(mem, 0, 0, w, h, hdc, x, y, SRCCOPY | CAPTUREBLT);
        SelectObject(mem, old);
        BitmapSource? bs = null;
        try
        {
            if (ok)
            {
                bs = Imaging.CreateBitmapSourceFromHBitmap(bmp, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                bs.Freeze();
            }
        }
        finally
        {
            DeleteObject(bmp);
            DeleteDC(mem);
            ReleaseDC(IntPtr.Zero, hdc);
        }
        return bs;
    }
}
