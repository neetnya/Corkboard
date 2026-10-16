using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Media.Imaging;
using Corkboard.Models;

namespace Corkboard.Services;

public enum StoreLoadStatus
{
    Ok,
    CreatedNew,
    Corrupt,
}

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
    public StoreLoadStatus LoadStatus { get; private set; } = StoreLoadStatus.CreatedNew;
    public string? CorruptBackupPath { get; private set; }

    public void Initialize()
    {
        Directory.CreateDirectory(DataDirectory);
        LoadStore();
        LoadSettings();
    }

    private void LoadStore()
    {
        if (!File.Exists(StorePath))
        {
            Store = new Store();
            EnsureDefaults();
            SaveStore();
            LoadStatus = StoreLoadStatus.CreatedNew;
            return;
        }

        if (TryDeserialize<Store>(StorePath, out var loaded) && loaded is not null)
        {
            Store = loaded;
            LoadStatus = StoreLoadStatus.Ok;
            if (EnsureDefaults()) SaveStore();
            return;
        }

        // 损坏：备份后用空白仓库，本次启动不覆盖原文件
        Store = new Store();
        EnsureDefaults();
        LoadStatus = StoreLoadStatus.Corrupt;
        try
        {
            CorruptBackupPath = StorePath + ".corrupt";
            File.Copy(StorePath, CorruptBackupPath, overwrite: true);
        }
        catch
        {
            CorruptBackupPath = null;
        }
    }

    private void LoadSettings()
    {
        if (!File.Exists(SettingsPath))
        {
            Settings = new AppSettings();
            return;
        }
        if (TryDeserialize<AppSettings>(SettingsPath, out var s) && s is not null)
        {
            Settings = s;
            return;
        }
        try { File.Copy(SettingsPath, SettingsPath + ".corrupt", overwrite: true); } catch { }
        Settings = new AppSettings();
    }

    /// <summary>补齐缺省分组。返回是否改动了内存中的数据。</summary>
    private bool EnsureDefaults()
    {
        bool changed = false;
        if (Store.Groups.Count == 0)
        {
            var g = new Group { Name = "默认分组" };
            Store.Groups.Add(g);
            Store.CurrentGroupId = g.Id;
            changed = true;
        }
        if (string.IsNullOrEmpty(Store.CurrentGroupId) || Store.Groups.All(g => g.Id != Store.CurrentGroupId))
        {
            Store.CurrentGroupId = Store.Groups[0].Id;
            changed = true;
        }
        return changed;
    }

    public Group? CurrentGroup => Store.Groups.FirstOrDefault(g => g.Id == Store.CurrentGroupId);

    public void SaveStore()
    {
        Directory.CreateDirectory(DataDirectory);
        AtomicWrite(StorePath, JsonSerializer.Serialize(Store, JsonOpts));
    }

    public void SaveSettings() => AtomicWrite(SettingsPath, JsonSerializer.Serialize(Settings, JsonOpts));

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

    private static void AtomicWrite(string path, string contents)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, contents);
        if (File.Exists(path))
            File.Replace(tmp, path, destinationBackupFileName: null, ignoreMetadataErrors: true);
        else
            File.Move(tmp, path);
    }

    private static bool TryDeserialize<T>(string path, out T? value) where T : class
    {
        value = null;
        try
        {
            var json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json)) return false;
            value = JsonSerializer.Deserialize<T>(json, JsonOpts);
            return value is not null;
        }
        catch
        {
            return false;
        }
    }
}
