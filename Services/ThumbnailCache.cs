using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Corkboard.Models;

namespace Corkboard.Services;

/// <summary>右侧列表缩略图缓存（降采样解码）。</summary>
public sealed class ThumbnailCache
{
    private readonly StoreService _store;
    private readonly Dictionary<string, BitmapSource> _cache = new();

    public ThumbnailCache(StoreService store) => _store = store;

    public BitmapSource Get(ScreenshotItem item)
    {
        if (_cache.TryGetValue(item.Id, out var cached)) return cached;
        var bmp = LoadThumbnail(_store.ResolvePath(item.File));
        if (bmp is null) bmp = MakeBlank();
        _cache[item.Id] = bmp;
        return bmp;
    }

    public void Invalidate(string id) => _cache.Remove(id);

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
