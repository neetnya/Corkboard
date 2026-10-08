using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Corkboard.Controls;
using Corkboard.Models;
using Corkboard.Services;

namespace Corkboard;

public partial class MainWindow : Window
{
    private readonly StoreService _store = new();
    private ThumbnailCache _thumbs = null!;
    private readonly GlobalHotkeyService _hotkeys = new();

    private Group? _currentGroup;
    private BoardImageControl? _editing;
    private EditorWindow? _editorWindow;
    private int _cascade;

    public MainWindow()
    {
        InitializeComponent();
        _store.Initialize();
        _thumbs = new ThumbnailCache(_store);
        _hotkeys.RegionRequested += OnRegionHotkey;
        _hotkeys.FullScreenRequested += OnFullHotkey;
        Loaded += OnLoaded;
    }

    // ---------- 初始化 ----------

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        var source = HwndSource.FromHwnd(hwnd);
        source?.AddHook(WndProc);
        _hotkeys.Register(hwnd, _store.Settings.RegionHotkey, _store.Settings.FullScreenHotkey);
        UpdateHotkeyHint();
        PopulateGroups();
        SelectCurrentGroup();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == GlobalHotkeyService.WM_HOTKEY)
        {
            _hotkeys.HandleMessage(msg, wParam, lParam);
            handled = true;
        }
        return IntPtr.Zero;
    }

    private void UpdateHotkeyHint()
        => HotkeyHint.Text = $"局部截图：{_store.Settings.RegionHotkey}    全屏截图：{_store.Settings.FullScreenHotkey}";

    // ---------- 分组 ----------

    private void PopulateGroups()
    {
        GroupCombo.ItemsSource = null;
        GroupCombo.ItemsSource = _store.Store.Groups;
    }

    private void SelectCurrentGroup()
    {
        var g = _store.CurrentGroup;
        if (g is null) return;
        GroupCombo.SelectedValue = g.Id;
        LoadGroup(g);
    }

    private void LoadGroup(Group g)
    {
        SaveCurrentBoard();
        FinishEdit();
        _currentGroup = g;
        _cascade = 0;
        ShotList.ItemsSource = g.Items;
        foreach (var it in g.Items) it.Thumbnail = _thumbs.Get(it);
        LoadBoard(g.Board);
    }

    private void GroupCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (GroupCombo.SelectedItem is Group g && g != _currentGroup)
        {
            _store.Store.CurrentGroupId = g.Id;
            _store.SaveStore();
            LoadGroup(g);
        }
    }

    private void NewGroup_Click(object sender, RoutedEventArgs e)
    {
        var name = Prompt("新建分组", "分组名称：", "新分组");
        if (string.IsNullOrWhiteSpace(name)) return;
        var g = new Group { Name = name.Trim() };
        _store.Store.Groups.Add(g);
        _store.Store.CurrentGroupId = g.Id;
        _store.SaveStore();
        PopulateGroups();
        GroupCombo.SelectedValue = g.Id;
        LoadGroup(g);
    }

    private void RenameGroup_Click(object sender, RoutedEventArgs e)
    {
        if (_currentGroup is null) return;
        var name = Prompt("重命名分组", "分组名称：", _currentGroup.Name);
        if (string.IsNullOrWhiteSpace(name)) return;
        _currentGroup.Name = name.Trim();
        _store.SaveStore();
        PopulateGroups();
        GroupCombo.SelectedValue = _currentGroup.Id;
    }

    private void DeleteGroup_Click(object sender, RoutedEventArgs e)
    {
        if (_currentGroup is null) return;
        if (_store.Store.Groups.Count <= 1)
        {
            MessageBox.Show(this, "至少保留一个分组。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (MessageBox.Show(this, $"确定删除分组「{_currentGroup.Name}」及其全部截图？此操作不可恢复。",
            "删除分组", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        var g = _currentGroup;
        _store.DeleteGroup(g);
        _thumbs = new ThumbnailCache(_store);
        PopulateGroups();
        SelectCurrentGroup();
    }

    // ---------- 截图 ----------

    private async void OnRegionHotkey()
    {
        bool wasVisible = IsVisible;
        Hide();
        await Task.Delay(60);

        Rect? region = null;
        var sel = new RegionSelectorWindow();
        sel.RegionSelected += r => region = r;
        sel.ShowDialog();

        if (region is Rect r2)
        {
            await Task.Delay(60);
            try
            {
                AddScreenshot(ScreenCapture.CaptureRegion((int)r2.X, (int)r2.Y, (int)r2.Width, (int)r2.Height));
            }
            catch { /* 截图失败忽略 */ }
        }

        if (wasVisible) { Show(); Activate(); }
    }

    private async void OnFullHotkey()
    {
        bool wasVisible = IsVisible;
        Hide();
        await Task.Delay(60);
        try { AddScreenshot(ScreenCapture.CapturePrimaryScreen()); }
        catch { }
        if (wasVisible) { Show(); Activate(); }
    }

    private void AddScreenshot(BitmapSource bmp)
    {
        if (_currentGroup is null) return;
        var item = _store.AddScreenshot(_currentGroup, bmp);
        item.Thumbnail = _thumbs.Get(item);
    }

    // ---------- 右侧列表 ----------

    private void ShotList_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject dep) return;
        var li = ItemsControl.ContainerFromElement(ShotList, dep) as ListBoxItem;
        if (li?.DataContext is ScreenshotItem item) AddImageToBoard(item);
    }

    private void ShotListDeleteItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem mi || mi.Parent is not ContextMenu cm) return;
        if (cm.PlacementTarget is FrameworkElement fe && fe.DataContext is ScreenshotItem item)
            DeleteScreenshotBoth(item);
    }

    private void DeleteScreenshotBoth(ScreenshotItem item)
    {
        if (_currentGroup is null) return;
        if (MessageBox.Show(this, "删除该截图？（将同时从白板与磁盘移除，不可恢复）",
            "删除截图", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;

        foreach (var ctl in Board.Children.OfType<BoardImageControl>().Where(c => c.Screenshot.Id == item.Id).ToList())
            Board.RemoveItem(ctl);

        _store.DeleteScreenshot(_currentGroup, item);
        _thumbs.Invalidate(item.Id);
        SaveCurrentBoard();
    }

    // ---------- 白板 ----------

    private void AddImageToBoard(ScreenshotItem item)
    {
        if (_currentGroup is null) return;
        var full = LoadFullImage(_store.ResolvePath(item.File));
        if (full is null) return;

        var ctl = new BoardImageControl(full, item) { Board = Board };
        double initScale = Math.Min(1.0, Math.Min(560.0 / Math.Max(1, full.PixelWidth), 420.0 / Math.Max(1, full.PixelHeight)));
        ctl.SetZoom(initScale);
        ctl.EditRequested += OnEditImage;
        ctl.DeleteRequested += OnDeleteImageLeft;
        ctl.WriteBackRequested += OnWriteBack;
        ctl.Changed += SaveCurrentBoard;

        double x = 60 + (_cascade % 8) * 42;
        double y = 40 + (_cascade % 8) * 42;
        _cascade++;

        Board.AddImageOnTop(ctl, x, y);
        SaveCurrentBoard();
    }

    private void AddNote_Click(object sender, RoutedEventArgs e)
    {
        if (_currentGroup is null) return;
        var state = new NoteState
        {
            X = 100 + (_cascade % 5) * 36,
            Y = 90 + (_cascade % 5) * 36,
            Text = "双击编辑便条",
        };
        _cascade++;
        var note = new NoteControl(state) { Board = Board };
        note.Changed += SaveCurrentBoard;
        Board.AddNoteOnTop(note, state.X, state.Y);
        SaveCurrentBoard();
    }

    private void OnDeleteImageLeft(BoardImageControl ctl)
    {
        Board.RemoveItem(ctl);
        if (_editing == ctl) FinishEdit();
        SaveCurrentBoard();
    }

    private void OnWriteBack(BoardImageControl ctl)
    {
        try
        {
            var flat = ctl.RenderFlattened();
            StoreService.SavePng(flat, _store.ResolvePath(ctl.Screenshot.File));
            ctl.Annotation.Clear();
            ctl.SetBaseImage(flat);
            _thumbs.Invalidate(ctl.Screenshot.Id);
            ctl.Screenshot.Thumbnail = _thumbs.Get(ctl.Screenshot);
            ctl.SaveState();
            SaveCurrentBoard();
            MessageBox.Show(this, "已写入右侧并覆盖原图。", "完成", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "写入失败：" + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SaveCurrentBoard()
    {
        if (_currentGroup is null) return;
        var board = _currentGroup.Board;
        board.Images.Clear();
        board.Notes.Clear();
        foreach (var c in Board.Children.OfType<BoardImageControl>())
        {
            c.SaveState();
            board.Images.Add(c.State);
        }
        foreach (var n in Board.Children.OfType<NoteControl>())
        {
            n.SaveState();
            board.Notes.Add(n.State);
        }
        _store.SaveStore();
    }

    private void LoadBoard(BoardState board)
    {
        Board.ClearAll();
        foreach (var img in board.Images)
        {
            var item = _currentGroup?.Items.FirstOrDefault(i => i.Id == img.ItemId);
            if (item is null) continue;
            var full = LoadFullImage(_store.ResolvePath(item.File));
            if (full is null) continue;
            var ctl = new BoardImageControl(full, item) { Board = Board };
            ctl.SetZoom(img.Scale);
            ctl.Annotation.LoadAnnotations(img.Strokes);
            ctl.EditRequested += OnEditImage;
            ctl.DeleteRequested += OnDeleteImageLeft;
            ctl.WriteBackRequested += OnWriteBack;
            ctl.Changed += SaveCurrentBoard;
            Board.AddImage(ctl, img.X, img.Y, img.Z);
        }
        foreach (var n in board.Notes)
        {
            var note = new NoteControl(n) { Board = Board };
            note.Changed += SaveCurrentBoard;
            Board.AddNote(note, n.X, n.Y, n.Z);
        }
    }

    // ---------- 图片标注编辑（弹窗） ----------

    private void OnEditImage(BoardImageControl ctl)
    {
        if (_editing == ctl) { FinishEdit(); return; } // 再次右键退出编辑
        FinishEdit();
        _editing = ctl;
        var win = new EditorWindow(ctl.GetBaseImage(), ctl.Annotation.Strokes);
        win.Owner = this;
        win.Closed += (_, _) =>
        {
            if (_editing == ctl)
            {
                ctl.Annotation.LoadAnnotations(win.Result);
                ctl.SaveState();
                SaveCurrentBoard();
            }
            _editing = null;
            _editorWindow = null;
        };
        _editorWindow = win;
        win.Show();
    }

    private void FinishEdit()
    {
        if (_editorWindow is not null)
        {
            var w = _editorWindow;
            _editorWindow = null;
            try { w.Close(); } catch { }
        }
        _editing = null;
        SaveCurrentBoard();
    }

    // ---------- 设置 / 关闭 ----------

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var win = new SettingsWindow(_store.Settings, () =>
        {
            _store.SaveSettings();
            var hwnd = new WindowInteropHelper(this).Handle;
            _hotkeys.Register(hwnd, _store.Settings.RegionHotkey, _store.Settings.FullScreenHotkey);
            UpdateHotkeyHint();
        });
        win.Owner = this;
        win.ShowDialog();
    }

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        // 关闭即退出进程
        FinishEdit();
        SaveCurrentBoard();
        _hotkeys.Dispose();
    }

    // ---------- 工具 ----------

    private static BitmapSource? LoadFullImage(string path)
    {
        if (!File.Exists(path)) return null;
        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.UriSource = new Uri(path, UriKind.Absolute);
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch { return null; }
    }

    private static string? Prompt(string title, string label, string initial)
    {
        var win = new Window
        {
            Title = title,
            Width = 360,
            Height = 170,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.SingleBorderWindow,
        };
        var box = new TextBox { Text = initial, Margin = new Thickness(0, 4, 0, 12) };
        var ok = new Button { Content = "确定", Width = 80, IsDefault = true };
        var cancel = new Button { Content = "取消", Width = 80, IsCancel = true };
        string? result = null;
        ok.Click += (_, _) => { result = box.Text; win.DialogResult = true; };

        var btns = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        btns.Children.Add(ok);
        btns.Children.Add(cancel);

        var sp = new StackPanel { Margin = new Thickness(16) };
        sp.Children.Add(new TextBlock { Text = label });
        sp.Children.Add(box);
        sp.Children.Add(btns);

        win.Content = sp;
        box.Focus();
        box.SelectAll();
        return win.ShowDialog() == true ? result : null;
    }
}
