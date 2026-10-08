using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace Corkboard.Controls;

/// <summary>框选截图的全屏遮罩窗口，选区松手即完成（返回物理像素矩形）。</summary>
public sealed class RegionSelectorWindow : Window
{
    private Point _startDip;
    private bool _selecting;
    private readonly Canvas _canvas;
    private readonly Path _mask;
    private readonly Rectangle _selRect;

    /// <summary>选区完成（参数为屏幕物理像素矩形）。</summary>
    public event Action<Rect>? RegionSelected;
    public event Action? Cancelled;

    public RegionSelectorWindow()
    {
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        Cursor = Cursors.Cross;
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;

        _canvas = new Canvas();
        _mask = new Path
        {
            Fill = new SolidColorBrush(Color.FromArgb(0x78, 0, 0, 0)),
            Data = FullScreenGeometry(),
        };
        _selRect = new Rectangle
        {
            Stroke = Brushes.Red,
            StrokeThickness = 1.5,
            StrokeDashArray = new DoubleCollection { 3, 2 },
            Fill = Brushes.Transparent,
            Visibility = Visibility.Collapsed,
        };
        _canvas.Children.Add(_mask);
        _canvas.Children.Add(_selRect);
        Content = _canvas;
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        _startDip = e.GetPosition(this);
        _selecting = true;
        _selRect.Visibility = Visibility.Visible;
        UpdateSel(e.GetPosition(this));
        CaptureMouse();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_selecting) UpdateSel(e.GetPosition(this));
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (!_selecting) return;
        _selecting = false;
        ReleaseMouseCapture();

        var startPx = PointToScreen(_startDip);
        var endPx = PointToScreen(e.GetPosition(this));
        int x = (int)Math.Min(startPx.X, endPx.X);
        int y = (int)Math.Min(startPx.Y, endPx.Y);
        int w = (int)Math.Abs(startPx.X - endPx.X);
        int h = (int)Math.Abs(startPx.Y - endPx.Y);

        Close();
        if (w < 4 || h < 4) { Cancelled?.Invoke(); return; }
        RegionSelected?.Invoke(new Rect(x, y, w, h));
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape)
        {
            Close();
            Cancelled?.Invoke();
        }
    }

    private void UpdateSel(Point current)
    {
        double x = Math.Min(_startDip.X, current.X);
        double y = Math.Min(_startDip.Y, current.Y);
        double w = Math.Abs(current.X - _startDip.X);
        double h = Math.Abs(current.Y - _startDip.Y);
        Canvas.SetLeft(_selRect, x);
        Canvas.SetTop(_selRect, y);
        _selRect.Width = w;
        _selRect.Height = h;
        _mask.Data = HoleGeometry(new Rect(x, y, w, h));
    }

    private Geometry FullScreenGeometry()
        => new RectangleGeometry(new Rect(0, 0, Width, Height));

    private Geometry HoleGeometry(Rect hole)
    {
        var full = new RectangleGeometry(new Rect(0, 0, Width, Height));
        var h = new RectangleGeometry(hole);
        var cg = new CombinedGeometry(GeometryCombineMode.Exclude, full, h);
        cg.Freeze();
        return cg;
    }
}
