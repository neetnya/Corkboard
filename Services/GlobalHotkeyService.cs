using System;
using System.Runtime.InteropServices;
using System.Windows.Input;
using Corkboard.Models;

namespace Corkboard.Services;

public readonly struct HotkeyRegisterResult
{
    public bool RegionOk { get; init; }
    public bool FullOk { get; init; }
    public int RegionError { get; init; }
    public int FullError { get; init; }
    public bool Ok => RegionOk && FullOk;
}

/// <summary>RegisterHotKey 全局快捷键服务。</summary>
public sealed class GlobalHotkeyService : IDisposable
{
    [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    public const int WM_HOTKEY = 0x0312;
    public const int RegionHotkeyId = 9001;
    public const int FullScreenHotkeyId = 9002;
    private const uint MOD_NOREPEAT = 0x4000;

    private IntPtr _hwnd;
    private bool _regionRegistered, _fullRegistered;

    public event Action? RegionRequested;
    public event Action? FullScreenRequested;

    public HotkeyRegisterResult Register(IntPtr hwnd, HotkeyConfig region, HotkeyConfig full)
    {
        _hwnd = hwnd;
        UnregisterAll();
        _regionRegistered = RegisterHotKey(hwnd, RegionHotkeyId, ToMods(region.Modifiers) | MOD_NOREPEAT, ToVk(region.Key));
        int regionErr = _regionRegistered ? 0 : Marshal.GetLastWin32Error();
        _fullRegistered = RegisterHotKey(hwnd, FullScreenHotkeyId, ToMods(full.Modifiers) | MOD_NOREPEAT, ToVk(full.Key));
        int fullErr = _fullRegistered ? 0 : Marshal.GetLastWin32Error();
        return new HotkeyRegisterResult
        {
            RegionOk = _regionRegistered,
            FullOk = _fullRegistered,
            RegionError = regionErr,
            FullError = fullErr,
        };
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
