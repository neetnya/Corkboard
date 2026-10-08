using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Corkboard.Models;

namespace Corkboard.Controls;

/// <summary>设置窗口：录制两个全局快捷键。</summary>
public sealed class SettingsWindow : Window
{
    private readonly AppSettings _settings;
    private readonly Action _onSave;
    private readonly HotkeyBox _regionBox;
    private readonly HotkeyBox _fullBox;

    public SettingsWindow(AppSettings settings, Action onSave)
    {
        _settings = settings;
        _onSave = onSave;
        Title = "设置";
        Width = 400;
        Height = 250;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;

        _regionBox = new HotkeyBox(settings.RegionHotkey);
        _fullBox = new HotkeyBox(settings.FullScreenHotkey);

        var grid = new Grid { Margin = new Thickness(16) };
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        AddRow(grid, 0, "局部截图", _regionBox);
        AddRow(grid, 1, "全屏截图", _fullBox);

        var hint = new TextBlock
        {
            Text = "点击输入框后按下组合键即可录制（如 Ctrl+Alt+A），Esc 取消。",
            Foreground = Brushes.Gray,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 6, 0, 0),
        };
        Grid.SetRow(hint, 2);
        Grid.SetColumnSpan(hint, 2);
        grid.Children.Add(hint);

        var ok = new Button { Content = "确定", Width = 80, IsDefault = true };
        var cancel = new Button { Content = "取消", Width = 80, IsCancel = true };
        var btns = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 12, 0, 0),
        };
        btns.Children.Add(ok);
        btns.Children.Add(cancel);
        Grid.SetRow(btns, 3);
        Grid.SetColumnSpan(btns, 2);
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
        grid.Children.Add(btns);

        ok.Click += (_, _) =>
        {
            _regionBox.Apply();
            _fullBox.Apply();
            _onSave();
            DialogResult = true;
        };

        Content = grid;
    }

    private static void AddRow(Grid g, int row, string label, FrameworkElement box)
    {
        var l = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetRow(l, row);
        Grid.SetColumn(l, 0);
        Grid.SetRow(box, row);
        Grid.SetColumn(box, 1);
        box.Margin = new Thickness(0, 4, 0, 4);
        g.Children.Add(l);
        g.Children.Add(box);
    }
}

/// <summary>录制快捷键的文本框（写入前用工作副本，取消不生效）。</summary>
internal sealed class HotkeyBox : TextBox
{
    private readonly HotkeyConfig _config;
    private HotkeyConfig _working;

    public HotkeyBox(HotkeyConfig config)
    {
        _config = config;
        _working = Clone(config);
        IsReadOnly = true;
        IsReadOnlyCaretVisible = false;
        Text = config.ToString();
        ToolTip = "点击后按下组合键";
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        Text = "按下组合键...";
        Focus();
        e.Handled = true;
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Escape)
        {
            _working = Clone(_config);
            Text = _config.ToString();
            return;
        }
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
            return; // 仅修饰键：继续等待
        _working.Modifiers = ToHotMods(Keyboard.Modifiers);
        _working.Key = key;
        Text = _working.ToString();
    }

    public void Apply()
    {
        _config.Modifiers = _working.Modifiers;
        _config.Key = _working.Key;
    }

    private static HotkeyConfig Clone(HotkeyConfig c)
        => new() { Modifiers = c.Modifiers, Key = c.Key };

    private static HotModifiers ToHotMods(ModifierKeys m)
    {
        HotModifiers r = HotModifiers.None;
        if (m.HasFlag(ModifierKeys.Control)) r |= HotModifiers.Control;
        if (m.HasFlag(ModifierKeys.Alt)) r |= HotModifiers.Alt;
        if (m.HasFlag(ModifierKeys.Shift)) r |= HotModifiers.Shift;
        if (m.HasFlag(ModifierKeys.Windows)) r |= HotModifiers.Win;
        return r;
    }
}
