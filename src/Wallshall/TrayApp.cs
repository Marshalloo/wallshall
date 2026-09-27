using System.Diagnostics;
using System.Net;

class TrayApp : ApplicationContext
{
    const int MaxPagesPerRun = 10;
    const int KeepUsedFiles = 30;

    readonly WallhavenApi api = new();
    readonly NotifyIcon tray;
    readonly DarkMenu menu;
    readonly ToolStripMenuItem favoriteItem;
    readonly ToolStripMenuItem favoritesOnlyItem;
    readonly ToolStripMenuItem openSiteItem;
    readonly System.Windows.Forms.Timer timer;
    readonly SemaphoreSlim gate = new(1, 1);

    AppSettings settings;
    State state;
    SettingsForm? settingsForm;

    string CacheDir => settings.CacheDir;
    string FavoritesDir => settings.FavoritesDir;

    public TrayApp()
    {
        settings = AppSettings.Load();
        state = State.Load();
        EnsureDirs();

        menu = new DarkMenu();
        menu.AddItem("Сменить обои", '', async (_, _) => await ChangeAsync());
        favoriteItem = menu.AddItem("В избранное", '', (_, _) => ToggleFavorite());
        favoritesOnlyItem = menu.AddItem("Только избранное", '', async (_, _) => await ToggleFavoritesOnlyAsync());
        menu.AddSeparator();
        openSiteItem = menu.AddItem("Открыть на сайте", '', (_, _) => OpenSite());
        menu.AddItem("Показать в папке", '', (_, _) => ShowInFolder());
        menu.AddItem("Открыть избранное", '', (_, _) => Shell.Open(FavoritesDir));
        menu.AddSeparator();
        menu.AddItem("Настройки…", '', async (_, _) => await ShowSettingsAsync());
        menu.AddItem("Очистить кэш", '', async (_, _) => await CleanAsync());
        menu.AddItem("Выход", '', (_, _) => Exit());
        menu.Opening += (_, _) => RefreshMenu();

        tray = new NotifyIcon
        {
            Icon = Theme.LoadAppIcon(SystemInformation.SmallIconSize),
            Text = AppInfo.Name,
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
            int need = settings.PerMonitor ? Math.Max(1, Wallpaper.MonitorCount()) : 1;
            var (files, error) = settings.FavoritesOnly
                ? PickFavorites(need)
                : await PickFromCacheAsync(need);

            if (files.Count == 0)
            {
                SetStatus(error ?? "Нет картинок по фильтрам");
                return;
            }

            Wallpaper.Set(files);
            state.Current = files;

            if (settings.FavoritesOnly)
            {
                foreach (var file in files) state.MarkFavShown(Path.GetFileName(file));
            }
            else
            {
                foreach (var file in files) state.MarkUsed(Path.GetFileName(file));
                Cache.Cleanup(CacheDir, state.Used, KeepUsedFiles);
            }

            state.Save();
            SetStatus(Status(files, error));
        }
        finally { gate.Release(); }
    }

    (List<string> files, string? error) PickFavorites(int need)
    {
        var files = Favorites.Pick(FavoritesDir, state.FavShown, need);
        return (files, files.Count == 0 ? "Избранное пусто" : null);
    }

    async Task<(List<string> files, string? error)> PickFromCacheAsync(int need)
    {
        var today = DateTime.Now.ToString("yyyy-MM-dd");
        bool newDay = state.Date != today;
        if (newDay) { state.Page = 1; state.Date = today; }

        var files = newDay ? new List<string>() : Cache.PickUnused(CacheDir, state.UsedSet(), need);
        string? error = null;

        if (files.Count < need)
        {
            error = await FillCacheAsync();
            files = Cache.PickUnused(CacheDir, state.UsedSet(), need);
        }

        if (files.Count < need) files = Cache.FillUp(CacheDir, files, need);

        return (files, error);
    }

    string Status(List<string> files, string? error)
    {
        var prefix = settings.FavoritesOnly ? "Избранное" : "Обои";
        return error != null ? error + ": из кэша" :
            files.Count > 1 ? $"{prefix}: {files.Count} шт." :
            $"{prefix}: {Path.GetFileName(files[0])}";
    }

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

