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
    public bool IsEraseMode { get; set; }
    public Color BrushColor { get; set; } = Colors.Red;
    public double BrushThickness { get; set; } = 3.0;

    public IReadOnlyList<Stroke> Strokes => _strokes;
    public event Action? Changed;

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
        if (IsEraseMode)
        {
            if (RemoveStrokeNear(p)) Changed?.Invoke();
            e.Handled = true;
            return;
        }
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
            var last = _currentStroke.Points[^1];
            if (Math.Abs(p.X - last.X) + Math.Abs(p.Y - last.Y) < 1.5) return;
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
                {
                    _strokes.Add(_currentStroke);
                    Changed?.Invoke();
                }
            }
            _currentStroke = null;
            _currentPath = null;
        }
    }

    public bool RemoveStrokeNear(Point p)
    {
        for (int i = _strokes.Count - 1; i >= 0; i--)
        {
            var s = _strokes[i];
            double tol = s.Thickness / 2 + 8;
            if (!Hits(s, p, tol)) continue;
            _strokes.RemoveAt(i);
            RebuildVisuals();
            return true;
        }
        return false;
    }

    private void RebuildVisuals()
    {
        Children.Clear();
        foreach (var s in _strokes) Children.Add(MakePathVisual(s));
    }

    private static bool Hits(Stroke s, Point p, double tol)
    {
        for (int i = 1; i < s.Points.Count; i++)
        {
            var a = s.Points[i - 1];
            var b = s.Points[i];
            if (DistToSegment(p.X, p.Y, a.X, a.Y, b.X, b.Y) <= tol) return true;
        }
        return false;
    }

    private static double DistToSegment(double px, double py, double x1, double y1, double x2, double y2)
    {
        double dx = x2 - x1, dy = y2 - y1;
        double len2 = dx * dx + dy * dy;
        if (len2 < 1e-9) return Math.Sqrt((px - x1) * (px - x1) + (py - y1) * (py - y1));
        double t = Math.Clamp(((px - x1) * dx + (py - y1) * dy) / len2, 0, 1);
        double hx = px - (x1 + t * dx), hy = py - (y1 + t * dy);
        return Math.Sqrt(hx * hx + hy * hy);
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
