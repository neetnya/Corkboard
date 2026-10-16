using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Corkboard.Models;

namespace Corkboard.Controls;

/// <summary>图片编辑窗口：缩放、平移、画笔、橡皮、撤销。关闭时提交笔画。</summary>
public sealed class EditorWindow : Window
{
    private readonly AnnotationCanvas _annotation;
    private readonly Canvas _canvas;
    private readonly Grid _viewport;
    private readonly ScaleTransform _scale = new(1.0, 1.0);
    private readonly List<List<Stroke>> _history = new();
    private readonly List<Border> _swatches = new();
    private Button? _eraseBtn;
    private int _historyIndex = -1;
    private bool _userZoomed;
    private bool _panning;
    private Point _panStart;
    private double _marginX, _marginY;
    private Color _color = Colors.Red;

    public IReadOnlyList<Stroke> Result => _annotation.Strokes;

    public EditorWindow(BitmapSource baseImage, IEnumerable<Stroke> existing)
    {
        Title = "编辑";
        Width = 1040;
        Height = 780;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _annotation = new AnnotationCanvas
        {
            IsBrushMode = true,
            BrushColor = Colors.Red,
            BrushThickness = 3.0,
            Width = baseImage.PixelWidth,
            Height = baseImage.PixelHeight,
        };
        _annotation.LoadAnnotations(existing);
        _annotation.Changed += PushHistory;
        PushHistory();

        var image = new Image
        {
            Source = baseImage,
            Stretch = Stretch.Fill,
            Width = baseImage.PixelWidth,
            Height = baseImage.PixelHeight,
        };

        _canvas = new Canvas
        {
            Width = baseImage.PixelWidth,
            Height = baseImage.PixelHeight,
            RenderTransform = _scale,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };
        _canvas.Children.Add(image);
        _canvas.Children.Add(_annotation);

        _viewport = new Grid { ClipToBounds = true, Background = Brushes.LightGray };
        _viewport.Children.Add(_canvas);
        _viewport.MouseWheel += OnWheel;
        _viewport.MouseDown += OnPanDown;
        _viewport.MouseMove += OnPanMove;
        _viewport.MouseUp += OnPanUp;

        var dock = new DockPanel();
        var toolbar = BuildToolbar();
        DockPanel.SetDock(toolbar, Dock.Top);
        dock.Children.Add(toolbar);
        dock.Children.Add(_viewport);
        Content = dock;

        Loaded += (_, _) => Fit();
        SizeChanged += (_, _) => { if (!_userZoomed) Fit(); };
        PreviewKeyDown += OnKey;
    }

