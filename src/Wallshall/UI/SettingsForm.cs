using System.Diagnostics;
using System.Net;

class SettingsForm : DarkForm
{
    readonly DarkField apiKey = new(300) { Password = true, Placeholder = "Не задан" };
    readonly DarkCheckBox showKey = new() { Text = "Показать" };
    readonly DarkButton checkKey = new() { Text = "Проверить" };
    readonly Label keyStatus = new() { AutoSize = true };
    readonly LinkLabel getKey = new()
    {
        Text = "Где взять ключ?", AutoSize = true,
        LinkColor = Theme.Accent, ActiveLinkColor = Theme.AccentPressed,
        VisitedLinkColor = Theme.Accent, LinkBehavior = LinkBehavior.HoverUnderline,
    };

    readonly DarkCheckBox general = new() { Text = "General" };
    readonly DarkCheckBox anime = new() { Text = "Anime" };
    readonly DarkCheckBox people = new() { Text = "People" };

    readonly DarkCheckBox sfw = new() { Text = "SFW" };
    readonly DarkCheckBox sketchy = new() { Text = "Sketchy" };
    readonly DarkCheckBox nsfw = new() { Text = "NSFW" };

    readonly DarkField topRange = new(200) { Editable = false };
    readonly DarkField atLeast = new(200) { Placeholder = "Любое" };
    readonly RatioPicker ratios = new();
    readonly DarkField interval = new(200);

    readonly DarkField cacheDir = new(300);
    readonly DarkButton browseCache = new() { Text = "Обзор…" };
    readonly DarkField favoritesDir = new(300);
    readonly DarkButton browseFavorites = new() { Text = "Обзор…" };

    readonly DarkCheckBox perMonitor = new() { Toggle = true };
    readonly DarkCheckBox favoritesOnly = new() { Toggle = true };
    readonly DarkCheckBox autostart = new() { Toggle = true };
    readonly DarkButton save = new() { Text = "Сохранить", Primary = true };

    public AppSettings Result { get; private set; }

    public SettingsForm(AppSettings current)
    {
        Result = current.Clone();
        Text = "Настройки Wallshall";

        FillDropDowns();
        BuildLayout();
        ShowValues(current);
        WireUp();

        FinishLayout();
    }

    void FillDropDowns()
    {
        topRange.Option("1d", "1 день").Option("3d", "3 дня").Option("1w", "1 неделя")
                .Option("1M", "1 месяц").Option("3M", "3 месяца").Option("6M", "6 месяцев")
                .Option("1y", "1 год");
        atLeast.Option("", "Любое").Option("1920x1080", "1920x1080").Option("2560x1440", "2560x1440")
               .Option("3440x1440", "3440x1440").Option("3840x2160", "3840x2160");
        interval.Option("5", "5").Option("10", "10").Option("15", "15").Option("30", "30")
                .Option("60", "60").Option("180", "180");
    }

