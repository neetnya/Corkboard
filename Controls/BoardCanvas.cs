using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Corkboard.Controls;

/// <summary>白板画布：便条层级恒定高于图片；中键或空白处拖拽平移视图。</summary>
public sealed class BoardCanvas : Canvas
{
    private const int NoteZBase = 10000;
    private int _imageTopZ;
    private int _noteTopZ = NoteZBase;
    private readonly TranslateTransform _view = new();
    private bool _panning;
    private Point _panStart;
    private double _startX, _startY;

    public BoardCanvas()
    {
        RenderTransform = _view;
        Background = Brushes.Transparent;
    }

    public double ViewX
    {
        get => _view.X;
        set => _view.X = value;
    }

    public double ViewY
    {
        get => _view.Y;
        set => _view.Y = value;
    }

    public event Action? ViewChanged;

    public void BringImageToTop(BoardImageControl el) => SetZIndex(el, ++_imageTopZ);
    public void BringNoteToTop(NoteControl el) => SetZIndex(el, ++_noteTopZ);

    /// <summary>新增图片：置顶于图片层。</summary>
    public void AddImageOnTop(BoardImageControl el, double x, double y)
    {
        SetLeft(el, x);
        SetTop(el, y);
        SetZIndex(el, ++_imageTopZ);
        Children.Add(el);
    }

    /// <summary>新增便条：置顶于便条层（恒高于所有图片）。</summary>
    public void AddNoteOnTop(NoteControl el, double x, double y)
    {
        SetLeft(el, x);
        SetTop(el, y);
        SetZIndex(el, ++_noteTopZ);
        Children.Add(el);
    }

    /// <summary>恢复已保存的图片层级。</summary>
    public void AddImage(BoardImageControl el, double x, double y, int z)
    {
        SetLeft(el, x);
        SetTop(el, y);
        z = Math.Clamp(z, 0, NoteZBase - 1);
        SetZIndex(el, z);
        _imageTopZ = Math.Max(_imageTopZ, z);
        Children.Add(el);
    }

    /// <summary>恢复已保存的便条层级。</summary>
    public void AddNote(NoteControl el, double x, double y, int z)
    {
        SetLeft(el, x);
        SetTop(el, y);
        z = Math.Max(NoteZBase, z);
        SetZIndex(el, z);
        _noteTopZ = Math.Max(_noteTopZ, z);
        Children.Add(el);
    }

    public void RemoveItem(FrameworkElement el) => Children.Remove(el);

    public void ClearAll()
    {
        Children.Clear();
        _imageTopZ = 0;
        _noteTopZ = NoteZBase;
        ViewX = 0;
        ViewY = 0;
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (e.Source != this) return;
        BeginPan(e);
        e.Handled = true;
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.ChangedButton != MouseButton.Middle) return;
        BeginPan(e);
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!_panning) return;
        var p = e.GetPosition(PanSpace);
        ViewX = _startX + (p.X - _panStart.X);
        ViewY = _startY + (p.Y - _panStart.Y);
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e) => EndPan();

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Middle) EndPan();
    }

    private IInputElement PanSpace => Parent as IInputElement ?? this;

    private void BeginPan(MouseEventArgs e)
    {
        _panning = true;
        _panStart = e.GetPosition(PanSpace);
        _startX = ViewX;
        _startY = ViewY;
        CaptureMouse();
        Cursor = Cursors.SizeAll;
    }

    private void EndPan()
    {
        if (!_panning) return;
        _panning = false;
        ReleaseMouseCapture();
        Cursor = Cursors.Arrow;
        ViewChanged?.Invoke();
    }
}
