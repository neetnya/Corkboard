using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Corkboard.Models;

namespace Corkboard.Controls;

/// <summary>图片编辑窗口：在独立窗口内显示图片并提供画笔编辑，关闭时提交笔画。</summary>
public sealed class EditorWindow : Window
{
    private readonly AnnotationCanvas _annotation;
    private readonly Canvas _canvas;
    private readonly Grid _viewport;
    private readonly ScaleTransform _scale = new(1.0, 1.0);

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

        var dock = new DockPanel();
        var toolbar = BuildToolbar();
        DockPanel.SetDock(toolbar, Dock.Top);
        dock.Children.Add(toolbar);
        dock.Children.Add(_viewport);
        Content = dock;

        Loaded += (_, _) => Fit();
        SizeChanged += (_, _) => Fit();
    }

    private UIElement BuildToolbar()
    {
        var colorPanel = new WrapPanel { Width = 224, VerticalAlignment = VerticalAlignment.Center };
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
            sw.MouseLeftButtonDown += (_, _) => _annotation.BrushColor = cc;
            colorPanel.Children.Add(sw);
        }

        var slider = new Slider { Minimum = 1, Maximum = 12, Value = 3, Width = 120, Height = 20, VerticalAlignment = VerticalAlignment.Center };
        var label = new TextBlock { Text = "3", Margin = new Thickness(6, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        slider.ValueChanged += (_, e) =>
        {
            label.Text = ((int)e.NewValue).ToString();
            _annotation.BrushThickness = e.NewValue;
        };

        var finish = new Button { Content = "完成", MinWidth = 76, Margin = new Thickness(18, 0, 0, 0) };
        finish.Click += (_, _) => Close();

        var bar = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        bar.Children.Add(new TextBlock { Text = "颜色", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
        bar.Children.Add(colorPanel);
        bar.Children.Add(new TextBlock { Text = "粗细", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(14, 0, 4, 0) });
        bar.Children.Add(slider);
        bar.Children.Add(label);
        bar.Children.Add(finish);

        return new Border
        {
            BorderBrush = Brushes.LightGray,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Background = Brushes.WhiteSmoke,
            Padding = new Thickness(12, 8, 12, 8),
            Child = bar,
        };
    }

    private void Fit()
    {
        double availW = Math.Max(120, _viewport.ActualWidth - 24);
        double availH = Math.Max(120, _viewport.ActualHeight - 24);
        double s = Math.Min(availW / _canvas.Width, availH / _canvas.Height);
        s = Math.Clamp(s, 0.05, 8.0);
        _scale.ScaleX = s;
        _scale.ScaleY = s;
        _canvas.Margin = new Thickness(
            Math.Max(0, (availW - _canvas.Width * s) / 2),
            Math.Max(0, (availH - _canvas.Height * s) / 2),
            0, 0);
    }
}
