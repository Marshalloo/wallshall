using System.Runtime.InteropServices;

static class Shell
{
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern IntPtr ShellExecuteW(IntPtr hwnd, string? verb, string file,
                                       string? parameters, string? directory, int showCmd);

    const int SW_SHOWNORMAL = 1;

    public static bool Open(string target) => Run("open", target, null);

    public static bool Reveal(string file) =>
        File.Exists(file) && Run("open", "explorer.exe", $"/select,\"{file}\"");

    static bool Run(string verb, string file, string? parameters)
    {
        try
        {
            return (long)ShellExecuteW(IntPtr.Zero, verb, file, parameters, null, SW_SHOWNORMAL) > 32;
        }
        catch
        {
            return false;
        }
    }
}
