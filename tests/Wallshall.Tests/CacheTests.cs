static class CacheTests
{
    public static void Run()
    {
        Check.Suite("Кэш и избранное");

        var cache = Check.TempDir("cache");
        var favorites = Check.TempDir("favorites");

        for (int i = 1; i <= 5; i++) File.WriteAllText(Path.Combine(cache, $"wallhaven-c{i}.jpg"), "x");
        File.WriteAllText(Path.Combine(cache, "чужая картинка.jpg"), "x");
        File.WriteAllText(Path.Combine(favorites, "моё фото.png"), "x");

        Check.That(Cache.Images(cache).Count() == 5, "в кэше видны только файлы wallhaven-*");
        Check.That(Favorites.Count(favorites) == 1, "в избранном видны любые картинки");

        var first = Path.Combine(cache, "wallhaven-c1.jpg");
        Check.That(Favorites.Add(favorites, first), "добавление в избранное");
        Check.That(Favorites.Contains(favorites, first), "файл считается избранным");
        Check.That(Favorites.Count(favorites) == 2, "в избранном стало два");
        Check.That(Favorites.Add(favorites, first) && Favorites.Count(favorites) == 2, "повторное добавление не дублирует");
        Check.That(File.Exists(first), "оригинал в кэше остался");

        Check.That(Favorites.Remove(favorites, first), "удаление из избранного");
        Check.That(File.Exists(first), "кэш при этом не пострадал");

        Favorites.Add(favorites, first);
        Cache.DeleteAll(cache);
        Check.That(!Cache.Images(cache).Any(), "кэш очищен");
        Check.That(Favorites.Count(favorites) == 2, "избранное пережило очистку кэша");
        Check.That(File.Exists(Path.Combine(cache, "чужая картинка.jpg")), "чужие файлы в папке кэша не трогаются");

        Check.Suite("Ротация избранного по кругу");

        var ring = Check.TempDir("ring");
        File.WriteAllText(Path.Combine(ring, "a.jpg"), "x");
        File.WriteAllText(Path.Combine(ring, "b.jpg"), "x");

        var shown = new List<string>();
        var counts = new Dictionary<string, int>();
        string? previous = null;
        bool noRepeats = true;

        for (int i = 0; i < 20; i++)
        {
            var name = Path.GetFileName(Favorites.Pick(ring, shown, 1)[0]);
            counts[name] = counts.GetValueOrDefault(name) + 1;
            if (name == previous) noRepeats = false;
            previous = name;
            shown.Add(name);
        }

        Check.That(counts.Count == 2 && counts.Values.All(v => v == 10), "за десять кругов каждая показана поровну");
        Check.That(noRepeats, "подряд одна и та же не идёт");

        var pair = Favorites.Pick(ring, new List<string>(), 2);
        Check.That(pair.Count == 2 && pair[0] != pair[1], "для двух мониторов берутся разные");

        Check.That(Favorites.Pick(ring, new List<string>(), 3).Count == 2, "картинок меньше, чем мониторов");
        Check.That(Favorites.Pick(Path.Combine(ring, "нет"), new List<string>(), 1).Count == 0, "пустая папка не валит");

        Check.Suite("Ссылка на страницу Wallhaven");

        Check.Equal(Cache.WallhavenId("wallhaven-o5kxm9.jpg") ?? "", "o5kxm9", "идентификатор из имени файла");
        Check.That(Cache.WallhavenId("моё фото.png") == null, "у своей картинки идентификатора нет");
    }
}
