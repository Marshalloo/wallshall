using System.Diagnostics;
using System.Net;

class TrayApp : ApplicationContext
{
    const int MaxPagesPerRun = 10;
    const int KeepUsedFiles = 30;
    const int MaxThumbs = 16;
    const int MaxLabel = 22;

    const char GlyphFavorite = '';
    const char GlyphFavoriteOn = '';
    const char GlyphSite = '';
    const char GlyphFolder = '';
    const char GlyphPicture = '';

    readonly WallhavenApi api = new();
    readonly NotifyIcon tray;
    readonly DarkMenu menu;
    readonly System.Windows.Forms.Timer timer;
    readonly SemaphoreSlim gate = new(1, 1);

    readonly ToolStripMenuItem favoriteItem;
    readonly ToolStripMenuItem siteItem;
    readonly ToolStripMenuItem revealItem;
    readonly ToolStripMenuItem favoritesOnlyItem;
    readonly List<ToolStripMenuItem> monitorItems = new();
    readonly Dictionary<string, Image> thumbs = new(StringComparer.OrdinalIgnoreCase);

    int blockFor = -1;
    AppSettings settings;
    State state;
    SettingsForm? settingsForm;

    string CacheDir => settings.CacheDir;
    string FavoritesDir => settings.FavoritesDir;

