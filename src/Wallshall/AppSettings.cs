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
    public static string DefaultFavoritesDir => Path.Combine(AppDir, "favorites");
    static readonly string FilePath = Path.Combine(AppDir, "settings.json");

    public string ApiKeyProtected { get; set; } = "";

    [JsonIgnore]
    public string ApiKey
    {
        get => Unprotect(ApiKeyProtected);
        set => ApiKeyProtected = Protect(value.Trim());
    }

    public bool General { get; set; } = true;
    public bool Anime { get; set; } = true;
    public bool People { get; set; } = true;

    public bool Sfw { get; set; } = true;
    public bool Sketchy { get; set; } = false;
    public bool Nsfw { get; set; } = false;

    public string TopRange { get; set; } = "1M";
    public string AtLeast { get; set; } = "1920x1080";
    public string Ratios { get; set; } = "16x9";

    /// Пусто в файле — настройки от старой версии, где был только список Ratios.
    public string? RatioMode { get; set; }

    [JsonIgnore]
    public string Mode
    {
        get => RatioMode ?? (Ratios == "" ? RatioSelection.ModeAny : RatioSelection.ModeCustom);
        set => RatioMode = value;
    }

    /// Формы экранов подставляет приложение: сюда не тянем ни COM, ни WinForms.
    public static Func<string> ScreenRatios { get; set; } = () => "";
    public int IntervalMinutes { get; set; } = 15;
    public bool PerMonitor { get; set; } = false;
    public bool FavoritesOnly { get; set; } = false;
    public string CacheDir { get; set; } = DefaultCacheDir;
    public string FavoritesDir { get; set; } = DefaultFavoritesDir;

    public string BuildQuery()
    {
        bool nsfw = Nsfw && ApiKey != "";
        var q = $"sorting=toplist&topRange={TopRange}" +
                $"&categories={B(General)}{B(Anime)}{B(People)}" +
                $"&purity={B(Sfw)}{B(Sketchy)}{B(nsfw)}";
        if (AtLeast != "") q += "&atleast=" + Uri.EscapeDataString(AtLeast);

        var ratios = RatioSelection.Query(Mode, Ratios, ScreenRatios());
        if (ratios != "") q += "&ratios=" + string.Join(",", RatioSelection.Parse(ratios).Select(Uri.EscapeDataString));

        return q;
    }

    static char B(bool b) => b ? '1' : '0';

    /// Новая установка: пропорции берём от экрана. У старых настроек режима нет,
    /// и там остаётся их список, иначе фильтр сменился бы сам собой.
    static AppSettings Fresh() => new() { RatioMode = RatioSelection.ModeScreen, Ratios = "" };

    public AppSettings Clone() =>
        JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(this))!;

    public static AppSettings Load()
    {
        MigrateFromOldName();
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? Fresh();
        }
        catch { }
        return Fresh();
    }

    public void Save()
    {
        Directory.CreateDirectory(AppDir);
        var tmp = FilePath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(tmp, FilePath, overwrite: true);
    }

    static void MigrateFromOldName()
    {
        var oldDir = Path.Combine(Path.GetDirectoryName(AppDir)!, "WallhavenTray");
        if (!Directory.Exists(oldDir) || Directory.Exists(AppDir)) return;
        try
        {
            Directory.Move(oldDir, AppDir);

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
