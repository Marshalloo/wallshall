/// Причины, по которым настройки нельзя сохранить. Чистая функция: про контролы,
/// диалоги и файловую систему ничего не знает, поэтому проверяется тестами.
static class SettingsRules
{
    public static readonly string NoCategory = "Выберите хотя бы одну категорию.";
    public static readonly string NoPurity = "Выберите хотя бы один тип контента.";
    public static readonly string NsfwNeedsKey = "Для NSFW нужен API-ключ.";
    public static readonly string BadInterval =
        $"Интервал — целое число минут от {AppSettings.MinInterval} до {AppSettings.MaxInterval}.";
    public static readonly string BadCacheDir = "Не удалось использовать эту папку для кэша.";
    public static readonly string BadFavoritesDir = "Не удалось использовать эту папку для избранного.";
    public static readonly string SharedFolders =
        "Папки кэша и избранного должны быть разными, иначе очистка кэша удалит избранное.";
    public static readonly string EmptyFavorites =
        "В папке избранного нет картинок — режим «Только избранное» включить не получится.";

    /// Первая причина отказа или null, если сохранять можно.
    public static string? Problem(AppSettings wanted, bool cacheUsable, bool favoritesUsable, int favoritesCount)
    {
        if (!wanted.AnyCategory) return NoCategory;
        if (!wanted.AnyPurity) return NoPurity;
        if (wanted.Nsfw && wanted.ApiKey == "") return NsfwNeedsKey;
        if (!wanted.IntervalValid) return BadInterval;

        if (!cacheUsable) return BadCacheDir;
        if (!favoritesUsable) return BadFavoritesDir;

        if (Cache.SamePath(wanted.CacheDir, wanted.FavoritesDir)) return SharedFolders;
        if (wanted.FavoritesOnly && favoritesCount == 0) return EmptyFavorites;

        return null;
    }
}
