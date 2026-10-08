using System.Collections.Generic;

namespace Corkboard.Models;

/// <summary>可序列化的二维点（图像原始像素坐标系）。</summary>
public struct Pt
{
    public double X { get; set; }
    public double Y { get; set; }
    public Pt(double x, double y) { X = x; Y = y; }
}

/// <summary>画笔的一笔（非破坏性标注，坐标在图像原始像素空间）。</summary>
public sealed class Stroke
{
    public List<Pt> Points { get; set; } = new();
    public string Color { get; set; } = "#FF0000";
    public double Thickness { get; set; } = 3.0;
}
