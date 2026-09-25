using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Win32;

class State
{
    public int Page { get; set; } = 1;
    public string Date { get; set; } = "";
    public List<string> Used { get; set; } = new();   // имена показанных файлов
}

class TrayApp : ApplicationContext
{
    const string ApiUrl = "https://wallhaven.cc/api/v1/search";
    const int MaxPagesPerRun = 10;      // сколько страниц пролистать за раз в поиске новых картинок
    const int KeepUsedFiles = 30;       // сколько уже показанных картинок оставлять в кэше
    const int MaxUsedHistory = 5000;    // размер истории показанных
    const int ParallelDownloads = 6;
    const string FilePrefix = "wallhaven-";   // трогаем только свои файлы, даже если папка общая

    static readonly string StateFile = Path.Combine(AppSettings.AppDir, "state.json");
    static readonly string[] ImageExt = { ".jpg", ".jpeg", ".png" };

    readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(60) };
    readonly NotifyIcon tray;
    readonly System.Windows.Forms.Timer timer;
    readonly SemaphoreSlim gate = new(1, 1);
    AppSettings settings;
    State state;
    SettingsForm? settingsForm;

    string CacheDir => settings.CacheDir;

    public TrayApp()
    {
        settings = AppSettings.Load();
        state = LoadState();
        EnsureCacheDir();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Wallshall/1.0");

        // Значки из Segoe Fluent Icons: обновить, шестерёнка, папка, корзина, питание
        var menu = new DarkMenu();
        menu.AddItem("Сменить обои", '\uE72C', async (_, _) => await ChangeAsync());
        menu.AddItem("Настройки…", '\uE713', async (_, _) => await ShowSettingsAsync());
        menu.AddItem("Открыть папку", '\uE838', (_, _) => Process.Start("explorer.exe", CacheDir));
        menu.AddItem("Очистить кэш", '\uE74D', async (_, _) => await CleanAsync());
        menu.AddSeparator();
        menu.AddItem("Выход", '\uE7E8', (_, _) => Exit());

        tray = new NotifyIcon
        {
            Icon = Theme.LoadAppIcon(SystemInformation.SmallIconSize),
            Text = "Wallshall",
            ContextMenuStrip = menu,
            Visible = true
        };
        tray.DoubleClick += async (_, _) => await ChangeAsync();

        timer = new System.Windows.Forms.Timer { Interval = settings.IntervalMinutes * 60_000 };
        timer.Tick += async (_, _) => await ChangeAsync();
        timer.Start();

        _ = ChangeAsync();
    }

    // ================= Основная логика =================

    async Task ChangeAsync()
    {
        if (!await gate.WaitAsync(0)) return;   // уже идёт смена
        try
        {
            // Новый день — начинаем топ сначала (показанные всё равно пропустятся)
            var today = DateTime.Now.ToString("yyyy-MM-dd");
            bool newDay = state.Date != today;
            if (newDay) { state.Page = 1; state.Date = today; }

            // Если непоказанных в кэше нет (или новый день) — докачиваем
            string? file = newDay ? null : PickUnused();
            string? error = null;
            if (file == null)
            {
                try { await FillCacheAsync(); }
                catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                { error = "Неверный API-ключ"; }
                catch (Exception ex) { error = "Сайт недоступен"; Debug.WriteLine(ex); }
                file = PickUnused();
            }

            // Совсем ничего нового — любая из кэша
            file ??= PickAny();

            if (file == null)
            {
                SetStatus(error ?? "Нет картинок по фильтрам");
                return;
            }

            SetWallpaper(file);
            MarkUsed(Path.GetFileName(file));
            CleanupCache();
            SaveState();
            SetStatus((error != null ? error + ": " : "Обои: ") + Path.GetFileName(file));
        }
        finally { gate.Release(); }
    }

    /// Листает страницы топа, пока не найдёт непоказанные картинки,
    /// и скачивает все непоказанные со страницы (про запас).
    async Task FillCacheAsync()
    {
        var used = state.Used.ToHashSet(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < MaxPagesPerRun; i++)
        {
            var urls = await GetPageAsync(state.Page);
            if (urls.Count == 0) { state.Page = 1; break; }   // топ закончился — по кругу

            var fresh = urls.Where(u => !used.Contains(FileNameOf(u))).ToList();
            if (fresh.Count > 0)
            {
                await DownloadAllAsync(fresh);
                return;
            }
            state.Page++;   // вся страница уже показана — сразу следующая, без "пустого клика"
        }
        SaveState();
    }

    async Task<List<string>> GetPageAsync(int page)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, $"{ApiUrl}?{settings.BuildQuery()}&page={page}");
        var key = settings.ApiKey;
        if (key != "") req.Headers.Add("X-API-Key", key);   // в заголовке, чтобы ключ не светился в URL

        using var resp = await http.SendAsync(req);
        resp.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("data").EnumerateArray()
            .Select(e => e.GetProperty("path").GetString()!)
            .ToList();
    }

    async Task DownloadAllAsync(List<string> urls)
    {
        var opts = new ParallelOptions { MaxDegreeOfParallelism = ParallelDownloads };
        await Parallel.ForEachAsync(urls, opts, async (url, ct) =>
        {
            var target = Path.Combine(CacheDir, FileNameOf(url));
            if (File.Exists(target)) return;
            var tmp = target + ".part";   // чтобы не было битых файлов при обрыве
            try
            {
                await using (var src = await http.GetStreamAsync(url, ct))
                await using (var dst = File.Create(tmp))
                    await src.CopyToAsync(dst, ct);
                File.Move(tmp, target, overwrite: true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                TryDelete(tmp);
            }
        });
    }

    // ================= Настройки =================

    async Task ShowSettingsAsync()
    {
        if (settingsForm != null) { settingsForm.Activate(); return; }

        AppSettings result;
        using (settingsForm = new SettingsForm(settings))
        {
            var ok = settingsForm.ShowDialog() == DialogResult.OK;
            result = settingsForm.Result;
            settingsForm = null;
            if (!ok) return;
        }

        await gate.WaitAsync();   // ждём, если сейчас идёт смена обоев
        bool filtersChanged;
        try
        {
            var old = settings;
            filtersChanged = old.BuildQuery() != result.BuildQuery();
            bool dirChanged = !SamePath(old.CacheDir, result.CacheDir);

            if (filtersChanged)
            {
                // Запас скачан по старым фильтрам (например, с NSFW) — выкидываем его.
                // История показов остаётся.
                DeleteOurFiles(old.CacheDir);
                if (dirChanged) DeleteOurFiles(result.CacheDir);
                state.Page = 1;
            }
            else if (dirChanged && CachedImages(old.CacheDir).Any())
            {
                if (DarkMessage.Ask("Перенести уже скачанные обои в новую папку?"))
                    MoveOurFiles(old.CacheDir, result.CacheDir);
            }

            settings = result;
            settings.Save();
            SaveState();
            EnsureCacheDir();
            timer.Interval = settings.IntervalMinutes * 60_000;   // заодно перезапускает отсчёт
            SetStatus("Настройки сохранены");
        }
        finally { gate.Release(); }

        if (filtersChanged) await ChangeAsync();   // сразу показать обои по новым фильтрам
    }

    // ================= Кэш и состояние =================

    void EnsureCacheDir()
    {
        try { Directory.CreateDirectory(CacheDir); }
        catch
        {
            // Папка пропала (например, отключили диск) — возвращаемся к стандартной
            settings.CacheDir = AppSettings.DefaultCacheDir;
            Directory.CreateDirectory(CacheDir);
            try { settings.Save(); } catch { }
        }
    }

    static IEnumerable<FileInfo> CachedImages(string dir)
    {
        if (!Directory.Exists(dir)) return Enumerable.Empty<FileInfo>();
        return new DirectoryInfo(dir).EnumerateFiles(FilePrefix + "*")
            .Where(f => ImageExt.Contains(f.Extension, StringComparer.OrdinalIgnoreCase));
    }

    IEnumerable<FileInfo> CachedImages() => CachedImages(CacheDir);

    string? PickUnused()
    {
        var used = state.Used.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var list = CachedImages().Where(f => !used.Contains(f.Name)).ToArray();
        return list.Length > 0 ? list[Random.Shared.Next(list.Length)].FullName : null;
    }

    string? PickAny()
    {
        var list = CachedImages().ToArray();
        return list.Length > 0 ? list[Random.Shared.Next(list.Length)].FullName : null;
    }

    void MarkUsed(string name)
    {
        state.Used.Remove(name);
        state.Used.Add(name);
        if (state.Used.Count > MaxUsedHistory)
            state.Used.RemoveRange(0, state.Used.Count - MaxUsedHistory);
    }

    /// Удаляет старые показанные файлы; непоказанные (запас) не трогает.
    void CleanupCache()
    {
        var order = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < state.Used.Count; i++) order[state.Used[i]] = i;

        var oldUsed = CachedImages()
            .Where(f => order.ContainsKey(f.Name))
            .OrderByDescending(f => order[f.Name])
            .Skip(KeepUsedFiles);
        foreach (var f in oldUsed) TryDelete(f.FullName);

        foreach (var part in Directory.EnumerateFiles(CacheDir, FilePrefix + "*.part")) TryDelete(part);
    }

    static void DeleteOurFiles(string dir)
    {
        if (!Directory.Exists(dir)) return;
        foreach (var f in Directory.EnumerateFiles(dir, FilePrefix + "*"))
            if (ImageExt.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase) || f.EndsWith(".part"))
                TryDelete(f);
    }

    static void MoveOurFiles(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (var f in CachedImages(from).ToList())
        {
            try
            {
                var dest = Path.Combine(to, f.Name);
                if (File.Exists(dest)) f.Delete();
                else f.MoveTo(dest);
            }
            catch (Exception ex) { Debug.WriteLine(ex); }
        }
    }

    async Task CleanAsync()
    {
        if (!DarkMessage.Ask("Удалить все скачанные обои и историю показов?")) return;

        await gate.WaitAsync();
        try
        {
            DeleteOurFiles(CacheDir);
            state = new State();
            SaveState();
            SetStatus("Кэш очищен");
        }
        finally { gate.Release(); }
    }

    static State LoadState()
    {
        try
        {
            if (File.Exists(StateFile))
                return JsonSerializer.Deserialize<State>(File.ReadAllText(StateFile)) ?? new State();
        }
        catch (Exception ex) { Debug.WriteLine(ex); }
        return new State();
    }

    void SaveState()
    {
        try
        {
            Directory.CreateDirectory(AppSettings.AppDir);
            var tmp = StateFile + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(state));
            File.Move(tmp, StateFile, overwrite: true);
        }
        catch (Exception ex) { Debug.WriteLine(ex); }
    }

    // ================= Windows =================

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool SystemParametersInfo(int uiAction, int uiParam, string pvParam, int fWinIni);

    static void SetWallpaper(string path)
    {
        // Стиль "Заполнение", чтобы картинка любого размера закрывала экран
        using (var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", writable: true))
        {
            key?.SetValue("WallpaperStyle", "10");
            key?.SetValue("TileWallpaper", "0");
        }
        // SPI_SETDESKWALLPAPER = 20, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE = 3
        SystemParametersInfo(20, 0, path, 3);
    }

    // ================= Мелочи =================

    static string FileNameOf(string url) => Path.GetFileName(new Uri(url).LocalPath);

    static bool SamePath(string a, string b) =>
        string.Equals(Path.GetFullPath(a).TrimEnd('\\'), Path.GetFullPath(b).TrimEnd('\\'),
                      StringComparison.OrdinalIgnoreCase);

    static void TryDelete(string path)
    {
        try { File.Delete(path); } catch { }
    }

    void SetStatus(string s) => tray.Text = s.Length > 63 ? s[..63] : s;

    void Exit()
    {
        settingsForm?.Close();
        timer.Stop();
        tray.Visible = false;
        tray.Dispose();
        http.Dispose();
        ExitThread();
    }
}
