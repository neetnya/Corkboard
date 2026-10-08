using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Windows.Media.Imaging;

namespace Corkboard.Models;

/// <summary>右侧列表中的一张已保存截图。</summary>
public sealed class ScreenshotItem : INotifyPropertyChanged
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    /// <summary>相对 data/ 目录的路径，例如 groups/g1/i1.png。</summary>
    public string File { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [JsonIgnore]
    public BitmapSource? Thumbnail
    {
        get => _thumbnail;
        set { _thumbnail = value; OnPropertyChanged(); }
    }
    private BitmapSource? _thumbnail;

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>白板上一张图片的状态（含非破坏性标注）。</summary>
public sealed class BoardImageState
{
    public string ItemId { get; set; } = "";
    public double X { get; set; }
    public double Y { get; set; }
    public double Scale { get; set; } = 1.0;
    public int Z { get; set; }
    public List<Stroke> Strokes { get; set; } = new();
}

/// <summary>白板上一张便条的状态。</summary>
public sealed class NoteState
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public double X { get; set; }
    public double Y { get; set; }
    public double W { get; set; } = 180;
    public double H { get; set; } = 120;
    public int Z { get; set; }
    public string Text { get; set; } = "";
}

/// <summary>某个分组的白板布局。</summary>
public sealed class BoardState
{
    public List<BoardImageState> Images { get; set; } = new();
    public List<NoteState> Notes { get; set; } = new();
}

/// <summary>分组（对应一个文件夹）。</summary>
public sealed class Group
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "默认分组";
    public ObservableCollection<ScreenshotItem> Items { get; set; } = new();
    public BoardState Board { get; set; } = new();
}

/// <summary>store.json 的根。</summary>
public sealed class Store
{
    public int Version { get; set; } = 1;
    public string CurrentGroupId { get; set; } = "";
    public List<Group> Groups { get; set; } = new();
}
