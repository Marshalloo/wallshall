using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;



static class Wallpaper
{

    public static int MonitorCount()
    {
        try
        {
            var api = Create();
            if (api == null) return 0;
            int count = 0;
            for (uint i = 0; i < api.GetMonitorDevicePathCount(); i++)
            {
                var id = api.GetMonitorDevicePathAt(i);
                if (string.IsNullOrEmpty(id)) continue;
                try { api.GetMonitorRECT(id, out _); count++; }
                catch { }
            }
            return count;
        }
        catch (Exception ex) { Debug.WriteLine(ex); return 0; }
    }


    public static void Set(IReadOnlyList<string> files)
    {
        if (files.Count == 0) return;
        if (files.Count > 1 && SetPerMonitor(files)) return;
        SetEverywhere(files[0]);
    }

    static bool SetPerMonitor(IReadOnlyList<string> files)
    {
        try
        {
            var api = Create();
            if (api == null) return false;

            api.SetPosition(DesktopWallpaperPosition.Fill);
            int n = 0;
            for (uint i = 0; i < api.GetMonitorDevicePathCount(); i++)
            {
                var id = api.GetMonitorDevicePathAt(i);
                if (string.IsNullOrEmpty(id)) continue;
                try
                {
                    api.GetMonitorRECT(id, out _);
                    api.SetWallpaper(id, files[n % files.Count]);
                    n++;
                }
                catch (Exception ex) { Debug.WriteLine(ex); }
            }
            return n > 0;
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
