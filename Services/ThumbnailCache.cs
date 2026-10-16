using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Corkboard.Models;

namespace Corkboard.Services;

/// <summary>右侧列表缩略图缓存（降采样解码，最多保留 120 张）。</summary>
public sealed class ThumbnailCache
{
    private const int MaxEntries = 120;
    private readonly StoreService _store;
    private readonly Dictionary<string, BitmapSource> _cache = new();
    private readonly LinkedList<string> _lru = new();

    public ThumbnailCache(StoreService store) => _store = store;

    public BitmapSource Get(ScreenshotItem item)
    {
        if (_cache.TryGetValue(item.Id, out var cached))
        {
            Touch(item.Id);
            return cached;
        }
        var bmp = LoadThumbnail(_store.ResolvePath(item.File)) ?? MakeBlank();
        _cache[item.Id] = bmp;
        _lru.AddFirst(item.Id);
        Trim();
        return bmp;
    }

    public void Invalidate(string id)
    {
        _cache.Remove(id);
        var node = _lru.Find(id);
        if (node != null) _lru.Remove(node);
    }

    private void Touch(string id)
    {
        var node = _lru.Find(id);
        if (node is null) return;
        _lru.Remove(node);
        _lru.AddFirst(id);
    }

    private void Trim()
    {
        while (_lru.Count > MaxEntries)
        {
            var last = _lru.Last!.Value;
            _lru.RemoveLast();
            _cache.Remove(last);
        }
    }

    private static BitmapSource MakeBlank()
    {
        var b = new WriteableBitmap(1, 1, 96, 96, PixelFormats.Bgra32, null);
        b.Freeze();
        return b;
    }

    private static BitmapSource? LoadThumbnail(string path)
    {
        if (!File.Exists(path)) return null;
        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.DecodePixelWidth = 320;
            bmp.UriSource = new Uri(path, UriKind.Absolute);
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch { return null; }
    }
}
