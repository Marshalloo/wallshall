/// Имена мониторов по их расположению. Чистая функция: COM и WinForms сюда не входят,
/// поэтому раскладку можно проверить тестом, не имея второго монитора.
static class MonitorLayout
{
    /// Имена в том же порядке, в каком экраны пришли, но назначенные по позиции:
    /// второй в перечислении экран вполне может оказаться левым.
    public static string[] Name(IReadOnlyList<(int Left, int Top)> corners)
    {
        if (corners.Count == 0) return Array.Empty<string>();

        bool horizontal = Distinct(corners, c => c.Left);
        bool vertical = !horizontal && Distinct(corners, c => c.Top);

        var order = Enumerable.Range(0, corners.Count).ToList();
        if (horizontal) order.Sort((a, b) => corners[a].Left.CompareTo(corners[b].Left));
        else if (vertical) order.Sort((a, b) => corners[a].Top.CompareTo(corners[b].Top));

        var labels = Labels(corners.Count, horizontal, vertical);
        var names = new string[corners.Count];
        for (int i = 0; i < order.Count; i++) names[order[i]] = labels[i];

        return names;
    }

    static bool Distinct<T>(IReadOnlyList<T> items, Func<T, int> key) =>
        items.Select(key).Distinct().Count() == items.Count;

    static string[] Labels(int count, bool horizontal, bool vertical) =>
        count == 1 ? new[] { "Экран" } :
        horizontal && count == 2 ? new[] { "Левый", "Правый" } :
        horizontal && count == 3 ? new[] { "Левый", "Центральный", "Правый" } :
        vertical && count == 2 ? new[] { "Верхний", "Нижний" } :
        vertical && count == 3 ? new[] { "Верхний", "Средний", "Нижний" } :
        Enumerable.Range(1, count).Select(i => "Монитор " + i).ToArray();
}
