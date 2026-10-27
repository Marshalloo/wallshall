static class RulesTests
{
    public static void Run()
    {
        Check.Suite("Когда настройки сохранять нельзя");

        Check.That(Problem(Good()) == null, "корректные настройки проходят");

        Check.Equal(Problem(With(s => s.General = s.Anime = s.People = false)) ?? "",
                    SettingsRules.NoCategory, "ни одной категории");

        Check.Equal(Problem(With(s => s.Sfw = s.Sketchy = s.Nsfw = false)) ?? "",
                    SettingsRules.NoPurity, "ни одного типа контента");

        Check.Equal(Problem(With(s => { s.Nsfw = true; s.ApiKey = ""; })) ?? "",
                    SettingsRules.NsfwNeedsKey, "NSFW без ключа");

        Check.That(Problem(With(s => { s.Nsfw = true; s.ApiKey = "ключ"; })) == null, "NSFW с ключом проходит");

        Check.Equal(Problem(With(s => s.IntervalMinutes = 0)) ?? "", SettingsRules.BadInterval, "интервал 0");
        Check.Equal(Problem(With(s => s.IntervalMinutes = -5)) ?? "", SettingsRules.BadInterval, "отрицательный интервал");
        Check.Equal(Problem(With(s => s.IntervalMinutes = 99999)) ?? "", SettingsRules.BadInterval, "слишком большой интервал");
        Check.That(Problem(With(s => s.IntervalMinutes = AppSettings.MinInterval)) == null, "минимальный интервал проходит");
        Check.That(Problem(With(s => s.IntervalMinutes = AppSettings.MaxInterval)) == null, "максимальный интервал проходит");

        Check.Equal(SettingsRules.Problem(Good(), cacheUsable: false, favoritesUsable: true, favoritesCount: 5) ?? "",
                    SettingsRules.BadCacheDir, "недоступная папка кэша");

        Check.Equal(SettingsRules.Problem(Good(), cacheUsable: true, favoritesUsable: false, favoritesCount: 5) ?? "",
                    SettingsRules.BadFavoritesDir, "недоступная папка избранного");

        Check.Equal(Problem(With(s => s.FavoritesDir = s.CacheDir)) ?? "",
                    SettingsRules.SharedFolders, "одна папка на кэш и избранное");

        Check.Equal(SettingsRules.Problem(With(s => s.FavoritesOnly = true), true, true, favoritesCount: 0) ?? "",
                    SettingsRules.EmptyFavorites, "режим «только избранное» при пустой папке");

        Check.That(SettingsRules.Problem(With(s => s.FavoritesOnly = true), true, true, favoritesCount: 1) == null,
                   "режим «только избранное» при непустой папке проходит");

        Check.Suite("Порядок проверок");

        var everything = With(s =>
        {
            s.General = s.Anime = s.People = false;
            s.Sfw = s.Sketchy = s.Nsfw = false;
            s.IntervalMinutes = 0;
        });
        Check.Equal(Problem(everything) ?? "", SettingsRules.NoCategory,
                    "сообщается первая причина, а не все сразу");
    }

    static AppSettings Good() => new()
    {
        General = true, Sfw = true,
        IntervalMinutes = 15,
        CacheDir = Path.Combine(Path.GetTempPath(), "wallshall-rules", "cache"),
        FavoritesDir = Path.Combine(Path.GetTempPath(), "wallshall-rules", "favorites"),
    };

    static AppSettings With(Action<AppSettings> change)
    {
        var settings = Good();
        change(settings);
        return settings;
    }

    static string? Problem(AppSettings settings) =>
        SettingsRules.Problem(settings, cacheUsable: true, favoritesUsable: true, favoritesCount: 5);
}
