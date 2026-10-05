using System.Drawing.Drawing2D;

/// Плитка с нарисованной пропорцией: форму видно, не вчитываясь в числа.
class RatioTile : Control
{
    bool hover, chosen;

    public string Ratio { get; }

    public RatioTile(string ratio)
    {
        Ratio = ratio;
        Text = ratio.Replace("x", "×");

        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                 ControlStyles.Selectable, true);
        Size = new Size(64, 68);
        Margin = new Padding(0, 0, 8, 8);
        TabStop = true;
        Cursor = Cursors.Hand;
    }

    public bool Chosen
    {
        get => chosen;
        set { if (chosen == value) return; chosen = value; Invalidate(); }
    }

    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { Focus(); base.OnMouseDown(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

    protected override bool IsInputKey(Keys keyData) =>
        keyData is Keys.Space or Keys.Enter || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode is not (Keys.Space or Keys.Enter)) return;

        OnClick(EventArgs.Empty);
        e.Handled = true;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? Theme.Card);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var box = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
        using (var path = Theme.RoundRect(box, LogicalToDeviceUnits(4)))
        {
            var back = chosen ? Theme.Tint(Theme.Accent, 0.18f) : hover ? Theme.ControlHover : Theme.Field;
            using var fill = new SolidBrush(back);
            using var pen = new Pen(chosen ? Theme.Accent : Theme.Border);
            g.FillPath(fill, path);
            g.DrawPath(pen, path);
        }

        PaintShape(g);

        var label = new Rectangle(0, Height - LogicalToDeviceUnits(20), Width, LogicalToDeviceUnits(18));
        TextRenderer.DrawText(g, Text, Font, label, chosen ? Theme.Text : Theme.TextSecondary,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
            TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);

        if (!Focused || !ShowFocusCues) return;

        using var ring = Theme.RoundRect(new RectangleF(1.5f, 1.5f, Width - 3f, Height - 3f), LogicalToDeviceUnits(3));
        using var ringPen = new Pen(Theme.Text, 1.5f);
        g.DrawPath(ringPen, ring);
    }

    void PaintShape(Graphics g)
    {
        float span = LogicalToDeviceUnits(38);
        double ratio = RatioSelection.Ratio(Ratio);

        float w = ratio >= 1 ? span : (float)(span * ratio);
        float h = ratio >= 1 ? (float)(span / ratio) : span;
        h = Math.Max(h, LogicalToDeviceUnits(4));

        float cy = (Height - LogicalToDeviceUnits(18)) / 2f;
        var shape = new RectangleF((Width - w) / 2f, cy - h / 2f, w, h);

        using var path = Theme.RoundRect(shape, LogicalToDeviceUnits(2));
        using var brush = new SolidBrush(chosen ? Theme.Accent : Theme.ShapeIdle);
        g.FillPath(brush, path);
    }
}

/// Пропорции: режим сверху, ручной выбор — плитками под ним.
class RatioPicker : TableLayoutPanel
{
    readonly RatioSelection selection = new();
    readonly DarkSegments modes = new();
    readonly FlowLayoutPanel tiles = new();
    readonly Label hint = new();
    readonly List<RatioTile> all = new();

    public event EventHandler? ValueChanged;

    public RatioPicker()
    {
        ColumnCount = 1;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Margin = Padding.Empty;

        modes.Add(RatioSelection.ModeScreen, "Как на экране")
             .Add(RatioSelection.ModeAny, "Любые")
             .Add(RatioSelection.ModeLandscape, "Горизонтальные")
             .Add(RatioSelection.ModePortrait, "Вертикальные")
             .Add(RatioSelection.ModeCustom, "Свои");
        modes.ValueChanged += (_, _) => { Sync(); Changed(); };
        Controls.Add(modes);

        tiles.AutoSize = true;
        tiles.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        tiles.MaximumSize = new Size(440, 0);
        tiles.Margin = new Padding(0, 10, 0, 0);
        foreach (var ratio in RatioSelection.Known)
        {
            var tile = new RatioTile(ratio);
            tile.Click += (_, _) => { selection.Toggle(ratio); Sync(); Changed(); };
            all.Add(tile);
            tiles.Controls.Add(tile);
        }
        Controls.Add(tiles);

        hint.AutoSize = true;
        hint.MaximumSize = new Size(440, 0);
        hint.ForeColor = Theme.TextSecondary;
        hint.Margin = new Padding(0, 8, 0, 2);
        Controls.Add(hint);

        Sync();
    }

    public string Mode
    {
        get => modes.Value;
        set { modes.Value = value; Sync(); }
    }

    public string Value
    {
        get => selection.Value;
        set { selection.Value = value; Sync(); }
    }

    void Changed() => ValueChanged?.Invoke(this, EventArgs.Empty);

    void Sync()
    {
        bool custom = modes.Value == RatioSelection.ModeCustom;
        tiles.Visible = custom;

        foreach (var tile in all) tile.Chosen = selection.Has(tile.Ratio);

        hint.Text = custom
            ? selection.Empty ? "Ничего не выбрано — подойдут обои любой формы." : ""
            : Hint(modes.Value);
        hint.Visible = hint.Text != "";
    }

    static string Hint(string mode) => mode switch
    {
        RatioSelection.ModeScreen => ScreenHint(),
        RatioSelection.ModeLandscape => "Любые горизонтальные, включая сверхширокие и близкие к квадрату.",
        RatioSelection.ModePortrait => "Любые вертикальные — для монитора, повёрнутого на бок.",
        _ => "Подойдут обои любой формы.",
    };

    static string ScreenHint()
    {
        var screen = AppSettings.ScreenRatios();
        if (screen == "") return "Форму экрана определить не удалось — фильтр по пропорциям не ставится.";

        if (RatioSelection.IsKeyword(screen))
            return screen == RatioSelection.ModeLandscape
                ? "У экрана необычная форма — берём любые горизонтальные."
                : "У экрана необычная форма — берём любые вертикальные.";

        var names = string.Join(" и ", RatioSelection.Parse(screen).Select(r => r.Replace("x", "×")));
        return $"Определено по экрану: {names}.";
    }
}
