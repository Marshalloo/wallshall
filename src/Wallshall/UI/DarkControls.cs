using System.Drawing.Drawing2D;

class DarkMenu : ContextMenuStrip
{
    public DarkMenu()
    {
        Renderer = new DarkMenuRenderer();
        BackColor = Theme.MenuBg;
        ForeColor = Theme.Text;
        Font = Theme.Font;
        Padding = new Padding(Theme.S(4));
        DropShadowEnabled = false;
        ImageScalingSize = new Size(Theme.S(16), Theme.S(16));
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Theme.RoundPopup(Handle);
    }

    public ToolStripMenuItem AddItem(string text, char? glyph, EventHandler onClick)
    {
        var image = glyph is char g ? Theme.Glyph(g, Theme.S(16), Theme.Text) : null;
        var item = new ToolStripMenuItem(text, image, onClick)
        {
            Padding = new Padding(Theme.S(4), Theme.S(6), Theme.S(12), Theme.S(6)),
            ForeColor = Theme.Text,
        };
        Items.Add(item);
        return item;
    }

    public void AddSeparator() => Items.Add(new ToolStripSeparator());
}

class DarkMenuRenderer : ToolStripRenderer
{
    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e) =>
        e.Graphics.Clear(Theme.MenuBg);

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e) { }
    protected override void OnRenderImageMargin(ToolStripRenderEventArgs e) { }
    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e) { }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        if (e.Item is not ToolStripMenuItem item) return;
        bool selected = item.Selected && item.Enabled;
        if (!selected && !item.Checked) return;

        var g = e.Graphics;
        float k = e.ToolStrip!.DeviceDpi / 96f;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var r = new RectangleF(2 * k, 1 * k, item.Width - 4 * k, item.Height - 2 * k);
        using (var path = Theme.RoundRect(r, 4 * k))
        using (var b = new SolidBrush(Theme.MenuHover))
            g.FillPath(b, path);


        if (item.Checked)
        {
            float h = item.Height * 0.4f;
            using var pill = Theme.RoundRect(new RectangleF(2 * k, (item.Height - h) / 2, 3 * k, h), 1.5f * k);
            using var ab = new SolidBrush(Theme.Accent);
            g.FillPath(ab, pill);
        }
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Enabled ? Theme.Text : Theme.TextDisabled;
        base.OnRenderItemText(e);
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        int y = e.Item.Height / 2;
        using var pen = new Pen(Theme.Border);
        e.Graphics.DrawLine(pen, 0, y, e.Item.Width, y);
    }

    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
    {
        e.ArrowColor = Theme.TextSecondary;
        base.OnRenderArrow(e);
    }
}





class DarkForm : Form
{
    public DarkForm()
    {
        SuspendLayout();
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Theme.Bg;
        ForeColor = Theme.Text;
        Font = Theme.Font;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Icon = Theme.LoadAppIcon();
    }


    protected void FinishLayout()
    {
        ResumeLayout(false);
        PerformLayout();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Theme.DarkTitleBar(Handle);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        Activate();
    }
}


class DarkMessage : DarkForm
{
    DarkMessage(string text, bool question)
    {
        Text = "Wallshall";
        ShowInTaskbar = false;

        var root = new TableLayoutPanel
        {
            ColumnCount = 1, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(24, 20, 24, 20),
        };
        root.Controls.Add(new Label
        {
            Text = text, AutoSize = true, MaximumSize = new Size(380, 0),
            Margin = new Padding(0, 0, 0, 20),
        });

        var ok = new DarkButton { Text = question ? "Да" : "OK", Primary = true, DialogResult = DialogResult.OK };
        var buttons = UI.Row(ok);
        if (question)
        {
            var no = new DarkButton { Text = "Нет", DialogResult = DialogResult.Cancel };
            buttons.Controls.Add(no);
            CancelButton = no;
        }
        else CancelButton = ok;
        AcceptButton = ok;
        buttons.Anchor = AnchorStyles.Right;
        root.Controls.Add(buttons);

        Controls.Add(root);
        FinishLayout();
    }

    public static bool Ask(string text, IWin32Window? owner = null) => Show(text, true, owner);
    public static void Info(string text, IWin32Window? owner = null) => Show(text, false, owner);

