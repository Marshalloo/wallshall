using System.Runtime.InteropServices;

static class Shell
{
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern IntPtr ShellExecuteW(IntPtr hwnd, string? verb, string file,
                                       string? parameters, string? directory, int showCmd);

    const int SW_SHOWNORMAL = 1;

    public static bool Open(string target)
    {
        try
        {
            return (long)ShellExecuteW(IntPtr.Zero, "open", target, null, null, SW_SHOWNORMAL) > 32;
        }
        catch
        {
            return false;
        }
    }
}