    public TrayApp()
    {
        AppSettings.ScreenRatios = Wallpaper.ScreenRatios;
        settings = AppSettings.Load();
        state = State.Load();
        EnsureDirs();

        // Пункты для одной картинки: живут в меню, пока обои одни на все экраны
        favoriteItem = new ToolStripMenuItem("В избранное", null, (_, _) => ToggleFavorite(FileAt(0)));
        siteItem = new ToolStripMenuItem("Открыть на сайте", Theme.MenuGlyph(GlyphSite), (_, _) => OpenSite(FileAt(0)));
        revealItem = new ToolStripMenuItem("Показать в папке", Theme.MenuGlyph(GlyphFolder), (_, _) => Reveal(FileAt(0)));
        foreach (var item in new[] { favoriteItem, siteItem, revealItem }) Style(item);

        menu = new DarkMenu();
        menu.AddItem("Сменить обои", '', async (_, _) => await ChangeAsync());
        menu.AddSeparator();
        menu.AddSeparator();
        favoritesOnlyItem = menu.AddItem("Только избранное", '', async (_, _) => await ToggleFavoritesOnlyAsync());
        menu.AddItem("Открыть избранное", '', (_, _) => Shell.Open(FavoritesDir));
        menu.AddItem("Настройки…", '', async (_, _) => await ShowSettingsAsync());
        menu.AddItem("Очистить кэш", '', async (_, _) => await CleanAsync());
        menu.AddSeparator();
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
            int need = settings.PerMonitor ? Math.Max(1, Wallpaper.Count()) : 1;
            var (files, error) = settings.FavoritesOnly
                ? PickFavorites(need)
                : await PickFromCacheAsync(need);

            if (files.Count == 0)
            {
                SetStatus(error ?? "Нет картинок по фильтрам");
                return;
            }

            bool perMonitor = Wallpaper.Set(files);
            state.Current = perMonitor ? files : new List<string> { files[0] };

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
            await WarmThumbsAsync();
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

    // ================= Меню =================

    /// Обои разные — строка на каждый монитор; одни на всех — плоские пункты.
    void RefreshMenu()
    {
        int count = state.Current.Count > 1 ? state.Current.Count : 0;
        if (count != blockFor) BuildBlock(count);

        if (blockFor == 0) UpdateSingle();
        else UpdateMonitors();

        int total = Favorites.Count(FavoritesDir);
        favoritesOnlyItem.Text = total > 0 ? $"Только избранное ({total})" : "Только избранное";
        favoritesOnlyItem.Checked = settings.FavoritesOnly;
    }

    void BuildBlock(int count)
    {
        while (menu.Items.Count > 2 && menu.Items[2] is not ToolStripSeparator) menu.Items.RemoveAt(2);

        if (count == 0)
        {
            menu.Insert(2, favoriteItem);
            menu.Insert(3, siteItem);
            menu.Insert(4, revealItem);
        }
        else
        {
            while (monitorItems.Count < count) monitorItems.Add(CreateMonitorItem(monitorItems.Count));
            for (int i = 0; i < count; i++) menu.Insert(2 + i, monitorItems[i]);
        }
        blockFor = count;
    }

    ToolStripMenuItem CreateMonitorItem(int index)
    {
        var item = new ToolStripMenuItem();
        Style(item);

        var sub = DarkMenu.SubMenu(item);
        sub.AddItem("В избранное", GlyphFavorite, (_, _) => ToggleFavorite(FileAt(index)));
        sub.AddItem("Открыть на сайте", GlyphSite, (_, _) => OpenSite(FileAt(index)));
        sub.AddItem("Показать в папке", GlyphFolder, (_, _) => Reveal(FileAt(index)));
        return item;
    }

    void UpdateSingle()
    {
        var file = FileAt(0);
        bool saved = file != null && Favorites.Contains(FavoritesDir, file);

        favoriteItem.Text = saved ? "Убрать из избранного" : "В избранное";
        favoriteItem.Image = Theme.MenuGlyph(saved ? GlyphFavoriteOn : GlyphFavorite);
        favoriteItem.Enabled = file != null;
        siteItem.Enabled = file != null && Cache.WallhavenId(file) != null;
        revealItem.Enabled = file != null;
    }

    void UpdateMonitors()
    {
        var monitors = Wallpaper.Monitors();
        bool named = monitors.Count == blockFor;

        for (int i = 0; i < blockFor; i++)
        {
            var item = monitorItems[i];
            var file = FileAt(i);
            var name = named ? monitors[i].Name : $"Обои {i + 1}";
            bool saved = file != null && Favorites.Contains(FavoritesDir, file);

            item.Text = file == null ? name : $"{name} · {Label(file)}" + (saved ? "  ★" : "");
            item.Image = (file == null ? null : Thumb(file)) ?? Theme.MenuGlyph(GlyphPicture);
            item.Enabled = file != null;

            var favorite = (ToolStripMenuItem)item.DropDownItems[0];
            favorite.Text = saved ? "Убрать из избранного" : "В избранное";
            favorite.Image = Theme.MenuGlyph(saved ? GlyphFavoriteOn : GlyphFavorite);
            item.DropDownItems[1].Enabled = file != null && Cache.WallhavenId(file) != null;
        }
    }

    static void Style(ToolStripItem item)
    {
        item.Padding = new Padding(Theme.S(4), Theme.S(6), Theme.S(12), Theme.S(6));
        item.ForeColor = Theme.Text;
    }

    static string Label(string file)
    {
        var name = Cache.WallhavenId(file) ?? Path.GetFileNameWithoutExtension(file);
        return name.Length > MaxLabel ? name[..MaxLabel] + "…" : name;
    }

    string? FileAt(int index) =>
        index < state.Current.Count && File.Exists(state.Current[index]) ? state.Current[index] : null;

    Image? Thumb(string file)
    {
        if (thumbs.TryGetValue(file, out var cached)) return cached;

        var image = Theme.Thumbnail(file);
        if (image != null) thumbs[file] = image;
        return image;
    }

    /// Раскодировать обои в 4K — дело небыстрое, поэтому готовим миниатюры заранее и не на UI-потоке.
    async Task WarmThumbsAsync()
    {
        var files = state.Current.Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(f => !thumbs.ContainsKey(f))
            .ToList();
        if (files.Count == 0) return;

        var made = await Task.Run(() => files.Select(f => (File: f, Image: Theme.Thumbnail(f))).ToList());
        foreach (var (file, image) in made)
            if (image != null) thumbs[file] = image;

        TrimThumbs();
    }

    /// Миниатюры не удаляются, пока могут быть нарисованы в меню, — просто ограничиваем их число.
    void TrimThumbs()
    {
        if (thumbs.Count <= MaxThumbs) return;

        var keep = state.Current.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var key in thumbs.Keys.Where(k => !keep.Contains(k)).ToList())
        {
            if (thumbs.Count <= MaxThumbs) break;
            thumbs.Remove(key);
        }
    }

    // ================= Действия =================

    void ToggleFavorite(string? file)
    {
        if (file == null) return;

        if (Favorites.Contains(FavoritesDir, file))
        {
            bool removed = Favorites.Remove(FavoritesDir, file);
            state.Current = state.Current.Where(File.Exists).ToList();
            state.Save();
            SetStatus(removed ? "Убрано из избранного" : "Не удалось убрать");
            return;
        }

        SetStatus(Favorites.Add(FavoritesDir, file) ? "Добавлено в избранное" : "Не удалось добавить");
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

    void OpenSite(string? file)
    {
        var id = file == null ? null : Cache.WallhavenId(file);
        if (id != null) Shell.Open("https://wallhaven.cc/w/" + id);
    }

    void Reveal(string? file)
    {
        if (file == null || !Shell.Reveal(file))
            Shell.Open(settings.FavoritesOnly ? FavoritesDir : CacheDir);
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
