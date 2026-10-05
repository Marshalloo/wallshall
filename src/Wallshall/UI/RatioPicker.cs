/// Выбор пропорций: несколько сразу, плюс «Все широкие» и «Все вертикальные».
/// Правила выбора живут в RatioSelection, здесь только отрисовка.
class RatioPicker : TableLayoutPanel
{
    static readonly (string Caption, string[] Values)[] Groups =
    {
        ("Широкие", new[] { "16x9", "16x10" }),
        ("Сверхширокие", new[] { "21x9", "32x9", "48x9" }),
        ("Вертикальные", new[] { "9x16", "10x16", "9x18" }),
        ("Квадратные", new[] { "1x1", "3x2", "4x3", "5x4" }),
    };

    readonly RatioSelection selection = new();
    readonly ToolTip tips = new();
    readonly DarkButton any;
    readonly List<DarkButton> chips = new();
    readonly FlowLayoutPanel extraRow;

    public RatioPicker()
    {
        ColumnCount = 2;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Margin = Padding.Empty;
        ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var modes = Row("");
        any = Chip("Любые", 70);
        any.Click += (_, _) => { selection.Clear(); Sync(); };
        modes.Controls.Add(any);
        modes.Controls.Add(Mode("Все широкие", RatioSelection.AllWide, "Любые горизонтальные пропорции"));
        modes.Controls.Add(Mode("Все вертикальные", RatioSelection.AllPortrait, "Любые вертикальные пропорции"));

        foreach (var (caption, values) in Groups)
        {
            var row = Row(caption);
            foreach (var value in values) row.Controls.Add(Exact(value, value.Replace("x", "×")));
        }

        extraRow = Row("Другие");
        ShowRow(extraRow, false);

        Sync();
    }

    public string Value
    {
        get => selection.Value;
        set
        {
            selection.Value = value;

            foreach (var token in RatioSelection.Parse(selection.Value))
                if (!chips.Any(chip => ValueOf(chip) == token))
                {
                    extraRow.Controls.Add(Exact(token, token));
                    ShowRow(extraRow, true);
                }

            Sync();
        }
    }

    void Sync()
    {
        foreach (var chip in chips) chip.Chosen = selection.Has(ValueOf(chip));
        any.Chosen = selection.Empty;
    }

    static string ValueOf(DarkButton chip) => (string)chip.Tag!;

    FlowLayoutPanel Row(string caption)
    {
        var label = new Label
        {
            Text = caption,
            AutoSize = true,
            ForeColor = Theme.TextSecondary,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 4, 12, 6),
        };
        Controls.Add(label);

        var row = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = false,
            Margin = Padding.Empty,
            Tag = label,
        };
        Controls.Add(row);
        return row;
    }

    static void ShowRow(FlowLayoutPanel row, bool visible)
    {
        row.Visible = visible;
        ((Control)row.Tag!).Visible = visible;
    }

    static DarkButton Chip(string text, int minWidth) => new DarkButton { Text = text }.AsChip(minWidth);

    DarkButton Mode(string text, string value, string hint)
    {
        var chip = Track(Chip(text, 110), value);
        tips.SetToolTip(chip, hint);
        return chip;
    }

    DarkButton Exact(string value, string text) => Track(Chip(text, 56), value);

    DarkButton Track(DarkButton chip, string value)
    {
        chip.Tag = value;
        chip.Click += (_, _) => { selection.Toggle(value); Sync(); };
        chips.Add(chip);
        return chip;
    }
}
