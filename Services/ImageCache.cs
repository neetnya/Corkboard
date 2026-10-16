using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media.Imaging;

namespace Corkboard.Services;

/// <summary>原图解码缓存：同一文件只解码一次，弱引用可被 GC 收回。</summary>
public sealed class ImageCache
{
    private readonly Dictionary<string, WeakReference<BitmapSource>> _map = new(StringComparer.OrdinalIgnoreCase);

    public BitmapSource? Get(string path)
    {
        if (_map.TryGetValue(path, out var wr) && wr.TryGetTarget(out var cached))
            return cached;
        var loaded = Load(path);
        if (loaded != null) _map[path] = new WeakReference<BitmapSource>(loaded);
        return loaded;
    }

    public void Invalidate(string path) => _map.Remove(path);

    private static BitmapSource? Load(string path)
    {
        if (!File.Exists(path)) return null;
        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.UriSource = new Uri(path, UriKind.Absolute);
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch { return null; }
    }
}
