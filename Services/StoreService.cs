using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Media.Imaging;
using Corkboard.Models;

namespace Corkboard.Services;

/// <summary>负责 store.json / settings.json / 分组文件夹 / PNG 的读写。</summary>
public sealed class StoreService
{
    public string DataDirectory { get; } = Path.Combine(AppContext.BaseDirectory, "data");
    public string StorePath => Path.Combine(DataDirectory, "store.json");
    public string SettingsPath => Path.Combine(AppContext.BaseDirectory, "settings.json");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public Store Store { get; private set; } = new();
    public AppSettings Settings { get; private set; } = new();

    public void Initialize()
    {
        Directory.CreateDirectory(DataDirectory);
        Store = Load<Store>(StorePath) ?? new Store();
        Settings = Load<AppSettings>(SettingsPath) ?? new AppSettings();
        EnsureDefaults();
    }

    private void EnsureDefaults()
    {
        if (Store.Groups.Count == 0)
        {
            var g = new Group { Name = "默认分组" };
            Store.Groups.Add(g);
            Store.CurrentGroupId = g.Id;
        }
        if (string.IsNullOrEmpty(Store.CurrentGroupId) || Store.Groups.All(g => g.Id != Store.CurrentGroupId))
            Store.CurrentGroupId = Store.Groups[0].Id;
        SaveStore();
    }

    public Group? CurrentGroup => Store.Groups.FirstOrDefault(g => g.Id == Store.CurrentGroupId);

    public void SaveStore()
    {
        Directory.CreateDirectory(DataDirectory);
        File.WriteAllText(StorePath, JsonSerializer.Serialize(Store, JsonOpts));
    }

    public void SaveSettings() => File.WriteAllText(SettingsPath, JsonSerializer.Serialize(Settings, JsonOpts));

    public string ResolvePath(string relative)
        => Path.Combine(DataDirectory, relative.Replace('/', Path.DirectorySeparatorChar));

    public ScreenshotItem AddScreenshot(Group group, BitmapSource bmp)
    {
        var id = Guid.NewGuid().ToString("N");
        var dir = Path.Combine(DataDirectory, "groups", group.Id);
        Directory.CreateDirectory(dir);
        var rel = $"groups/{group.Id}/{id}.png";
        SavePng(bmp, ResolvePath(rel));
        var item = new ScreenshotItem { Id = id, File = rel };
        group.Items.Add(item);
        SaveStore();
        return item;
    }

    public void DeleteScreenshot(Group group, ScreenshotItem item)
    {
        var abs = ResolvePath(item.File);
        if (File.Exists(abs)) { try { File.Delete(abs); } catch { /* 忽略占用等异常 */ } }
        group.Items.Remove(item);
        SaveStore();
    }

    public void DeleteGroup(Group group)
    {
        var dir = Path.Combine(DataDirectory, "groups", group.Id);
        if (Directory.Exists(dir)) { try { Directory.Delete(dir, true); } catch { } }
        Store.Groups.Remove(group);
        if (Store.Groups.Count == 0) { var g = new Group { Name = "默认分组" }; Store.Groups.Add(g); }
        if (Store.CurrentGroupId == group.Id) Store.CurrentGroupId = Store.Groups[0].Id;
        SaveStore();
    }

    public static void SavePng(BitmapSource bmp, string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bmp));
        using var fs = File.Create(path);
        encoder.Save(fs);
    }

    private static T? Load<T>(string path) where T : class
    {
        if (!File.Exists(path)) return null;
        try { return JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonOpts); }
        catch { return null; }
    }
}
