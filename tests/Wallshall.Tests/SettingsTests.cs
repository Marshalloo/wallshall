static class SettingsTests
{
    public static void Run()
    {
        Check.Suite("Настройки и запрос");

        AppSettings.ScreenRatios = () => "21x9";

        var fresh = new AppSettings { RatioMode = RatioSelection.ModeScreen, Ratios = "" };
        Check.That(fresh.BuildQuery().Contains("&ratios=21x9"), "новая установка фильтрует по экрану");

        var old = new AppSettings { Ratios = "16x9" };
        Check.Equal(old.Mode, RatioSelection.ModeCustom, "настройки без режима остаются ручными");
        Check.That(old.BuildQuery().Contains("&ratios=16x9"), "и фильтруют по сохранённому списку");

        var empty = new AppSettings { Ratios = "" };
        Check.Equal(empty.Mode, RatioSelection.ModeAny, "без пропорций — «любые»");
        Check.That(!empty.BuildQuery().Contains("ratios="), "и без параметра");

        var any = new AppSettings { RatioMode = RatioSelection.ModeAny, Ratios = "16x9" };
        Check.That(!any.BuildQuery().Contains("ratios="), "режим «любые» перебивает список");

        var several = new AppSettings { RatioMode = RatioSelection.ModeCustom, Ratios = "16x9,9x16" };
        Check.That(several.BuildQuery().Contains("&ratios=16x9,9x16"), "запятая не экранируется");

        AppSettings.ScreenRatios = () => "";
        var unknown = new AppSettings { RatioMode = RatioSelection.ModeScreen };
        Check.That(!unknown.BuildQuery().Contains("ratios="), "экран не определился — фильтра нет");

        Check.Suite("Повреждённый файл настроек");

        var broken = new AppSettings
        {
            IntervalMinutes = 0,
            General = false, Anime = false, People = false,
            Sfw = false, Sketchy = false, Nsfw = false,
            RatioMode = "чепуха",
            Ratios = "landscape,16x9,,16X9",
            CacheDir = "  ",
            FavoritesDir = "  ",
        }.Normalized();

        Check.That(broken.IntervalMinutes == AppSettings.MinInterval, "нулевой интервал поднят до минимума");
        Check.That(broken.General, "без категорий включается General");
        Check.That(broken.Sfw, "без типа контента включается SFW");
        Check.Equal(broken.Mode, RatioSelection.ModeCustom, "неизвестный режим отброшен");
        Check.Equal(broken.Ratios, "16x9", "список почищен от ключевых слов и дублей");
        Check.That(!Cache.SamePath(broken.CacheDir, broken.FavoritesDir), "папки кэша и избранного разведены");

        var huge = new AppSettings { IntervalMinutes = 999999 }.Normalized();
        Check.That(huge.IntervalMinutes == AppSettings.MaxInterval, "огромный интервал урезан");

        var shared = new AppSettings { CacheDir = @"C:\wallshall", FavoritesDir = @"C:\wallshall\" }.Normalized();
        Check.That(!Cache.SamePath(shared.CacheDir, shared.FavoritesDir),
                   "одинаковые папки разведены, иначе очистка кэша стёрла бы избранное");
    }
}
