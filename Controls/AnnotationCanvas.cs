using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Corkboard.Models;
using Corkboard.Services;

namespace Corkboard.Controls;

/// <summary>图片的标注层：画笔笔画（非破坏性）。</summary>
public sealed class AnnotationCanvas : Canvas
{
    private readonly List<Stroke> _strokes = new();

    private Stroke? _currentStroke;
    private Path? _currentPath;
    private bool _drawing;

    public bool IsBrushMode { get; set; }
    public Color BrushColor { get; set; } = Colors.Red;
    public double BrushThickness { get; set; } = 3.0;

    public IReadOnlyList<Stroke> Strokes => _strokes;

    public AnnotationCanvas()
    {
        Background = Brushes.Transparent; // 参与命中测试
        Focusable = true;
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (!IsBrushMode) return; // 非编辑模式：冒泡给父级用于拖拽
        var p = e.GetPosition(this);
        _currentStroke = new Stroke { Color = ColorUtil.ToHex(BrushColor), Thickness = BrushThickness };
        _currentStroke.Points.Add(new Pt(p.X, p.Y));
        _currentPath = MakePathVisual(_currentStroke);
        Children.Add(_currentPath);
        _drawing = true;
        CaptureMouse();
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_drawing && _currentStroke != null && _currentPath != null)
        {
            var p = e.GetPosition(this);
            _currentStroke.Points.Add(new Pt(p.X, p.Y));
            _currentPath.Data = BuildGeometry(_currentStroke);
        }
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (_drawing)
        {
            _drawing = false;
            ReleaseMouseCapture();
            if (_currentStroke != null && _currentPath != null)
            {
                if (_currentStroke.Points.Count < 2)
                    Children.Remove(_currentPath); // 点一下不算一笔
                else
                    _strokes.Add(_currentStroke);
            }
            _currentStroke = null;
            _currentPath = null;
        }
    }

    public void LoadAnnotations(IEnumerable<Stroke> strokes)
    {
        Clear();
        foreach (var s in strokes)
        {
            _strokes.Add(s);
            Children.Add(MakePathVisual(s));
        }
    }

    public void Clear()
    {
        _strokes.Clear();
        Children.Clear();
        _drawing = false;
        _currentStroke = null;
        _currentPath = null;
    }

    private static Path MakePathVisual(Stroke s)
    {
        return new Path
        {
            Data = BuildGeometry(s),
            Stroke = ColorUtil.Brush(s.Color),
            StrokeThickness = s.Thickness,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
        };
    }

    private static Geometry BuildGeometry(Stroke s)
    {
        if (s.Points.Count < 2) return Geometry.Empty;
        var g = new StreamGeometry();
        using (var ctx = g.Open())
        {
            ctx.BeginFigure(new Point(s.Points[0].X, s.Points[0].Y), false, false);
            var pts = new List<Point>(s.Points.Count);
            for (int i = 1; i < s.Points.Count; i++) pts.Add(new Point(s.Points[i].X, s.Points[i].Y));
            ctx.PolyLineTo(pts, true, false);
        }
        g.Freeze();
        return g;
    }
}