    static bool Show(string text, bool question, IWin32Window? owner)
    {
        using var f = new DarkMessage(text, question);
        if (owner != null) f.StartPosition = FormStartPosition.CenterParent;
        else f.TopMost = true;
        return (owner != null ? f.ShowDialog(owner) : f.ShowDialog()) == DialogResult.OK;
    }
}





static class UI
{

    public static FlowLayoutPanel Row(params Control[] controls)
    {
        var p = new FlowLayoutPanel
        {
            AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = false, Margin = new Padding(0),
        };
        foreach (var c in controls)
        {
            c.Anchor = AnchorStyles.Left;
            c.Margin = new Padding(0, 0, 8, 0);
            p.Controls.Add(c);
        }
        return p;
    }
}


class Card : Panel
{
    readonly TableLayoutPanel grid = new()
    {
        ColumnCount = 2,
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
        BackColor = Theme.Card,
        Margin = Padding.Empty,
        Location = new Point(16, 8),
    };

    public Card()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = Theme.Bg;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(16, 8, 16, 8);
        Margin = new Padding(0, 0, 0, 4);
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        Controls.Add(grid);
    }

    public void AddRow(string label, params Control[] controls)
    {
        grid.Controls.Add(new Label
        {
            Text = label, AutoSize = true, Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 8, 12, 8),
        });
        var row = UI.Row(controls);
        row.Margin = new Padding(0, 6, 0, 6);
        grid.Controls.Add(row);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = Theme.RoundRect(new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f), LogicalToDeviceUnits(6));
        using var b = new SolidBrush(Theme.Card);
        using var p = new Pen(Theme.CardBorder);
        g.FillPath(b, path);
        g.DrawPath(p, path);
    }
}





class DarkButton : Button
{
    bool hover, pressed;

    public bool Primary { get; set; }

    public DarkButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer, true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        AutoSize = true;
        Padding = new Padding(12, 4, 12, 4);
        MinimumSize = new Size(96, 32);
    }

    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { pressed = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? Theme.Bg);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        Color fill, text;
        if (!Enabled) { fill = Theme.Control; text = Theme.TextDisabled; }
        else if (Primary)
        {
            fill = pressed ? Theme.AccentPressed : hover ? Theme.AccentHover : Theme.Accent;
            text = Theme.OnAccent;
        }
        else
        {
            fill = pressed ? Theme.ControlPressed : hover ? Theme.ControlHover : Theme.Control;
            text = pressed ? Theme.TextSecondary : Theme.Text;
        }

        var r = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
        using (var path = Theme.RoundRect(r, LogicalToDeviceUnits(4)))
        {
            using (var b = new SolidBrush(fill)) g.FillPath(b, path);
            if (!Primary)
                using (var p = new Pen(Theme.Border)) g.DrawPath(p, path);
        }

        if (Focused && ShowFocusCues)
        {
            using var fp = Theme.RoundRect(new RectangleF(1.5f, 1.5f, Width - 3f, Height - 3f), LogicalToDeviceUnits(3));
            using var pen = new Pen(Theme.Text, 1.5f);
            g.DrawPath(pen, fp);
        }

        TextRenderer.DrawText(g, Text, Font, ClientRectangle, text,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
            TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
    }
}





class DarkCheckBox : CheckBox
{
    bool hover;


    public bool Toggle { get; set; }

