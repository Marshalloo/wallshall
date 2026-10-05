/// Пропорции обоев: режим, набор выбранных вручную и определение формы экрана.
///
/// Wallhaven понимает landscape и portrait, но только сами по себе: на запрос
/// ratios=portrait,16x9 сайт молча отдаёт одни 16x9. Поэтому ключевые слова здесь
/// не входят в ручной набор, а живут отдельными режимами.
class RatioSelection
{
    public const string ModeScreen = "screen";
    public const string ModeAny = "any";
    public const string ModeLandscape = "landscape";
    public const string ModePortrait = "portrait";
    public const string ModeCustom = "custom";

    /// Пропорции для ручного выбора, от самой широкой к самой вытянутой.
    public static readonly string[] Known =
    {
        "48x9", "32x9", "21x9", "16x9", "16x10", "3x2",
        "4x3", "5x4", "1x1", "10x16", "9x16", "9x18",
    };

    /// Насколько форма экрана может отличаться от табличной, чтобы считаться ею же.
    const double Tolerance = 0.03;

    readonly List<string> chosen = new();

    public bool Empty => chosen.Count == 0;

    public bool Has(string value) => chosen.Contains(value);

    public void Clear() => chosen.Clear();

    public void Toggle(string value)
    {
        if (chosen.Remove(value)) return;
        chosen.Add(value);
    }

    public string Value
    {
        get => string.Join(",", chosen.OrderBy(Rank));
        set
        {
            chosen.Clear();
            chosen.AddRange(Parse(value).Where(token => !IsKeyword(token)));
        }
    }

    public static bool IsKeyword(string value) => value == ModeLandscape || value == ModePortrait;

    public static List<string> Parse(string value) =>
        value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(token => token.ToLowerInvariant())
            .Distinct()
            .ToList();

    /// Что уйдёт в параметр ratios. Пусто — параметр не отправляется.
    public static string Query(string mode, string custom, string screen) => mode switch
    {
        ModeAny => "",
        ModeLandscape => ModeLandscape,
        ModePortrait => ModePortrait,
        ModeScreen => screen,
        _ => string.Join(",", Parse(custom).Where(token => !IsKeyword(token)).OrderBy(Rank)),
    };

    /// Ближайшая табличная пропорция; если форма необычная — ключевое слово по ориентации.
    public static string FromResolution(int width, int height)
    {
        if (width <= 0 || height <= 0) return "";

        double ratio = (double)width / height;
        var best = Known.OrderBy(token => Math.Abs(Ratio(token) - ratio)).First();

        return Math.Abs(Ratio(best) - ratio) <= ratio * Tolerance
            ? best
            : ratio >= 1 ? ModeLandscape : ModePortrait;
    }

    /// Формы всех экранов одной строкой. Если среди них есть необычная,
    /// фильтр не ставится вовсе: ключевое слово со списком не сочетается.
    public static string FromMonitors(IEnumerable<(int Width, int Height)> monitors)
    {
        var tokens = monitors
            .Select(m => FromResolution(m.Width, m.Height))
            .Where(token => token != "")
            .Distinct()
            .ToList();

        if (tokens.Count == 0) return "";
        if (tokens.Count == 1) return tokens[0];

        return tokens.Any(IsKeyword) ? "" : string.Join(",", tokens.OrderBy(Rank));
    }

    public static double Ratio(string token)
    {
        var parts = token.Split('x');
        return parts.Length == 2 &&
               double.TryParse(parts[0], out double w) &&
               double.TryParse(parts[1], out double h) && h > 0
            ? w / h
            : 1;
    }

    static int Rank(string value)
    {
        int index = Array.IndexOf(Known, value);
        return index < 0 ? Known.Length : index;
    }
}
