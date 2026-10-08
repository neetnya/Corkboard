using System;
using System.Windows.Input;

namespace Corkboard.Models;

[Flags]
public enum HotModifiers
{
    None = 0,
    Alt = 1,
    Control = 2,
    Shift = 4,
    Win = 8,
}

/// <summary>一个可配置全局快捷键。</summary>
public sealed class HotkeyConfig
{
    public HotModifiers Modifiers { get; set; } = HotModifiers.Control | HotModifiers.Alt;
    public Key Key { get; set; } = Key.A;

    public override string ToString()
    {
        var parts = new System.Collections.Generic.List<string>();
        if ((Modifiers & HotModifiers.Control) != 0) parts.Add("Ctrl");
        if ((Modifiers & HotModifiers.Alt) != 0) parts.Add("Alt");
        if ((Modifiers & HotModifiers.Shift) != 0) parts.Add("Shift");
        if ((Modifiers & HotModifiers.Win) != 0) parts.Add("Win");
        parts.Add(KeyName(Key));
        return string.Join("+", parts);
    }

    private static string KeyName(Key k) => k switch
    {
        Key.D0 => "0", Key.D1 => "1", Key.D2 => "2", Key.D3 => "3", Key.D4 => "4",
        Key.D5 => "5", Key.D6 => "6", Key.D7 => "7", Key.D8 => "8", Key.D9 => "9",
        Key.OemPlus => "+", Key.OemMinus => "-", Key.OemComma => ",", Key.OemPeriod => ".",
        _ => k.ToString()
    };
}

/// <summary>settings.json 的根。</summary>
public sealed class AppSettings
{
    public HotkeyConfig RegionHotkey { get; set; } = new() { Modifiers = HotModifiers.Control | HotModifiers.Alt, Key = Key.A };
    public HotkeyConfig FullScreenHotkey { get; set; } = new() { Modifiers = HotModifiers.Control | HotModifiers.Alt, Key = Key.S };
    public string ImageFormat { get; set; } = "png";
}