    void RefreshMenu()
    {
        var current = CurrentFiles();
        bool saved = current.Count > 0 && current.All(f => Favorites.Contains(FavoritesDir, f));

        favoriteItem.Text = saved ? "Убрать из избранного" : "В избранное";
        favoriteItem.Enabled = current.Count > 0;
        SetGlyph(favoriteItem, saved ? '' : '');

        int count = Favorites.Count(FavoritesDir);
        favoritesOnlyItem.Text = count > 0 ? $"Только избранное ({count})" : "Только избранное";
        favoritesOnlyItem.Checked = settings.FavoritesOnly;

        openSiteItem.Enabled = current.Any(f => Cache.WallhavenId(f) != null);
    }

    static void SetGlyph(ToolStripItem item, char glyph)
    {
        var old = item.Image;
        item.Image = Theme.Glyph(glyph, Theme.S(16), Theme.Text);
        old?.Dispose();
    }

    List<string> CurrentFiles() => state.Current.Where(File.Exists).ToList();

    void ToggleFavorite()
    {
        var current = CurrentFiles();
        if (current.Count == 0) return;

        if (current.All(f => Favorites.Contains(FavoritesDir, f)))
        {
            int removed = current.Count(f => Favorites.Remove(FavoritesDir, f));
            state.Current = current.Where(File.Exists).ToList();
            state.Save();
            SetStatus(removed > 0 ? "Убрано из избранного" : "Не удалось убрать");
            return;
        }

        int added = current.Count(f => Favorites.Add(FavoritesDir, f));
        SetStatus(added > 1 ? $"В избранном: +{added}" :
                  added > 0 ? "Добавлено в избранное" : "Не удалось добавить");
    }

    async Task ToggleFavoritesOnlyAsync()
    {
        if (!settings.FavoritesOnly && Favorites.Count(FavoritesDir) == 0)
        {
            DarkMessage.Info("Избранное пусто. Добавьте обои пунктом «В избранное», " +
                             "или положите свои картинки в папку избранного.");
            return;
        }

        settings.FavoritesOnly = !settings.FavoritesOnly;
        settings.Save();
        SetStatus(settings.FavoritesOnly ? "Режим: только избранное" : "Режим: топ Wallhaven");
        await ChangeAsync();
    }

    void OpenSite()
    {
        var id = CurrentFiles().Select(Cache.WallhavenId).FirstOrDefault(x => x != null);
        if (id != null) Shell.Open("https://wallhaven.cc/w/" + id);
    }

    void ShowInFolder()
    {
        var current = CurrentFiles().FirstOrDefault();
        if (current == null || !Shell.Reveal(current))
            Shell.Open(settings.FavoritesOnly ? FavoritesDir : CacheDir);
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
        bool favDirChanged = !Cache.SamePath(old.FavoritesDir, updated.FavoritesDir);

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

        if (favDirChanged) state.FavShown.Clear();

        settings = updated;
        settings.Save();
        state.Save();
        EnsureDirs();
        timer.Interval = settings.IntervalMinutes * 60_000;
        SetStatus("Настройки сохранены");

        return filtersChanged || favDirChanged ||
               old.PerMonitor != updated.PerMonitor ||
               old.FavoritesOnly != updated.FavoritesOnly;
    }

    async Task CleanAsync()
    {
        if (!DarkMessage.Ask("Удалить все скачанные обои и историю показов?\n\nИзбранное останется на месте.")) return;

        await gate.WaitAsync();
        try
        {
            Cache.DeleteAll(CacheDir);
            state = new State { FavShown = state.FavShown, Current = state.Current };
            state.Save();
            SetStatus("Кэш очищен");
        }
        finally { gate.Release(); }
    }

    void EnsureDirs()
    {
        var cache = EnsureDir(settings.CacheDir, AppSettings.DefaultCacheDir);
        var favorites = EnsureDir(settings.FavoritesDir, AppSettings.DefaultFavoritesDir);
        if (cache == settings.CacheDir && favorites == settings.FavoritesDir) return;

        settings.CacheDir = cache;
        settings.FavoritesDir = favorites;
        try { settings.Save(); } catch { }
    }

    static string EnsureDir(string dir, string fallback)
    {
        try { Directory.CreateDirectory(dir); return dir; }
        catch
        {
            Directory.CreateDirectory(fallback);
            return fallback;
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