    void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            ColumnCount = 1,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(20, 8, 20, 20),
        };

        void Section(string title, Card card)
        {
            root.Controls.Add(new Label
            {
                Text = title, Font = Theme.FontSection, AutoSize = true,
                Margin = new Padding(2, 16, 0, 8),
            });
            card.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            root.Controls.Add(card);
        }

        var account = new Card();
        account.AddRow("API-ключ", apiKey, showKey);
        account.AddRow("", checkKey, keyStatus, getKey);
        Section("Аккаунт Wallhaven", account);

        var filters = new Card();
        filters.AddRow("Категории", general, anime, people);
        filters.AddRow("Контент", sfw, sketchy, nsfw);
        filters.AddRow("Топ за", topRange);
        filters.AddRow("Мин. разрешение", atLeast);
        filters.AddTallRow("Пропорции", ratios);
        Section("Фильтры", filters);

        var favorites = new Card();
        favorites.AddRow("Папка избранного", favoritesDir, browseFavorites);
        favorites.AddRow("Только избранное", favoritesOnly);
        Section("Избранное", favorites);

        var app = new Card();
        app.AddRow("Менять каждые, мин", interval);
        app.AddRow("Папка кэша", cacheDir, browseCache);
        app.AddRow("Разные обои на мониторах", perMonitor);
        app.AddRow("Запуск с Windows", autostart);
        Section("Приложение", app);

        // кнопка «Сохранить» — поле: её подписывает WireUp
        var cancel = new DarkButton { Text = "Отмена", DialogResult = DialogResult.Cancel };
        var buttons = UI.Row(save, cancel);
        buttons.Anchor = AnchorStyles.Right;
        buttons.Margin = new Padding(0, 0, 0, 0);

        var footer = new TableLayoutPanel
        {
            ColumnCount = 2,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Anchor = AnchorStyles.Left | AnchorStyles.Right,
            Margin = new Padding(0, 20, 0, 0),
        };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        footer.Controls.Add(new Label
        {
            Text = AppInfo.Display, AutoSize = true,
            ForeColor = Theme.TextDisabled, Anchor = AnchorStyles.Left,
            Margin = new Padding(2, 0, 12, 0),
        });
        footer.Controls.Add(buttons);
        root.Controls.Add(footer);

        Controls.Add(root);
        AcceptButton = save;
        CancelButton = cancel;
    }

    void ShowValues(AppSettings current)
    {
        apiKey.Value = current.ApiKey;
        general.Checked = current.General;
        anime.Checked = current.Anime;
        people.Checked = current.People;
        sfw.Checked = current.Sfw;
        sketchy.Checked = current.Sketchy;
        nsfw.Checked = current.Nsfw;
        topRange.Value = current.TopRange;
        atLeast.Value = current.AtLeast;
        ratios.Value = current.Ratios;
        ratios.Mode = current.Mode;
        interval.Value = current.IntervalMinutes.ToString();
        cacheDir.Value = current.CacheDir;
        favoritesDir.Value = current.FavoritesDir;
        perMonitor.Checked = current.PerMonitor;
        favoritesOnly.Checked = current.FavoritesOnly;
        autostart.Checked = Autostart.IsEnabled;
    }

    void WireUp()
    {
        showKey.CheckedChanged += (_, _) => apiKey.Password = !showKey.Checked;
        getKey.LinkClicked += (_, _) => Shell.Open("https://wallhaven.cc/settings/account");
        checkKey.Click += async (_, _) => await CheckKeyAsync();
        browseCache.Click += (_, _) => Browse(cacheDir, "Папка для скачанных обоев");
        browseFavorites.Click += (_, _) => Browse(favoritesDir, "Папка для избранных обоев");
        save.Click += (_, _) => Save();
    }

    void SetKeyStatus(string text, Color color)
    {
        keyStatus.Text = text;
        keyStatus.ForeColor = color;
    }

    async Task CheckKeyAsync()
    {
        var key = apiKey.Value;
        if (key == "") { SetKeyStatus("Введите ключ", Theme.Warning); return; }

        checkKey.Enabled = false;
        SetKeyStatus("Проверяю…", Theme.TextSecondary);
        try
        {
            switch (await WallhavenApi.CheckKeyAsync(key))
            {
                case HttpStatusCode.OK: SetKeyStatus("✓ Ключ работает", Theme.Success); break;
                case HttpStatusCode.Unauthorized: SetKeyStatus("✗ Неверный ключ", Theme.Error); break;
                case null: SetKeyStatus("Сайт недоступен", Theme.Warning); break;
                case { } code: SetKeyStatus($"Ошибка {(int)code}", Theme.Warning); break;
            }
        }
        finally { checkKey.Enabled = true; }
    }

    void Browse(DarkField field, string title)
    {
        using var dlg = new FolderBrowserDialog
        {
            Description = title,
            UseDescriptionForTitle = true,
            SelectedPath = Directory.Exists(field.Value) ? field.Value : AppSettings.AppDir,
        };
        if (dlg.ShowDialog(this) == DialogResult.OK) field.Value = dlg.SelectedPath;
    }

    /// Полный путь, если папкой можно пользоваться, иначе null.
    static string? Prepare(DarkField field)
    {
        if (field.Value == "") return null;
        try
        {
            var dir = Path.GetFullPath(field.Value);
            Directory.CreateDirectory(dir);
            return dir;
        }
        catch (Exception ex) { Debug.WriteLine(ex); return null; }
    }

    void Save()
    {
        if (!int.TryParse(interval.Value, out int minutes)) minutes = 0;

        var cache = Prepare(cacheDir);
        var favorites = Prepare(favoritesDir);

        Collect(Result);
        Result.IntervalMinutes = minutes;
        Result.PerMonitor = perMonitor.Checked;
        Result.FavoritesOnly = favoritesOnly.Checked;
        Result.CacheDir = cache ?? "";
        Result.FavoritesDir = favorites ?? "";

        var problem = SettingsRules.Problem(Result, cache != null, favorites != null,
                                            favorites == null ? 0 : Favorites.Count(favorites));
        if (problem != null)
        {
            DarkMessage.Info(problem, this);
            return;
        }

        try { Autostart.IsEnabled = autostart.Checked; }
        catch (Exception ex) { Debug.WriteLine(ex); }

        DialogResult = DialogResult.OK;
    }

    AppSettings Collect(AppSettings into)
    {
        into.ApiKey = apiKey.Value;
        into.General = general.Checked;
        into.Anime = anime.Checked;
        into.People = people.Checked;
        into.Sfw = sfw.Checked;
        into.Sketchy = sketchy.Checked;
        into.Nsfw = nsfw.Checked;
        into.TopRange = topRange.Value;
        into.AtLeast = atLeast.Value;
        into.Ratios = ratios.Value;
        into.Mode = ratios.Mode;
        return into;
    }
}