    public DarkCheckBox()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        AutoSize = true;
    }

    int S(float v) => LogicalToDeviceUnits((int)Math.Round(v));
    int BoxWidth => Toggle ? S(40) : S(20);

    public override Size GetPreferredSize(Size proposedSize)
    {
        var t = Text == "" ? Size.Empty : TextRenderer.MeasureText(Text, Font, Size.Empty, TextFormatFlags.NoPrefix);
        int gap = Text == "" ? 0 : S(8);
        return new Size(BoxWidth + gap + t.Width + 2, Math.Max(S(28), t.Height + S(4)));
    }

    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? Theme.Card);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        if (Toggle) PaintToggle(g); else PaintCheck(g);

        if (Text != "")
        {
            int x = BoxWidth + S(8);
            TextRenderer.DrawText(g, Text, Font, new Rectangle(x, 0, Width - x, Height),
                Enabled ? Theme.Text : Theme.TextDisabled,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left |
                TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
        }
    }

    void PaintCheck(Graphics g)
    {
        float size = S(20);
        var r = new RectangleF(0.5f, (Height - size) / 2 + 0.5f, size - 1, size - 1);
        using var path = Theme.RoundRect(r, S(4));

        if (Checked)
        {
            using (var b = new SolidBrush(hover ? Theme.AccentHover : Theme.Accent)) g.FillPath(b, path);
            using var pen = new Pen(Theme.OnAccent, Math.Max(1.5f, S(1.6f))) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            g.DrawLines(pen, new[]
            {
                new PointF(r.X + r.Width * 0.27f, r.Y + r.Height * 0.52f),
                new PointF(r.X + r.Width * 0.43f, r.Y + r.Height * 0.68f),
                new PointF(r.X + r.Width * 0.74f, r.Y + r.Height * 0.34f),
            });
        }
        else
        {
            using (var b = new SolidBrush(hover ? Theme.FieldHover : Theme.Field)) g.FillPath(b, path);
            using var pen = new Pen(Theme.BorderBottom);
            g.DrawPath(pen, path);
        }
        if (Focused && ShowFocusCues) FocusRing(g, r);
    }

    void PaintToggle(Graphics g)
    {
        float w = S(40), h = S(20);
        var r = new RectangleF(0.5f, (Height - h) / 2 + 0.5f, w - 1, h - 1);
        using var path = Theme.RoundRect(r, h / 2);

        float knob = hover ? S(14) : S(12);
        float cy = r.Y + r.Height / 2;
        if (Checked)
        {
            using (var b = new SolidBrush(hover ? Theme.AccentHover : Theme.Accent)) g.FillPath(b, path);
            float cx = r.Right - S(10);
            using var kb = new SolidBrush(Theme.OnAccent);
            g.FillEllipse(kb, cx - knob / 2, cy - knob / 2, knob, knob);
        }
        else
        {
            using (var b = new SolidBrush(hover ? Theme.FieldHover : Theme.Field)) g.FillPath(b, path);
            using (var p = new Pen(Theme.BorderBottom)) g.DrawPath(p, path);
            float cx = r.X + S(10);
            using var kb = new SolidBrush(Theme.TextSecondary);
            g.FillEllipse(kb, cx - knob / 2, cy - knob / 2, knob, knob);
        }
        if (Focused && ShowFocusCues) FocusRing(g, r);
    }

    void FocusRing(Graphics g, RectangleF r)
    {
        r.Inflate(S(2) - 0.5f, S(2) - 0.5f);
        using var path = Theme.RoundRect(r, Toggle ? r.Height / 2 : S(5));
        using var pen = new Pen(Theme.Text, 1.5f);
        g.DrawPath(pen, path);
    }
}









class DarkField : Control
{
    public readonly TextBox Box = new()
    {
        BorderStyle = BorderStyle.None,
        BackColor = Theme.Field,
        ForeColor = Theme.Text,
    };

    readonly List<(string Value, string Text)> options = new();
    string pickValue = "";
    bool editable = true, hover;
    DarkMenu? openMenu;

