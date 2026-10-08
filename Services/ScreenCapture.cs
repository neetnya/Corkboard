using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace Corkboard.Services;

/// <summary>基于 GDI BitBlt 的屏幕捕获（零第三方依赖）。</summary>
public static class ScreenCapture
{
    [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hWnd);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr hdc);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int w, int h);
    [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);
    [DllImport("gdi32.dll")] private static extern bool BitBlt(IntPtr hdcDest, int x, int y, int w, int h, IntPtr hdcSrc, int sx, int sy, int rop);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr obj);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr hdc);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hWnd, IntPtr hdc);
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int nIndex);

    private const int SRCCOPY = 0x00CC0020;
    private const int SM_CXSCREEN = 0;
    private const int SM_CYSCREEN = 1;

    /// <summary>捕获屏幕物理像素坐标 (x,y,w,h) 区域。</summary>
    public static BitmapSource CaptureRegion(int x, int y, int w, int h)
    {
        if (w <= 0 || h <= 0) throw new ArgumentException("无效的截图区域");
        IntPtr hdc = GetDC(IntPtr.Zero);
        IntPtr mem = CreateCompatibleDC(hdc);
        IntPtr bmp = CreateCompatibleBitmap(hdc, w, h);
        IntPtr old = SelectObject(mem, bmp);
        BitBlt(mem, 0, 0, w, h, hdc, x, y, SRCCOPY);
        SelectObject(mem, old);
        BitmapSource bs;
        try
        {
            bs = Imaging.CreateBitmapSourceFromHBitmap(bmp, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
        }
        finally
        {
            DeleteObject(bmp);
            DeleteDC(mem);
            ReleaseDC(IntPtr.Zero, hdc);
        }
        bs.Freeze();
        return bs;
    }

    /// <summary>捕获主屏（物理像素）。</summary>
    public static BitmapSource CapturePrimaryScreen()
    {
        int w = GetSystemMetrics(SM_CXSCREEN);
        int h = GetSystemMetrics(SM_CYSCREEN);
        return CaptureRegion(0, 0, w, h);
    }
}
