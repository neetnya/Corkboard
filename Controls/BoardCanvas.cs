using System;
using System.Windows;
using System.Windows.Controls;

namespace Corkboard.Controls;

/// <summary>白板画布：便条层级恒定高于图片，图片/便条各自独立排序。</summary>
public sealed class BoardCanvas : Canvas
{
    private const int NoteZBase = 10000;
    private int _imageTopZ;
    private int _noteTopZ = NoteZBase;

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

    public void ClearAll() => Children.Clear();
}
