using System;
using System.Runtime.InteropServices;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Corkboard.Services;

/// <summary>系统托盘图标（Shell_NotifyIcon，零第三方依赖）。</summary>
public sealed class TrayIconService : IDisposable
{
    [DllImport("shell32.dll")] private static extern bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATA data);
    [DllImport("user32.dll")] private static extern IntPtr LoadIcon(IntPtr hInstance, IntPtr lpIconName);
    [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr hIcon);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("shell32.dll", CharSet = CharSet.Auto)] private static extern uint ExtractIconEx(string lpszFile, int nIconIndex, IntPtr[] phiconLarge, IntPtr[] phiconSmall, uint nIcons);

    private const uint NIM_ADD = 0x00000000;
    private const uint NIM_DELETE = 0x00000002;
    private const uint NIF_MESSAGE = 0x00000001;
    private const uint NIF_ICON = 0x00000002;
    private const uint NIF_TIP = 0x00000004;
    private const uint WM_LBUTTONDBLCLK = 0x0203;
    private const uint WM_RBUTTONUP = 0x0205;
    private const int IDI_APPLICATION = 32512;

    private readonly IntPtr _hwnd;
    private readonly uint _callbackMessage;
    private IntPtr _icon;
    private bool _added;

    public event Action? DoubleClick;
    public event Action? ShowRequested;
    public event Action? ExitRequested;

    public TrayIconService(IntPtr hwnd, uint callbackMessage)
    {
        _hwnd = hwnd;
        _callbackMessage = callbackMessage;
    }

    public void Show()
    {
        _icon = LoadAppIcon();
        var data = new NOTIFYICONDATA
        {
            cbSize = (uint)Marshal.SizeOf(typeof(NOTIFYICONDATA)),
            hWnd = _hwnd,
            uID = 1,
            uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
            uCallbackMessage = _callbackMessage,
            hIcon = _icon,
            szTip = "截图线索板",
        };
        _added = Shell_NotifyIcon(NIM_ADD, ref data);
    }

    public void HandleMessage(int msg, IntPtr lParam)
    {
        if ((uint)msg != _callbackMessage) return;
        uint l = (uint)(lParam.ToInt64() & 0xFFFFFFFF);
        if (l == WM_LBUTTONDBLCLK) DoubleClick?.Invoke();
        else if (l == WM_RBUTTONUP) ShowContextMenu();
    }

    private void ShowContextMenu()
    {
        SetForegroundWindow(_hwnd);
        var menu = new ContextMenu { Placement = PlacementMode.MousePoint };
        var show = new MenuItem { Header = "显示主窗口" };
        show.Click += (_, _) => ShowRequested?.Invoke();
        var exit = new MenuItem { Header = "退出" };
        exit.Click += (_, _) => ExitRequested?.Invoke();
        menu.Items.Add(show);
        menu.Items.Add(new Separator());
        menu.Items.Add(exit);
        menu.IsOpen = true;
    }

    private static IntPtr LoadAppIcon()
    {
        string? path = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(path))
        {
            var large = new IntPtr[1];
            var small = new IntPtr[1];
            if (ExtractIconEx(path, 0, large, small, 1) > 0)
            {
                if (large[0] != IntPtr.Zero) DestroyIcon(large[0]);
                if (small[0] != IntPtr.Zero) return small[0];
            }
        }
        return LoadIcon(IntPtr.Zero, (IntPtr)IDI_APPLICATION);
    }

    public void Dispose()
    {
        if (_added)
        {
            var data = new NOTIFYICONDATA { cbSize = (uint)Marshal.SizeOf(typeof(NOTIFYICONDATA)), hWnd = _hwnd, uID = 1 };
            Shell_NotifyIcon(NIM_DELETE, ref data);
            _added = false;
        }
        if (_icon != IntPtr.Zero) { DestroyIcon(_icon); _icon = IntPtr.Zero; }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NOTIFYICONDATA
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
    }
}
