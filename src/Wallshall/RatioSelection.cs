/// Набор выбранных пропорций и правила выбора, без привязки к интерфейсу.
///
/// Wallhaven понимает landscape и portrait, но только сами по себе: на запрос
/// ratios=portrait,16x9 сайт молча отдаёт одни 16x9. Поэтому «Все …» и обычные
/// пропорции здесь взаимно исключаются.
class RatioSelection
{
    public const string AllWide = "landscape";
    public const string AllPortrait = "portrait";

    public static readonly string[] Known =
    {
        AllWide, AllPortrait,
        "16x9", "16x10",
        "21x9", "32x9", "48x9",
        "9x16", "10x16", "9x18",
        "1x1", "3x2", "4x3", "5x4",
    };

    readonly List<string> chosen = new();

    public bool Empty => chosen.Count == 0;

    public static bool IsAll(string value) => value == AllWide || value == AllPortrait;

    public bool Has(string value) => chosen.Contains(value);

    public void Clear() => chosen.Clear();

    public void Toggle(string value)
    {
        if (chosen.Remove(value)) return;

        chosen.RemoveAll(other => IsAll(other) != IsAll(value));
        chosen.Add(value);
    }

    public string Value
    {
        get => string.Join(",", chosen.OrderBy(Rank));
        set
        {
            chosen.Clear();
            chosen.AddRange(Parse(value));
            if (chosen.Any(IsAll)) chosen.RemoveAll(v => !IsAll(v));
        }
    }

    public static List<string> Parse(string value) =>
        value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(token => token.ToLowerInvariant())
            .Distinct()
            .ToList();

    static int Rank(string value)
    {
        int index = Array.IndexOf(Known, value);
        return index < 0 ? Known.Length : index;
    }
}
