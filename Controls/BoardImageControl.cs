using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Corkboard.Models;

namespace Corkboard.Controls;

/// <summary>白板上的图片：可拖拽、滚轮缩放（锚点）、置顶、黑色边框、右键编辑/删除/写回。</summary>
public sealed class BoardImageControl : UserControl
{
    private readonly Grid _host;
    private readonly Grid _contentRoot;
    private readonly Border _frame;
    private readonly ScaleTransform _scaleTransform = new(1.0, 1.0);
    private readonly Image _baseImage;

    public AnnotationCanvas Annotation { get; }
    public ScreenshotItem Screenshot { get; }
    public BoardImageState State { get; } = new();
    public BoardCanvas? Board { get; set; }

    private bool _dragging;
    private Point _dragStart;
    private double _startLeft, _startTop;

    public event Action<BoardImageControl>? EditRequested;
    public event Action<BoardImageControl>? DeleteRequested;
    public event Action<BoardImageControl>? WriteBackRequested;
    public event Action? Changed;

    public BoardImageControl(BitmapSource image, ScreenshotItem item)
    {
        Screenshot = item;
        _baseImage = new Image { Source = image, Stretch = Stretch.Fill };
        Annotation = new AnnotationCanvas();

        _contentRoot = new Grid
        {
            Width = image.PixelWidth,
            Height = image.PixelHeight,
            RenderTransform = _scaleTransform,
        };
        _contentRoot.Children.Add(_baseImage);
        _contentRoot.Children.Add(Annotation);

        // 黑色边框：固定屏幕粗细（不随缩放），不拦截鼠标
        _frame = new Border
        {
            BorderBrush = Brushes.Black,
            BorderThickness = new Thickness(1),
            IsHitTestVisible = false,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };

        _host = new Grid { Width = image.PixelWidth, Height = image.PixelHeight };
        _host.Children.Add(_contentRoot);
        _host.Children.Add(_frame);
        Content = _host;
        Focusable = true;
        UpdateFrameSize();

        var menu = new ContextMenu();
        var edit = new MenuItem { Header = "编辑" };
        edit.Click += (_, _) => EditRequested?.Invoke(this);
        var write = new MenuItem { Header = "写入右侧" };
        write.Click += (_, _) => WriteBackRequested?.Invoke(this);
        var del = new MenuItem { Header = "删除" };
        del.Click += (_, _) => DeleteRequested?.Invoke(this);
        menu.Items.Add(edit);
        menu.Items.Add(write);
        menu.Items.Add(new Separator());
        menu.Items.Add(del);
        ContextMenu = menu;
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (Board is null) return;
        Board.BringImageToTop(this);
        _dragStart = e.GetPosition(Board);
        _startLeft = Canvas.GetLeft(this);
        _startTop = Canvas.GetTop(this);
        _dragging = true;
        CaptureMouse();
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!_dragging || Board is null) return;
        var p = e.GetPosition(Board);
        Canvas.SetLeft(this, _startLeft + (p.X - _dragStart.X));
        Canvas.SetTop(this, _startTop + (p.Y - _dragStart.Y));
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (_dragging)
        {
            _dragging = false;
            ReleaseMouseCapture();
            Changed?.Invoke();
        }
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        if (Board is null) return;
        var p = e.GetPosition(Board);
        double factor = e.Delta > 0 ? 1.1 : 1.0 / 1.1;
        double oldScale = _scaleTransform.ScaleX;
        double newScale = Math.Clamp(oldScale * factor, 0.1, 8.0);
        if (Math.Abs(newScale - oldScale) < 1e-9) { e.Handled = true; return; }
        double left = Canvas.GetLeft(this);
        double top = Canvas.GetTop(this);
        double cx = (p.X - left) / oldScale;
        double cy = (p.Y - top) / oldScale;
        Canvas.SetLeft(this, p.X - newScale * cx);
        Canvas.SetTop(this, p.Y - newScale * cy);
        _scaleTransform.ScaleX = newScale;
        _scaleTransform.ScaleY = newScale;
        UpdateFrameSize();
        e.Handled = true;
    }

    public double GetZoom() => _scaleTransform.ScaleX;

    public void SetZoom(double z)
    {
        z = Math.Clamp(z, 0.1, 8.0);
        _scaleTransform.ScaleX = z;
        _scaleTransform.ScaleY = z;
        UpdateFrameSize();
    }

    public void SetBaseImage(BitmapSource img)
    {
        _baseImage.Source = img;
        _contentRoot.Width = img.PixelWidth;
        _contentRoot.Height = img.PixelHeight;
        _host.Width = img.PixelWidth;
        _host.Height = img.PixelHeight;
        UpdateFrameSize();
    }

    public BitmapSource GetBaseImage() => (BitmapSource)_baseImage.Source;

    public void SetBrushMode(bool enabled, Color color, double thickness)
    {
        Annotation.IsBrushMode = enabled;
        Annotation.BrushColor = color;
        Annotation.BrushThickness = thickness;
    }

    public void SaveState()
    {
        State.ItemId = Screenshot.Id;
        State.X = Canvas.GetLeft(this);
        State.Y = Canvas.GetTop(this);
        State.Z = Canvas.GetZIndex(this);
        State.Scale = _scaleTransform.ScaleX;
        State.Strokes.Clear();
        State.Strokes.AddRange(Annotation.Strokes);
    }

    /// <summary>扁平合成：以原始像素分辨率渲染「底图 + 标注」（不含边框）。</summary>
    public BitmapSource RenderFlattened()
    {
        double saved = _scaleTransform.ScaleX;
        _scaleTransform.ScaleX = _scaleTransform.ScaleY = 1.0;
        UpdateLayout();
        int w = Math.Max(1, (int)_contentRoot.Width);
        int h = Math.Max(1, (int)_contentRoot.Height);
        var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(_contentRoot);
        _scaleTransform.ScaleX = _scaleTransform.ScaleY = saved;
        UpdateFrameSize();
        var bmp = BitmapFrame.Create(rtb);
        bmp.Freeze();
        return bmp;
    }

    private void UpdateFrameSize()
    {
        _frame.Width = _contentRoot.Width * _scaleTransform.ScaleX;
        _frame.Height = _contentRoot.Height * _scaleTransform.ScaleY;
    }
}
