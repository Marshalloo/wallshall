using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Win32;

class AppSettings
{
    public static readonly string AppDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Wallshall");
    public static string DefaultCacheDir => Path.Combine(AppDir, "cache");
    static readonly string FilePath = Path.Combine(AppDir, "settings.json");

    // Ключ хранится зашифрованным (DPAPI, расшифровать может только текущий пользователь Windows)
    public string ApiKeyProtected { get; set; } = "";

    [JsonIgnore]
    public string ApiKey
    {
        get => Unprotect(ApiKeyProtected);
        set => ApiKeyProtected = Protect(value.Trim());
    }

    // Категории
    public bool General { get; set; } = true;
    public bool Anime { get; set; } = true;
    public bool People { get; set; } = true;

    // Контент
    public bool Sfw { get; set; } = true;
    public bool Sketchy { get; set; } = false;
    public bool Nsfw { get; set; } = false;   // требует API-ключ

    public string TopRange { get; set; } = "1M";
    public string AtLeast { get; set; } = "1920x1080";   // "" = любое
    public string Ratios { get; set; } = "16x9";         // "" = любые
    public int IntervalMinutes { get; set; } = 15;
    public string CacheDir { get; set; } = DefaultCacheDir;

    public string BuildQuery()
    {
        bool nsfw = Nsfw && ApiKey != "";
        var q = $"sorting=toplist&topRange={TopRange}" +
                $"&categories={B(General)}{B(Anime)}{B(People)}" +
                $"&purity={B(Sfw)}{B(Sketchy)}{B(nsfw)}";
        if (AtLeast != "") q += "&atleast=" + Uri.EscapeDataString(AtLeast);
        if (Ratios != "") q += "&ratios=" + Uri.EscapeDataString(Ratios);
        return q;
    }

    static char B(bool b) => b ? '1' : '0';

    public AppSettings Clone() =>
        JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(this))!;

    public static AppSettings Load()
    {
        MigrateFromOldName();
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new();
        }
        catch { }
        return new();
    }

    public void Save()
    {
        Directory.CreateDirectory(AppDir);
        var tmp = FilePath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(tmp, FilePath, overwrite: true);
    }

    /// Перенос данных со старого названия WallhavenTray (один раз).
    static void MigrateFromOldName()
    {
        var oldDir = Path.Combine(Path.GetDirectoryName(AppDir)!, "WallhavenTray");
        if (!Directory.Exists(oldDir) || Directory.Exists(AppDir)) return;
        try
        {
            Directory.Move(oldDir, AppDir);
            // Если кэш лежал в старой папке по умолчанию — поправить путь в настройках
            var file = Path.Combine(AppDir, "settings.json");
            if (File.Exists(file))
                File.WriteAllText(file, File.ReadAllText(file).Replace(
                    JsonSerializer.Serialize(oldDir).Trim('"'), JsonSerializer.Serialize(AppDir).Trim('"')));
            Autostart.RemoveLegacy();
        }
        catch { }
    }

    static string Protect(string s) => s == "" ? "" :
        Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(s), null, DataProtectionScope.CurrentUser));

    static string Unprotect(string s)
    {
        if (s == "") return "";
        try
        {
            return Encoding.UTF8.GetString(
                ProtectedData.Unprotect(Convert.FromBase64String(s), null, DataProtectionScope.CurrentUser));
        }
        catch { return ""; }
    }
}

static class Autostart
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string Name = "Wallshall";

    public static bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(Name) != null;
        }
        set
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (key == null) return;
            if (value) key.SetValue(Name, $"\"{Environment.ProcessPath}\"");
            else key.DeleteValue(Name, throwOnMissingValue: false);
        }
    }

    /// Убирает автозапуск под старым названием; если он был — включает под новым.
    public static void RemoveLegacy()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        if (key?.GetValue("WallhavenTray") == null) return;
        key.DeleteValue("WallhavenTray", throwOnMissingValue: false);
        IsEnabled = true;
    }
}
