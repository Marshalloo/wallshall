using System.Diagnostics;
using System.Text.Json;

class State
{
    public int Page { get; set; } = 1;
    public string Date { get; set; } = "";
    public List<string> Used { get; set; } = new();
}

class TrayApp : ApplicationContext
{
    const string ApiUrl = "https:
    const int MaxPagesPerRun = 10;
    const int KeepUsedFiles = 30;
    const int MaxUsedHistory = 5000;
    const int ParallelDownloads = 6;
    const string FilePrefix = "wallhaven-";

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



    async Task ChangeAsync()
    {
        if (!await gate.WaitAsync(0)) return;
        try
        {

            var today = DateTime.Now.ToString("yyyy-MM-dd");
            bool newDay = state.Date != today;
            if (newDay) { state.Page = 1; state.Date = today; }


            int need = settings.PerMonitor ? Math.Max(1, Wallpaper.MonitorCount()) : 1;


            var files = newDay ? new List<string>() : PickUnused(need);
            string? error = null;
            if (files.Count < need)
            {
                try { await FillCacheAsync(); }
                catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                { error = "Неверный API-ключ"; }
                catch (Exception ex) { error = "Сайт недоступен"; Debug.WriteLine(ex); }
                files = PickUnused(need);
            }


            if (files.Count < need) files = PickAny(need, files);

            if (files.Count == 0)
            {
                SetStatus(error ?? "Нет картинок по фильтрам");
                return;
            }

            Wallpaper.Set(files);
            foreach (var f in files) MarkUsed(Path.GetFileName(f));
            CleanupCache();
            SaveState();
            SetStatus(error != null ? error + ": из кэша"
                : files.Count > 1 ? $"Обои: {files.Count} шт."
                : "Обои: " + Path.GetFileName(files[0]));
        }
        finally { gate.Release(); }
    }



    async Task FillCacheAsync()
    {
        var used = state.Used.ToHashSet(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < MaxPagesPerRun; i++)
        {
            var urls = await GetPageAsync(state.Page);
            if (urls.Count == 0) { state.Page = 1; break; }

            var fresh = urls.Where(u => !used.Contains(FileNameOf(u))).ToList();
            if (fresh.Count > 0)
            {
                await DownloadAllAsync(fresh);
                return;
            }
            state.Page++;
        }
        SaveState();
    }

    async Task<List<string>> GetPageAsync(int page)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, $"{ApiUrl}?{settings.BuildQuery()}&page={page}");
        var key = settings.ApiKey;
        if (key != "") req.Headers.Add("X-API-Key", key);

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
            var tmp = target + ".part";
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

        await gate.WaitAsync();
        bool refresh;
        try
        {
            var old = settings;
            bool filtersChanged = old.BuildQuery() != result.BuildQuery();
            refresh = filtersChanged || old.PerMonitor != result.PerMonitor;
            bool dirChanged = !SamePath(old.CacheDir, result.CacheDir);

            if (filtersChanged)
            {


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
            timer.Interval = settings.IntervalMinutes * 60_000;
            SetStatus("Настройки сохранены");
        }
        finally { gate.Release(); }

        if (refresh) await ChangeAsync();
    }



    void EnsureCacheDir()
    {
        try { Directory.CreateDirectory(CacheDir); }
        catch
        {

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


    List<string> PickUnused(int count)
    {
        var used = state.Used.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return CachedImages().Where(f => !used.Contains(f.Name))
            .OrderBy(_ => Random.Shared.Next()).Take(count)
            .Select(f => f.FullName).ToList();
    }


    List<string> PickAny(int count, List<string> already)
    {
        var result = new List<string>(already);
        var rest = CachedImages().Select(f => f.FullName)
            .Where(f => !result.Contains(f, StringComparer.OrdinalIgnoreCase))
            .OrderBy(_ => Random.Shared.Next()).ToList();

        foreach (var f in rest)
        {
            if (result.Count >= count) break;
            result.Add(f);
        }

        return result;
    }

    void MarkUsed(string name)
    {
        state.Used.Remove(name);
        state.Used.Add(name);
        if (state.Used.Count > MaxUsedHistory)
            state.Used.RemoveRange(0, state.Used.Count - MaxUsedHistory);
    }


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
