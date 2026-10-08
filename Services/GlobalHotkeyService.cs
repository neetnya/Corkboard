using System;
using System.Runtime.InteropServices;
using System.Windows.Input;
using Corkboard.Models;

namespace Corkboard.Services;

/// <summary>RegisterHotKey 全局快捷键服务。</summary>
public sealed class GlobalHotkeyService : IDisposable
{
    [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    public const int WM_HOTKEY = 0x0312;
    public const int RegionHotkeyId = 9001;
    public const int FullScreenHotkeyId = 9002;

    private IntPtr _hwnd;
    private bool _regionRegistered, _fullRegistered;

    public event Action? RegionRequested;
    public event Action? FullScreenRequested;

    public void Register(IntPtr hwnd, HotkeyConfig region, HotkeyConfig full)
    {
        _hwnd = hwnd;
        UnregisterAll();
        _regionRegistered = RegisterHotKey(hwnd, RegionHotkeyId, ToMods(region.Modifiers), ToVk(region.Key));
        _fullRegistered = RegisterHotKey(hwnd, FullScreenHotkeyId, ToMods(full.Modifiers), ToVk(full.Key));
    }

    public void UnregisterAll()
    {
        if (_hwnd != IntPtr.Zero)
        {
            if (_regionRegistered) UnregisterHotKey(_hwnd, RegionHotkeyId);
            if (_fullRegistered) UnregisterHotKey(_hwnd, FullScreenHotkeyId);
        }
        _regionRegistered = _fullRegistered = false;
    }

    public void HandleMessage(int msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg != WM_HOTKEY) return;
        int id = wParam.ToInt32();
        if (id == RegionHotkeyId) RegionRequested?.Invoke();
        else if (id == FullScreenHotkeyId) FullScreenRequested?.Invoke();
    }

    public void Dispose() => UnregisterAll();

    private static uint ToMods(HotModifiers m)
    {
        uint r = 0;
        if ((m & HotModifiers.Alt) != 0) r |= 0x0001;      // MOD_ALT
        if ((m & HotModifiers.Control) != 0) r |= 0x0002;  // MOD_CONTROL
        if ((m & HotModifiers.Shift) != 0) r |= 0x0004;    // MOD_SHIFT
        if ((m & HotModifiers.Win) != 0) r |= 0x0008;      // MOD_WIN
        return r;
    }

    private static uint ToVk(Key key) => (uint)KeyInterop.VirtualKeyFromKey(key);
}
