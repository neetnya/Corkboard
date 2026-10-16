using System;
using System.Runtime.InteropServices;

namespace Corkboard.Services;

/// <summary>WPF 线程没有 WinRT DispatcherQueue，Graphics Capture 需要先建一个。</summary>
internal static class DispatcherQueueHelper
{
    [StructLayout(LayoutKind.Sequential)]
    private struct DispatcherQueueOptions
    {
        public int dwSize;
        public int threadType;
        public int apartmentType;
    }

    [DllImport("CoreMessaging.dll")]
    private static extern int CreateDispatcherQueueController(DispatcherQueueOptions options, out IntPtr dispatcherQueueController);

    private static IntPtr _controller;

    public static void EnsureWinRTQueue()
    {
        if (_controller != IntPtr.Zero) return;
        var opt = new DispatcherQueueOptions
        {
            dwSize = Marshal.SizeOf<DispatcherQueueOptions>(),
            threadType = 2,      // DQTYPE_THREAD_CURRENT
            apartmentType = 2,   // DQTAT_COM_STA
        };
        Marshal.ThrowExceptionForHR(CreateDispatcherQueueController(opt, out _controller));
    }
}
