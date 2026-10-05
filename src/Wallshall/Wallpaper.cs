using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;

record MonitorInfo(string Id, string Name, int Width, int Height);

static class Wallpaper
{
    public static List<MonitorInfo> Monitors()
    {
        var found = new List<(string Id, Rect Rect)>();
        try
        {
            var api = Create();
            if (api != null)
                for (uint i = 0; i < api.GetMonitorDevicePathCount(); i++)
                {
                    var id = api.GetMonitorDevicePathAt(i);
                    if (string.IsNullOrEmpty(id)) continue;
                    try { api.GetMonitorRECT(id, out var rect); found.Add((id, rect)); }
                    catch { }
                }
        }
        catch (Exception ex) { Debug.WriteLine(ex); }

        return Describe(found);
    }

    public static int Count() => Monitors().Count;

    /// Формы подключённых экранов для режима «как на экране».
    public static string ScreenRatios()
    {
        var sizes = Monitors().Select(m => (m.Width, m.Height)).ToList();
        if (sizes.Count == 0)
            sizes = Screen.AllScreens.Select(s => (s.Bounds.Width, s.Bounds.Height)).ToList();

        return RatioSelection.FromMonitors(sizes);
    }

    public static bool Set(IReadOnlyList<string> files)
    {
        if (files.Count == 0) return false;
        if (files.Count > 1 && SetPerMonitor(files)) return true;

        SetEverywhere(files[0]);
        return false;
    }

    static List<MonitorInfo> Describe(List<(string Id, Rect Rect)> found)
    {
        var result = new List<MonitorInfo>();
        if (found.Count == 0) return result;

        bool horizontal = found.Select(m => m.Rect.Left).Distinct().Count() == found.Count;
        bool vertical = !horizontal && found.Select(m => m.Rect.Top).Distinct().Count() == found.Count;

        var order = Enumerable.Range(0, found.Count).ToList();
        if (horizontal) order.Sort((a, b) => found[a].Rect.Left.CompareTo(found[b].Rect.Left));
        else if (vertical) order.Sort((a, b) => found[a].Rect.Top.CompareTo(found[b].Rect.Top));

        var labels = Labels(found.Count, horizontal, vertical);
        var names = new string[found.Count];
        for (int i = 0; i < order.Count; i++) names[order[i]] = labels[i];

        for (int i = 0; i < found.Count; i++)
        {
            var rect = found[i].Rect;
            result.Add(new MonitorInfo(found[i].Id, names[i], rect.Right - rect.Left, rect.Bottom - rect.Top));
        }
        return result;
    }

    static string[] Labels(int count, bool horizontal, bool vertical) =>
        count == 1 ? new[] { "Экран" } :
        horizontal && count == 2 ? new[] { "Левый", "Правый" } :
        horizontal && count == 3 ? new[] { "Левый", "Центральный", "Правый" } :
        vertical && count == 2 ? new[] { "Верхний", "Нижний" } :
        vertical && count == 3 ? new[] { "Верхний", "Средний", "Нижний" } :
        Enumerable.Range(1, count).Select(i => "Монитор " + i).ToArray();

    static bool SetPerMonitor(IReadOnlyList<string> files)
    {
        try
        {
            var api = Create();
            var monitors = Monitors();
            if (api == null || monitors.Count == 0) return false;

            api.SetPosition(DesktopWallpaperPosition.Fill);
            for (int i = 0; i < monitors.Count; i++)
            {
                try { api.SetWallpaper(monitors[i].Id, files[i % files.Count]); }
                catch (Exception ex) { Debug.WriteLine(ex); }
            }
            return true;
        }
        catch (Exception ex) { Debug.WriteLine(ex); return false; }
    }

    static void SetEverywhere(string path)
    {
        using (var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", writable: true))
        {
            key?.SetValue("WallpaperStyle", "10");
            key?.SetValue("TileWallpaper", "0");
        }
        SystemParametersInfo(20, 0, path, 3);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool SystemParametersInfo(int uiAction, int uiParam, string pvParam, int fWinIni);

    static IDesktopWallpaper? Create()
    {
        var type = Type.GetTypeFromCLSID(new Guid("C2CF3110-460E-4FC1-B9D0-8A1C0C9CC4BD"));
        return type == null ? null : Activator.CreateInstance(type) as IDesktopWallpaper;
    }

    enum DesktopWallpaperPosition { Center = 0, Tile = 1, Stretch = 2, Fit = 3, Fill = 4, Span = 5 }

    [ComImport, Guid("B92B56A9-8B55-4E14-9A89-0199BBB6F93B"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IDesktopWallpaper
    {
        void SetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string? monitorId,
                          [MarshalAs(UnmanagedType.LPWStr)] string wallpaper);

        [return: MarshalAs(UnmanagedType.LPWStr)]
        string GetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string? monitorId);

        [return: MarshalAs(UnmanagedType.LPWStr)]
        string GetMonitorDevicePathAt(uint monitorIndex);

        uint GetMonitorDevicePathCount();

        void GetMonitorRECT([MarshalAs(UnmanagedType.LPWStr)] string monitorId, out Rect displayRect);

        void SetBackgroundColor(uint color);
        uint GetBackgroundColor();
        void SetPosition(DesktopWallpaperPosition position);
        DesktopWallpaperPosition GetPosition();
    }

    [StructLayout(LayoutKind.Sequential)]
    struct Rect { public int Left, Top, Right, Bottom; }
}