    private UIElement BuildToolbar()
    {
        var colorPanel = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };
        var colors = new[] { Colors.Red, Colors.Blue, Colors.Green, Colors.Black, Colors.Yellow, Colors.White, Colors.Orange, Colors.Purple };
        foreach (var c in colors)
        {
            var cc = c;
            var sw = new Border
            {
                Width = 22,
                Height = 22,
                Background = new SolidColorBrush(c),
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3),
                Margin = new Thickness(0, 0, 6, 0),
                Cursor = Cursors.Hand,
                ToolTip = cc.ToString(),
            };
            sw.MouseLeftButtonDown += (_, _) => SelectColor(cc);
            colorPanel.Children.Add(sw);
            _swatches.Add(sw);
        }
        SelectColor(Colors.Red);

        var slider = new Slider { Minimum = 1, Maximum = 12, Value = 3, Width = 120, Height = 20, VerticalAlignment = VerticalAlignment.Center };
        var label = new TextBlock { Text = "3", Margin = new Thickness(6, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        slider.ValueChanged += (_, e) =>
        {
            label.Text = ((int)e.NewValue).ToString();
            _annotation.BrushThickness = e.NewValue;
        };

        var undo = new Button { Content = "撤销", MinWidth = 64, Margin = new Thickness(14, 0, 0, 0), ToolTip = "Ctrl+Z" };
        undo.Click += (_, _) => Undo();
        var redo = new Button { Content = "重做", MinWidth = 64, Margin = new Thickness(6, 0, 0, 0), ToolTip = "Ctrl+Y" };
        redo.Click += (_, _) => Redo();
        _eraseBtn = new Button { Content = "橡皮", MinWidth = 64, Margin = new Thickness(6, 0, 0, 0) };
        _eraseBtn.Click += (_, _) => ToggleErase();
        UpdateEraseButton();
        var fit = new Button { Content = "适应窗口", MinWidth = 76, Margin = new Thickness(6, 0, 0, 0) };
        fit.Click += (_, _) => { _userZoomed = false; Fit(); };
        var finish = new Button { Content = "完成", MinWidth = 76, Margin = new Thickness(18, 0, 0, 0) };
        finish.Click += (_, _) => Close();

        var hint = new TextBlock
        {
            Text = "滚轮缩放 · 中键拖动画布",
            Foreground = Brushes.Gray,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(16, 0, 0, 0),
        };

        var bar = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        bar.Children.Add(new TextBlock { Text = "颜色", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
        bar.Children.Add(colorPanel);
        bar.Children.Add(new TextBlock { Text = "粗细", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(14, 0, 4, 0) });
        bar.Children.Add(slider);
        bar.Children.Add(label);
        bar.Children.Add(undo);
        bar.Children.Add(redo);
        bar.Children.Add(_eraseBtn);
        bar.Children.Add(fit);
        bar.Children.Add(finish);
        bar.Children.Add(hint);

        return new Border
        {
            BorderBrush = Brushes.LightGray,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Background = Brushes.WhiteSmoke,
            Padding = new Thickness(12, 8, 12, 8),
            Child = bar,
        };
    }

    private void SelectColor(Color c)
    {
        _color = c;
        _annotation.BrushColor = c;
        _annotation.IsEraseMode = false;
        UpdateEraseButton();
        foreach (var sw in _swatches)
        {
            bool on = sw.Background is SolidColorBrush b && b.Color == c;
            sw.BorderBrush = on ? Brushes.DodgerBlue : Brushes.Gray;
            sw.BorderThickness = new Thickness(on ? 2 : 1);
        }
    }

    private void ToggleErase()
    {
        _annotation.IsEraseMode = !_annotation.IsEraseMode;
        UpdateEraseButton();
    }

    private void UpdateEraseButton()
    {
        if (_eraseBtn is null) return;
        _eraseBtn.Background = _annotation.IsEraseMode ? Brushes.Gainsboro : Brushes.White;
        _canvas.Cursor = _annotation.IsEraseMode ? Cursors.Cross : Cursors.Pen;
    }

    private void PushHistory()
    {
        if (_historyIndex >= 0 && _historyIndex < _history.Count - 1)
            _history.RemoveRange(_historyIndex + 1, _history.Count - _historyIndex - 1);
        _history.Add(Clone(_annotation.Strokes));
        if (_history.Count > 50) { _history.RemoveAt(0); }
        _historyIndex = _history.Count - 1;
    }

    private void Undo()
    {
        if (_historyIndex <= 0) return;
        _historyIndex--;
        ApplyHistory();
    }

    private void Redo()
    {
        if (_historyIndex >= _history.Count - 1) return;
        _historyIndex++;
        ApplyHistory();
    }

    private void ApplyHistory()
    {
        _annotation.Changed -= PushHistory;
        _annotation.LoadAnnotations(_history[_historyIndex]);
        _annotation.Changed += PushHistory;
    }

    private void OnKey(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Z && Keyboard.Modifiers == ModifierKeys.Control) { Undo(); e.Handled = true; }
        else if (e.Key == Key.Y && Keyboard.Modifiers == ModifierKeys.Control) { Redo(); e.Handled = true; }
        else if (e.Key == Key.Escape) { Close(); e.Handled = true; }
    }

    private void OnWheel(object sender, MouseWheelEventArgs e)
    {
        _userZoomed = true;
        var p = e.GetPosition(_viewport);
        double factor = e.Delta > 0 ? 1.1 : 1.0 / 1.1;
        double old = _scale.ScaleX;
        double neu = Math.Clamp(old * factor, 0.05, 8.0);
        if (Math.Abs(neu - old) < 1e-9) { e.Handled = true; return; }
        double localX = (p.X - _marginX) / old;
        double localY = (p.Y - _marginY) / old;
        _scale.ScaleX = _scale.ScaleY = neu;
        _marginX = p.X - localX * neu;
        _marginY = p.Y - localY * neu;
        _canvas.Margin = new Thickness(_marginX, _marginY, 0, 0);
        e.Handled = true;
    }

    private void OnPanDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Middle && e.ChangedButton != MouseButton.Right) return;
        _panning = true;
        _panStart = e.GetPosition(_viewport);
        _viewport.CaptureMouse();
        _viewport.Cursor = Cursors.SizeAll;
        e.Handled = true;
    }

    private void OnPanMove(object sender, MouseEventArgs e)
    {
        if (!_panning) return;
        var p = e.GetPosition(_viewport);
        _marginX += p.X - _panStart.X;
        _marginY += p.Y - _panStart.Y;
        _panStart = p;
        _canvas.Margin = new Thickness(_marginX, _marginY, 0, 0);
    }

    private void OnPanUp(object sender, MouseButtonEventArgs e)
    {
        if (!_panning) return;
        _panning = false;
        _viewport.ReleaseMouseCapture();
        _viewport.Cursor = Cursors.Arrow;
    }

    private void Fit()
    {
        double availW = Math.Max(120, _viewport.ActualWidth - 24);
        double availH = Math.Max(120, _viewport.ActualHeight - 24);
        double s = Math.Min(availW / _canvas.Width, availH / _canvas.Height);
        s = Math.Clamp(s, 0.05, 8.0);
        _scale.ScaleX = _scale.ScaleY = s;
        _marginX = Math.Max(0, (availW - _canvas.Width * s) / 2);
        _marginY = Math.Max(0, (availH - _canvas.Height * s) / 2);
        _canvas.Margin = new Thickness(_marginX, _marginY, 0, 0);
    }

    private static List<Stroke> Clone(IReadOnlyList<Stroke> src)
        => src.Select(s => new Stroke
        {
            Color = s.Color,
            Thickness = s.Thickness,
            Points = new List<Pt>(s.Points),
        }).ToList();
}
