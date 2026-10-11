namespace Maintenance.App;
sealed class AppearanceCheckBox:CheckBox
{
    public Color DisabledInk {get;set;}=Color.SlateGray;
    public Color Accent {get;set;}=Color.FromArgb(34,116,204);
    public override Size GetPreferredSize(Size proposedSize){var text=TextRenderer.MeasureText(Text,Font,Size.Empty,TextFormatFlags.SingleLine|TextFormatFlags.NoPrefix);return new(text.Width+32,Math.Max(text.Height+14,(int)(28*DeviceDpi/96f)));}
    public AppearanceCheckBox(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);}
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);Color ink=Enabled?(Checked?Color.White:ForeColor):DisabledInk;
        var bounds=new Rectangle(1,1,Math.Max(1,Width-3),Math.Max(1,Height-3));
        ThemeShapes.Fill(e.Graphics,bounds,Checked?Accent:BackColor,10);
        using(var border=new Pen(Checked?Accent:DisabledInk))using(var path=ThemeShapes.Rounded(bounds,10))e.Graphics.DrawPath(border,path);
        var text=Rectangle.Inflate(bounds,-12,-3);TextRenderer.DrawText(e.Graphics,Text,Font,text,ink,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPrefix|TextFormatFlags.WordBreak);
        if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(e.Graphics,text,ink,BackColor);
    }
}

