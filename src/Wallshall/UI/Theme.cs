using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using Microsoft.Win32;

static class Theme
{
    public static readonly Color Bg = Color.FromArgb(32, 32, 32);
    public static readonly Color Card = Color.FromArgb(43, 43, 43);
    public static readonly Color CardBorder = Color.FromArgb(29, 29, 29);
    public static readonly Color MenuBg = Color.FromArgb(44, 44, 44);
    public static readonly Color MenuHover = Color.FromArgb(58, 58, 58);
    public static readonly Color Field = Color.FromArgb(50, 50, 50);
    public static readonly Color FieldHover = Color.FromArgb(56, 56, 56);
    public static readonly Color FieldFocus = Color.FromArgb(31, 31, 31);
    public static readonly Color Border = Color.FromArgb(62, 62, 62);
    public static readonly Color BorderBottom = Color.FromArgb(150, 150, 150);
    public static readonly Color Control = Color.FromArgb(55, 55, 55);
    public static readonly Color ControlHover = Color.FromArgb(62, 62, 62);
    public static readonly Color ControlPressed = Color.FromArgb(48, 48, 48);
    public static readonly Color Text = Color.White;
    public static readonly Color TextSecondary = Color.FromArgb(200, 200, 200);
    public static readonly Color TextDisabled = Color.FromArgb(120, 120, 120);
    public static readonly Color ShapeIdle = Color.FromArgb(154, 154, 154);
    public static readonly Color Success = Color.FromArgb(108, 203, 95);
    public static readonly Color Error = Color.FromArgb(255, 153, 164);
    public static readonly Color Warning = Color.FromArgb(252, 225, 0);

    public static readonly Color Accent = ReadAccent();
    public static readonly Color AccentHover = Blend(Accent, Color.Black, 0.1f);
    public static readonly Color AccentPressed = Blend(Accent, Color.Black, 0.2f);
    public static readonly Color OnAccent = Color.Black;

    public static readonly Font Font = MakeFont(10f, FontStyle.Regular, "Segoe UI Variable Text", "Segoe UI");
    public static readonly Font FontSection = MakeFont(10.5f, FontStyle.Regular, "Segoe UI Semibold", "Segoe UI");
    static readonly string IconFont = FontExists("Segoe Fluent Icons") ? "Segoe Fluent Icons" : "Segoe MDL2 Assets";

    public static readonly float Scale = GetScale();
    public static int S(float v) => (int)Math.Round(v * Scale);

    public static Icon LoadAppIcon(Size? size = null)
    {
        using var s = typeof(Theme).Assembly.GetManifestResourceStream("wallpaper.ico");
        if (s == null) return SystemIcons.Application;
        return size is Size sz ? new Icon(s, sz) : new Icon(s);
    }

    public static Bitmap Glyph(char ch, int px, Color color)
    {
        var bmp = new Bitmap(px, px);
        using var g = Graphics.FromImage(bmp);
        g.TextRenderingHint = TextRenderingHint.AntiAlias;
        using var font = new Font(IconFont, px, GraphicsUnit.Pixel);
        using var brush = new SolidBrush(color);
        using var fmt = new StringFormat(StringFormat.GenericTypographic)
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };
        g.DrawString(ch.ToString(), font, brush, new RectangleF(0, 0, px, px), fmt);
        return bmp;
    }

    public static Size MenuImage { get; } = new(S(28), S(16));

    static readonly Dictionary<char, Bitmap> menuGlyphs = new();

    public static Bitmap MenuGlyph(char ch)
    {
        if (menuGlyphs.TryGetValue(ch, out var cached)) return cached;

        var bmp = new Bitmap(MenuImage.Width, MenuImage.Height);
        using (var g = Graphics.FromImage(bmp))
        using (var icon = Glyph(ch, S(16), Text))
            g.DrawImage(icon, (MenuImage.Width - icon.Width) / 2, (MenuImage.Height - icon.Height) / 2);

        menuGlyphs[ch] = bmp;
        return bmp;
    }

    public static Bitmap? Thumbnail(string file)
    {
        try
        {
            using var stream = new MemoryStream(File.ReadAllBytes(file));
            using var source = Image.FromStream(stream, useEmbeddedColorManagement: false, validateImageData: false);

            var bmp = new Bitmap(MenuImage.Width, MenuImage.Height);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;

                using var clip = RoundRect(new RectangleF(0, 0, MenuImage.Width, MenuImage.Height), S(2));
                g.SetClip(clip);

                float scale = Math.Max((float)MenuImage.Width / source.Width, (float)MenuImage.Height / source.Height);
                float w = source.Width * scale, h = source.Height * scale;
                g.DrawImage(source, (MenuImage.Width - w) / 2, (MenuImage.Height - h) / 2, w, h);
            }
            return bmp;
        }
        catch (Exception ex) { Debug.WriteLine(ex); return null; }
    }

    public static GraphicsPath RoundRect(RectangleF r, float radius)
    {
        var p = new GraphicsPath();
        float d = Math.Max(1, radius * 2);
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    /// Цвет поверх фона карточки — для мягкой подсветки выбранного.
    public static Color Tint(Color color, float alpha) => Blend(Card, color, alpha);

    public static Color Blend(Color a, Color b, float t) => Color.FromArgb(
        (int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));

    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    const int DWMWA_BORDER_COLOR = 34;
    const int DWMWA_CAPTION_COLOR = 35;

    public static void DarkTitleBar(IntPtr hwnd)
    {
        int on = 1;
        DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref on, 4);
        int caption = ColorRef(Bg);
        DwmSetWindowAttribute(hwnd, DWMWA_CAPTION_COLOR, ref caption, 4);
    }

    public static void RoundPopup(IntPtr hwnd)
    {
        int round = 2;
        DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref round, 4);
        int border = ColorRef(Border);
        DwmSetWindowAttribute(hwnd, DWMWA_BORDER_COLOR, ref border, 4);
    }

    static int ColorRef(Color c) => c.R | (c.G << 8) | (c.B << 16);

    static Color ReadAccent()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Accent");

            if (key?.GetValue("AccentPalette") is byte[] p && p.Length >= 8)
                return Color.FromArgb(p[4], p[5], p[6]);
        }
        catch { }
        return Color.FromArgb(76, 194, 255);
    }

    static float GetScale()
    {
        using var g = Graphics.FromHwnd(IntPtr.Zero);
        return g.DpiX / 96f;
    }

    static bool FontExists(string name)
    {
        using var f = new Font(name, 10f);
        return f.Name.Equals(name, StringComparison.OrdinalIgnoreCase);
    }

    static Font MakeFont(float size, FontStyle style, params string[] names)
    {
        foreach (var name in names)
            if (FontExists(name)) return new Font(name, size, style);
        return new Font(SystemFonts.MessageBoxFont!.FontFamily, size, style);
    }
}