    public DarkField(int width)
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                 ControlStyles.Selectable, true);
        Size = new Size(width, 32);
        TabStop = false;
        Controls.Add(Box);

        Box.Enter += (_, _) => Invalidate();
        Box.Leave += (_, _) => Invalidate();
        Box.MouseEnter += (_, _) => UpdateHover();
        Box.MouseLeave += (_, _) => UpdateHover();
        Box.KeyDown += (_, e) =>
        {
            if (options.Count > 0 && (e.KeyCode == Keys.F4 || (e.Alt && e.KeyCode == Keys.Down)))
            {
                ShowDropDown();
                e.Handled = e.SuppressKeyPress = true;
            }
        };
    }

    public bool Editable
    {
        get => editable;
        set { editable = value; Box.Visible = value; TabStop = !value; PerformLayout(); Invalidate(); }
    }

    public bool Password
    {
        get => Box.UseSystemPasswordChar;
        set => Box.UseSystemPasswordChar = value;
    }

    public string Placeholder
    {
        get => Box.PlaceholderText;
        set => Box.PlaceholderText = value;
    }

    public string Value
    {
        get => editable ? Box.Text.Trim() : pickValue;
        set { if (editable) Box.Text = value; else { pickValue = value; Invalidate(); } }
    }

    public DarkField Option(string value, string text)
    {
        options.Add((value, text));
        PerformLayout();
        Invalidate();
        return this;
    }

    bool HasDropDown => options.Count > 0;
    bool IsFocused => editable ? Box.Focused : Focused;
    int S(float v) => LogicalToDeviceUnits((int)Math.Round(v));

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        int left = S(11), right = HasDropDown ? S(34) : S(11);
        Box.SetBounds(left, (Height - Box.Height) / 2, Math.Max(10, Width - left - right), Box.Height);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? Theme.Card);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        bool focused = IsFocused || openMenu != null;
        var fill = focused ? Theme.FieldFocus : hover ? Theme.FieldHover : Theme.Field;
        if (Box.BackColor != fill) Box.BackColor = fill;

        using var path = Theme.RoundRect(new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f), S(4));
        using (var b = new SolidBrush(fill)) g.FillPath(b, path);
        using (var p = new Pen(Theme.Border)) g.DrawPath(p, path);


        var saved = g.Save();
        g.SetClip(path);
        int line = focused ? S(2) : 1;
        using (var b = new SolidBrush(focused ? Theme.Accent : Theme.BorderBottom))
            g.FillRectangle(b, 0, Height - line, Width, line);
        g.Restore(saved);

        if (!editable)
        {
            var text = options.FirstOrDefault(o => o.Value == pickValue).Text ?? pickValue;
            TextRenderer.DrawText(g, text, Font, new Rectangle(S(11), 0, Width - S(45), Height), Theme.Text,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.SingleLine |
                TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }

        if (HasDropDown)
        {
            float cx = Width - S(17), cy = Height / 2f;
            using var pen = new Pen(Theme.TextSecondary, Math.Max(1f, S(1.2f)))
            { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            g.DrawLines(pen, new[]
            {
                new PointF(cx - S(4), cy - S(2)),
                new PointF(cx, cy + S(2)),
                new PointF(cx + S(4), cy - S(2)),
            });
        }
    }

    void UpdateHover()
    {
        bool h = ClientRectangle.Contains(PointToClient(Cursor.Position));
        if (h != hover) { hover = h; Invalidate(); }
    }

    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); UpdateHover(); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); UpdateHover(); }
    protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); if (editable) Box.Focus(); Invalidate(); }
    protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        Cursor = !editable || (HasDropDown && e.X >= Width - S(34)) ? Cursors.Default : Cursors.IBeam;
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        if (!editable || (HasDropDown && e.X >= Width - S(34))) { Focus(); ShowDropDown(); }
        else Box.Focus();
    }

    protected override bool IsInputKey(Keys keyData) =>
        keyData is Keys.Down or Keys.Up || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!editable && e.KeyCode is Keys.Space or Keys.F4 or Keys.Down or Keys.Up)
        {
            ShowDropDown();
            e.Handled = true;
        }
    }

    void ShowDropDown()
    {
        if (!HasDropDown || openMenu != null) return;

        var menu = new DarkMenu { ShowImageMargin = false, ShowCheckMargin = false };
        int itemWidth = Width - menu.Padding.Horizontal;
        var current = Value;

        foreach (var (value, text) in options)
        {
            var v = value;
            var item = menu.AddItem(text, null, (_, _) =>
            {
                Value = v;
                if (editable) Box.Focus(); else Focus();
            });
            item.Padding = new Padding(S(10), S(6), S(10), S(6));
            item.Checked = string.Equals(v, current, StringComparison.OrdinalIgnoreCase);
            var pref = item.GetPreferredSize(Size.Empty);
            item.AutoSize = false;
            item.Size = new Size(Math.Max(itemWidth, pref.Width), pref.Height);
        }

        menu.Closed += (_, _) =>
        {
            openMenu = null;
            Invalidate();
            BeginInvoke(menu.Dispose);
        };
        openMenu = menu;
        Invalidate();
        menu.Show(this, new Point(0, Height + S(2)));
    }
}
