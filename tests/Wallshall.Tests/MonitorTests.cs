static class MonitorTests
{
    public static void Run()
    {
        Check.Suite("Имена мониторов");

        Check.Equal(Names((0, 0)), "Экран", "один монитор");
        Check.Equal(Names((0, 0), (1920, 0)), "Левый, Правый", "пара по горизонтали");
        Check.Equal(Names((1920, 0), (0, 0)), "Правый, Левый",
                    "имя назначается по позиции, а не по порядку перечисления");
        Check.Equal(Names((0, 0), (1920, 0), (3840, 0)), "Левый, Центральный, Правый", "три в ряд");
        Check.Equal(Names((0, 1080), (0, 0)), "Нижний, Верхний", "пара по вертикали");
        Check.Equal(Names((0, 0), (100, 0), (200, 0), (300, 0)),
                    "Монитор 1, Монитор 2, Монитор 3, Монитор 4", "четыре — по номерам");
        Check.Equal(Names((0, 0), (100, 0), (0, 100), (100, 100)),
                    "Монитор 1, Монитор 2, Монитор 3, Монитор 4", "сетка 2x2");
        Check.Equal(Names((0, 0), (0, 0)), "Монитор 1, Монитор 2", "клонированные экраны");
        Check.Equal(Names(), "", "мониторов нет");
    }

    static string Names(params (int Left, int Top)[] corners) =>
        string.Join(", ", MonitorLayout.Name(corners));
}
