using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using Corkboard.Models;

namespace Corkboard.Controls;

/// <summary>白板便条：白底黑字，可拖拽、右下角缩放、双击编辑。</summary>
public sealed class NoteControl : Border
{
    public NoteState State { get; }
    public BoardCanvas? Board { get; set; }
    private readonly TextBlock _label;
    private readonly Thumb _grip;
    private readonly Grid _grid;
    private TextBox? _editor;
    private Window? _host;

    private bool _dragging;
    private Point _dragStart;
    private double _startLeft, _startTop;

    public event Action? Changed;

    public NoteControl(NoteState state)
    {
        State = state;
        Width = state.W;
        Height = state.H;
        Background = Brushes.White;
        BorderBrush = new SolidColorBrush(Color.FromRgb(0xB8, 0xB8, 0xB8));
        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(3);
        Cursor = Cursors.SizeAll;

        _label = new TextBlock
        {
            Text = state.Text,
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.Black,
            FontSize = 14,
            Margin = new Thickness(10),
        };

        _grip = new Thumb
        {
            Width = 14,
            Height = 14,
            Cursor = Cursors.SizeNWSE,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Template = MakeGripTemplate(),
        };
        _grip.DragDelta += OnResize;
        _grip.DragCompleted += (_, _) => Changed?.Invoke();

        _grid = new Grid();
        _grid.Children.Add(_label);
        _grid.Children.Add(_grip);
        Child = _grid;
    }

    protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonDown(e);
        var src = e.OriginalSource as DependencyObject;
        if (src != null && _grip.IsAncestorOf(src)) return; // 缩放手柄：交还 Thumb
        if (Board is null) return;
        if (e.ClickCount >= 2)
        {
            BeginEdit();
            e.Handled = true;
            return;
        }
        Board.BringNoteToTop(this);
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

    protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonUp(e);
        if (_dragging)
        {
            _dragging = false;
            ReleaseMouseCapture();
            Changed?.Invoke();
        }
    }

    private void OnResize(object sender, DragDeltaEventArgs e)
    {
        double nw = Math.Max(80, Width + e.HorizontalChange);
        double nh = Math.Max(40, Height + e.VerticalChange);
        Width = nw;
        Height = nh;
        State.W = nw;
        State.H = nh;
    }

    private void BeginEdit()
    {
        if (_editor != null) return;
        _label.Visibility = Visibility.Collapsed;
        _editor = new TextBox
        {
            Text = _label.Text,
            TextWrapping = TextWrapping.Wrap,
            AcceptsReturn = true,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            FontSize = 14,
            Foreground = Brushes.Black,
            Margin = new Thickness(10),
            BorderThickness = new Thickness(0),
            Background = Brushes.White,
        };
        _grid.Children.Insert(0, _editor); // 在便条手柄之下
        _editor.Focus();
        _editor.SelectAll();
        _host = Window.GetWindow(this);
        if (_host != null) _host.PreviewMouseLeftButtonDown += OnHostPreviewDown;
        _editor.LostKeyboardFocus += (_, _) => CommitEdit();
        _editor.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter && Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) CommitEdit();
            else if (e.Key == Key.Escape) CancelEdit();
        };
    }

    private void CommitEdit()
    {
        if (_editor == null) return;
        _label.Text = _editor.Text;
        State.Text = _editor.Text;
        _label.Visibility = Visibility.Visible;
        _grid.Children.Remove(_editor);
        _editor = null;
        DetachHost();
        Changed?.Invoke();
    }

    private void CancelEdit()
    {
        if (_editor == null) return;
        _label.Visibility = Visibility.Visible;
        _grid.Children.Remove(_editor);
        _editor = null;
        DetachHost();
    }

    private void DetachHost()
    {
        if (_host != null)
        {
            _host.PreviewMouseLeftButtonDown -= OnHostPreviewDown;
            _host = null;
        }
    }

    private void OnHostPreviewDown(object sender, MouseButtonEventArgs e)
    {
        var p = e.GetPosition(this);
        if (p.X < 0 || p.Y < 0 || p.X > ActualWidth || p.Y > ActualHeight)
            CommitEdit();
    }

    public void SaveState()
    {
        State.X = Canvas.GetLeft(this);
        State.Y = Canvas.GetTop(this);
        State.Z = Canvas.GetZIndex(this);
        State.W = Width;
        State.H = Height;
        State.Text = _label.Text;
    }

    private static ControlTemplate MakeGripTemplate()
    {
        const string xaml =
            "<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' " +
            "xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='{x:Type Thumb}'>" +
            "<Grid Background='Transparent'>" +
            "<Path Data='M0,11 L11,0 L11,11 Z' Fill='#999999' HorizontalAlignment='Right' VerticalAlignment='Bottom'/>" +
            "</Grid></ControlTemplate>";
        return (ControlTemplate)XamlReader.Parse(xaml);
    }
}
