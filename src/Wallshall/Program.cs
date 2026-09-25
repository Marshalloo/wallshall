using System.Diagnostics;

static class Program
{
    [STAThread]
    static void Main()
    {
        using var mutex = new Mutex(true, "Wallshall_SingleInstance", out bool first);
        if (!first) return;

        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => Report(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Report(e.ExceptionObject as Exception);

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApp());
    }

    static void Report(Exception? ex)
    {
        Debug.WriteLine(ex);
        if (ex == null) return;
        try { DarkMessage.Info($"Что-то пошло не так:\n\n{ex.GetType().Name}: {ex.Message}"); } catch { }
    }
}
