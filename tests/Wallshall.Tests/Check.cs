/// Крошечный прогон тестов без сторонних пакетов: программе хватает, а лишней
/// зависимости в сборке не появляется.
static class Check
{
    static int failed;
    static string suite = "";

    public static void Suite(string name)
    {
        suite = name;
        Console.WriteLine();
        Console.WriteLine(name);
    }

    public static void That(bool passed, string what)
    {
        Console.WriteLine((passed ? "  ok     " : "  ПРОВАЛ ") + what);
        if (!passed) failed++;
    }

    public static void Equal(string actual, string expected, string what) =>
        That(actual == expected, actual == expected ? what : $"{what} — ожидалось «{expected}», получено «{actual}»");

    public static int Report()
    {
        Console.WriteLine();
        Console.WriteLine(failed == 0 ? "ВСЕ ПРОВЕРКИ ПРОШЛИ" : $"ПРОВАЛОВ: {failed}");
        return failed;
    }

    /// Пустой каталог под временные файлы теста.
    public static string TempDir(string name)
    {
        var dir = Path.Combine(Path.GetTempPath(), "wallshall-tests", suite, name);
        if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        Directory.CreateDirectory(dir);
        return dir;
    }
}
