namespace Maintenance.App;

// Retain native keyboard and accessibility behavior with readable disabled colors.
class AppearanceButton : Button
{
    public Color DisabledInk { get; set; } = Color.Gray;
    public Color DisabledSurface { get; set; } = Color.White;
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Parent?.BackColor??BackColor);
        var shape=new Rectangle(0,0,Math.Max(1,Width-1),Math.Max(1,Height-1));
        ThemeShapes.Fill(e.Graphics,shape,Enabled?BackColor:DisabledSurface,8);
        if (FlatAppearance.BorderSize > 0)
        {
            using var pen = new Pen(FlatAppearance.BorderColor);
            using var path=ThemeShapes.Rounded(shape,8);e.Graphics.DrawPath(pen,path);
        }
        var area = ClientRectangle; area.Inflate(-6, -2);
        var alignment = TextAlign == ContentAlignment.MiddleLeft ? TextFormatFlags.Left : TextFormatFlags.HorizontalCenter;
        if (TextAlign == ContentAlignment.MiddleLeft) area.X += Padding.Left;
        TextRenderer.DrawText(e.Graphics, Text, Font, area, Enabled ? ForeColor : DisabledInk,
            alignment | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, Rectangle.Inflate(ClientRectangle, -4, -4), ForeColor, BackColor);
    }
}

sealed class AppearanceChoice : AppearanceButton
{
    public Color SelectionAccent {get;set;}=Color.FromArgb(34,116,204);
    internal ContextMenuStrip? ActiveMenu { get; private set; }
    public List<string> Items { get; } = [];
    public Dictionary<string,string> SectionStarts { get; } = new();
    public event EventHandler? SelectedIndexChanged;
    public string SelectedItem
    {
        get => Text;
        set { if (Text == value) return; Text = value; SelectedIndexChanged?.Invoke(this, EventArgs.Empty); }
    }
    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        var menu = new ContextMenuStrip { BackColor = BackColor, ForeColor = ForeColor, Font=Font, ShowImageMargin = false, ShowCheckMargin=false, Padding=new(6) };
        menu.Renderer=new AccentMenuRenderer(SelectionAccent,BackColor,ForeColor);
        ActiveMenu = menu;
        foreach (var item in Items)
        {
            if(SectionStarts.TryGetValue(item,out var section)) {
                if(menu.Items.Count>0)menu.Items.Add(new ToolStripSeparator());
                menu.Items.Add(new ToolStripLabel(section.ToUpperInvariant().Replace("&","&&")){ForeColor=ForeColor,Padding=new(12,8,12,3),Font=new Font(Font.FontFamily,Math.Max(8,Font.Size-1),FontStyle.Bold)});
            }
            var option = new ToolStripMenuItem(item.Replace("&","&&")) { Checked = item == SelectedItem,Padding=new(12,5,18,5) };
            option.Click += (_, _) => SelectedItem = item;
            menu.Items.Add(option);
        }
        // Defer disposal until the native menu has finished dispatching its click.
        menu.Closed += (_, _) => { if (IsHandleCreated && !IsDisposed) BeginInvoke(new Action(menu.Dispose)); };
        menu.Show(this, new Point(0, Height));
    }
}
