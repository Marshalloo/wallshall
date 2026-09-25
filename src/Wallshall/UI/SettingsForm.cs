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
    readonly DarkField ratios = new(200) { Placeholder = "Любые" };
    readonly DarkField interval = new(200);

    readonly DarkField cacheDir = new(300);
    readonly DarkButton browse = new() { Text = "Обзор…" };

    readonly DarkCheckBox autostart = new() { Toggle = true };

    public AppSettings Result { get; private set; }

    public SettingsForm(AppSettings current)
    {
        Result = current.Clone();
        Text = "Настройки Wallshall";

        // ---- Варианты в выпадающих списках ----
        topRange.Option("1d", "1 день").Option("3d", "3 дня").Option("1w", "1 неделя")
                .Option("1M", "1 месяц").Option("3M", "3 месяца").Option("6M", "6 месяцев")
                .Option("1y", "1 год");
        atLeast.Option("", "Любое").Option("1920x1080", "1920x1080").Option("2560x1440", "2560x1440")
               .Option("3440x1440", "3440x1440").Option("3840x2160", "3840x2160");
        ratios.Option("", "Любые").Option("16x9", "16x9").Option("16x10", "16x10")
              .Option("21x9", "21x9").Option("32x9", "32x9").Option("9x16", "9x16")
              .Option("16x9,16x10", "16x9 и 16x10");
        interval.Option("5", "5").Option("10", "10").Option("15", "15").Option("30", "30")
                .Option("60", "60").Option("180", "180");

        // ---- Разметка ----
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
        filters.AddRow("Пропорции", ratios);
        Section("Фильтры", filters);

        var app = new Card();
        app.AddRow("Менять каждые, мин", interval);
        app.AddRow("Папка кэша", cacheDir, browse);
        app.AddRow("Запуск с Windows", autostart);
        Section("Приложение", app);

        var save = new DarkButton { Text = "Сохранить", Primary = true };
        var cancel = new DarkButton { Text = "Отмена", DialogResult = DialogResult.Cancel };
        var buttons = UI.Row(save, cancel);
        buttons.Anchor = AnchorStyles.Right;
        buttons.Margin = new Padding(0, 20, 0, 0);
        root.Controls.Add(buttons);

        Controls.Add(root);
        AcceptButton = save;
        CancelButton = cancel;

        // ---- Значения ----
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
        interval.Value = current.IntervalMinutes.ToString();
        cacheDir.Value = current.CacheDir;
        autostart.Checked = Autostart.IsEnabled;

        // ---- Обработчики ----
        showKey.CheckedChanged += (_, _) => apiKey.Password = !showKey.Checked;
        getKey.LinkClicked += (_, _) =>
            Process.Start(new ProcessStartInfo("https://wallhaven.cc/settings/account") { UseShellExecute = true });
        checkKey.Click += async (_, _) => await CheckKeyAsync();
        browse.Click += (_, _) => Browse();
        save.Click += (_, _) => Save();

        FinishLayout();
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
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            using var req = new HttpRequestMessage(HttpMethod.Get, "https://wallhaven.cc/api/v1/settings");
            req.Headers.Add("X-API-Key", key);
            req.Headers.UserAgent.ParseAdd("Wallshall/1.0");
            using var resp = await http.SendAsync(req);

            switch (resp.StatusCode)
            {
                case HttpStatusCode.OK: SetKeyStatus("✓ Ключ работает", Theme.Success); break;
                case HttpStatusCode.Unauthorized: SetKeyStatus("✗ Неверный ключ", Theme.Error); break;
                default: SetKeyStatus($"Ошибка {(int)resp.StatusCode}", Theme.Warning); break;
            }
        }
        catch
        {
            SetKeyStatus("Сайт недоступен", Theme.Warning);
        }
        finally { checkKey.Enabled = true; }
    }

    void Browse()
    {
        using var dlg = new FolderBrowserDialog
        {
            Description = "Папка для скачанных обоев",
            UseDescriptionForTitle = true,
            SelectedPath = Directory.Exists(cacheDir.Value) ? cacheDir.Value : AppSettings.AppDir,
        };
        if (dlg.ShowDialog(this) == DialogResult.OK) cacheDir.Value = dlg.SelectedPath;
    }

    void Save()
    {
        var key = apiKey.Value;
        if (!general.Checked && !anime.Checked && !people.Checked) { DarkMessage.Info("Выберите хотя бы одну категорию.", this); return; }
        if (!sfw.Checked && !sketchy.Checked && !nsfw.Checked) { DarkMessage.Info("Выберите хотя бы один тип контента.", this); return; }
        if (nsfw.Checked && key == "") { DarkMessage.Info("Для NSFW нужен API-ключ.", this); return; }

        if (!int.TryParse(interval.Value, out int minutes) || minutes < 1 || minutes > 1440)
        { DarkMessage.Info("Интервал — целое число минут от 1 до 1440.", this); return; }

        var dir = cacheDir.Value;
        try
        {
            if (dir == "") throw new Exception();
            dir = Path.GetFullPath(dir);
            Directory.CreateDirectory(dir);
        }
        catch { DarkMessage.Info("Не удалось использовать эту папку для кэша.", this); return; }

        Result.ApiKey = key;
        Result.General = general.Checked;
        Result.Anime = anime.Checked;
        Result.People = people.Checked;
        Result.Sfw = sfw.Checked;
        Result.Sketchy = sketchy.Checked;
        Result.Nsfw = nsfw.Checked;
        Result.TopRange = topRange.Value;
        Result.AtLeast = atLeast.Value;
        Result.Ratios = ratios.Value.Replace(" ", "");
        Result.IntervalMinutes = minutes;
        Result.CacheDir = dir;

        try { Autostart.IsEnabled = autostart.Checked; } catch { }

        DialogResult = DialogResult.OK;
    }
}
