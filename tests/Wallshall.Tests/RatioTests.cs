static class RatioTests
{
    public static void Run()
    {
        Check.Suite("Форма экрана");

        Check.Equal(RatioSelection.FromResolution(1920, 1080), "16x9", "1920x1080");
        Check.Equal(RatioSelection.FromResolution(2560, 1440), "16x9", "2560x1440");
        Check.Equal(RatioSelection.FromResolution(3840, 2160), "16x9", "4K");
        Check.Equal(RatioSelection.FromResolution(1366, 768), "16x9", "1366x768 — в пределах допуска");
        Check.Equal(RatioSelection.FromResolution(1920, 1200), "16x10", "1920x1200");
        Check.Equal(RatioSelection.FromResolution(2560, 1080), "21x9", "2560x1080");
        Check.Equal(RatioSelection.FromResolution(3440, 1440), "21x9", "3440x1440");
        Check.Equal(RatioSelection.FromResolution(3840, 1080), "32x9", "3840x1080");
        Check.Equal(RatioSelection.FromResolution(1080, 1920), "9x16", "повёрнутый экран");
        Check.Equal(RatioSelection.FromResolution(1024, 768), "4x3", "1024x768");
        Check.Equal(RatioSelection.FromResolution(1280, 1024), "5x4", "1280x1024");
        Check.Equal(RatioSelection.FromResolution(2000, 1000), "landscape", "необычная широкая");
        Check.Equal(RatioSelection.FromResolution(1000, 2400), "portrait", "необычная вытянутая");
        Check.Equal(RatioSelection.FromResolution(0, 0), "", "нулевой размер не валит");

        Check.Suite("Несколько экранов");

        Check.Equal(Screen((1920, 1080)), "16x9", "один экран");
        Check.Equal(Screen((1920, 1080), (2560, 1440)), "16x9", "два экрана одной формы");
        Check.Equal(Screen((3440, 1440), (1920, 1080)), "21x9,16x9", "разные формы — обе в списке");
        Check.Equal(Screen((1920, 1080), (1080, 1920)), "16x9,9x16", "горизонтальный и вертикальный");
        Check.Equal(Screen((1920, 1080), (2000, 1000)), "", "необычный рядом с обычным — фильтр снимается");
        Check.Equal(Screen((2000, 1000)), "landscape", "единственный необычный");
        Check.Equal(Screen(), "", "экранов нет");

        Check.Suite("Режимы пропорций");

        Check.Equal(Query(RatioSelection.ModeAny, "16x9", "16x9"), "", "«любые» не фильтруют");
        Check.Equal(Query(RatioSelection.ModeLandscape, "16x9", "16x9"), "landscape", "«горизонтальные»");
        Check.Equal(Query(RatioSelection.ModePortrait, "16x9", "16x9"), "portrait", "«вертикальные»");
        Check.Equal(Query(RatioSelection.ModeScreen, "16x9", "21x9"), "21x9", "«как на экране» берёт экран, а не список");
        Check.Equal(Query(RatioSelection.ModeCustom, "9x16,16x9", "21x9"), "16x9,9x16", "«свои» — список в стабильном порядке");
        Check.Equal(Query(RatioSelection.ModeCustom, "landscape,16x9", ""), "16x9", "ключевое слово в список не попадает");
        Check.Equal(Query(RatioSelection.ModeCustom, "", "16x9"), "", "пустой список — без фильтра");

        Check.Suite("Ручной набор");

        var selection = new RatioSelection();
        Check.That(selection.Empty, "по умолчанию пусто");

        selection.Toggle("16x9");
        selection.Toggle("9x16");
        Check.Equal(selection.Value, "16x9,9x16", "две пропорции складываются");

        selection.Toggle("16x9");
        Check.Equal(selection.Value, "9x16", "повторный клик снимает");

        selection.Value = "landscape,21x9";
        Check.Equal(selection.Value, "21x9", "ключевое слово из старых настроек отбрасывается");

        selection.Value = " 16X9 , , 9x16 ,16x9";
        Check.Equal(selection.Value, "16x9,9x16", "пробелы, регистр и дубли вычищаются");

        selection.Clear();
        Check.That(selection.Empty, "сброс");
    }

    static string Screen(params (int, int)[] monitors) => RatioSelection.FromMonitors(monitors);

    static string Query(string mode, string custom, string screen) =>
        RatioSelection.Query(mode, custom, screen);
}
