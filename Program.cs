static class Program
{
    [STAThread]
    static void Main()
    {
        // Не даём запустить вторую копию
        using var mutex = new Mutex(true, "Wallshall_SingleInstance", out bool first);
        if (!first) return;

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApp());
    }
}
