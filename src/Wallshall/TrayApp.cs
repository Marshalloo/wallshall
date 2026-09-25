using System.Diagnostics;
using System.Net;

class TrayApp : ApplicationContext
{
    const int MaxPagesPerRun = 10;
    const int KeepUsedFiles = 30;

    readonly WallhavenApi api = new();
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
        state = State.Load();
        EnsureCacheDir();

        var menu = new DarkMenu();
        menu.AddItem("Сменить обои", '\uE72C', async (_, _) => await ChangeAsync());
        menu.AddItem("Настройки…", '\uE713', async (_, _) => await ShowSettingsAsync());
        menu.AddItem("Открыть папку", '\uE838', (_, _) => Shell.Open(CacheDir));
        menu.AddItem("Очистить кэш", '\uE74D', async (_, _) => await CleanAsync());
        menu.AddSeparator();
        menu.AddItem("Выход", '\uE7E8', (_, _) => Exit());

        tray = new NotifyIcon
        {
            Icon = Theme.LoadAppIcon(SystemInformation.SmallIconSize),
            Text = "Wallshall",
            ContextMenuStrip = menu,
            Visible = true,
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
            var files = newDay ? new List<string>() : Cache.PickUnused(CacheDir, state.UsedSet(), need);
            string? error = null;

            if (files.Count < need)
            {
                error = await FillCacheAsync();
                files = Cache.PickUnused(CacheDir, state.UsedSet(), need);
            }

            if (files.Count < need) files = Cache.FillUp(CacheDir, files, need);

            if (files.Count == 0)
            {
                SetStatus(error ?? "Нет картинок по фильтрам");
                return;
            }

            Wallpaper.Set(files);
            foreach (var file in files) state.MarkUsed(Path.GetFileName(file));
            Cache.Cleanup(CacheDir, state.Used, KeepUsedFiles);
            state.Save();
            SetStatus(Status(files, error));
        }
        finally { gate.Release(); }
    }

    static string Status(List<string> files, string? error) =>
        error != null ? error + ": из кэша" :
        files.Count > 1 ? $"Обои: {files.Count} шт." :
        "Обои: " + Path.GetFileName(files[0]);

    async Task<string?> FillCacheAsync()
    {
        try
        {
            var used = state.UsedSet();
            for (int i = 0; i < MaxPagesPerRun; i++)
            {
                var urls = await api.GetPageAsync(settings, state.Page);
                if (urls.Count == 0) { state.Page = 1; break; }

                var fresh = urls.Where(u => !used.Contains(WallhavenApi.FileNameOf(u))).ToList();
                if (fresh.Count > 0)
                {
                    await api.DownloadAsync(fresh, CacheDir);
                    return null;
                }
                state.Page++;
            }
            state.Save();
            return null;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            return "Неверный API-ключ";
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            return "Сайт недоступен";
        }
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
            refresh = Apply(result);
        }
        finally { gate.Release(); }

        if (refresh) await ChangeAsync();
    }

    bool Apply(AppSettings updated)
    {
        var old = settings;
        bool filtersChanged = old.BuildQuery() != updated.BuildQuery();
        bool dirChanged = !Cache.SamePath(old.CacheDir, updated.CacheDir);

        if (filtersChanged)
        {
            Cache.DeleteAll(old.CacheDir);
            if (dirChanged) Cache.DeleteAll(updated.CacheDir);
            state.Page = 1;
        }
        else if (dirChanged && Cache.Images(old.CacheDir).Any())
        {
            if (DarkMessage.Ask("Перенести уже скачанные обои в новую папку?"))
                Cache.Move(old.CacheDir, updated.CacheDir);
        }

        settings = updated;
        settings.Save();
        state.Save();
        EnsureCacheDir();
        timer.Interval = settings.IntervalMinutes * 60_000;
        SetStatus("Настройки сохранены");

        return filtersChanged || old.PerMonitor != updated.PerMonitor;
    }

    async Task CleanAsync()
    {
        if (!DarkMessage.Ask("Удалить все скачанные обои и историю показов?")) return;

        await gate.WaitAsync();
        try
        {
            Cache.DeleteAll(CacheDir);
            state = new State();
            state.Save();
            SetStatus("Кэш очищен");
        }
        finally { gate.Release(); }
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

    void SetStatus(string text) => tray.Text = text.Length > 63 ? text[..63] : text;

    void Exit()
    {
        settingsForm?.Close();
        timer.Stop();
        tray.Visible = false;
        tray.Dispose();
        api.Dispose();
        ExitThread();
    }
}
