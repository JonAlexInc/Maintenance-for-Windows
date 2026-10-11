namespace Maintenance.App;
sealed class AccentMenuRenderer(Color accent,Color background,Color foreground):ToolStripProfessionalRenderer
{
    static bool Active(ToolStripItem item)=>item.Selected||item is ToolStripMenuItem{Checked:true};
    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e){using var brush=new SolidBrush(background);e.Graphics.FillRectangle(brush,new Rectangle(Point.Empty,e.Item.Size));ThemeShapes.Fill(e.Graphics,Rectangle.Inflate(new Rectangle(Point.Empty,e.Item.Size),-3,-2),Active(e.Item)?accent:background,8);}
    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e){e.TextColor=Active(e.Item)?Color.White:foreground;base.OnRenderItemText(e);}
    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e){}
}
static class ThemeShapes
{
    internal static System.Drawing.Drawing2D.GraphicsPath Rounded(Rectangle r,int radius){float d=Math.Min(Math.Min(r.Width,r.Height),radius*2);var p=new System.Drawing.Drawing2D.GraphicsPath();p.AddArc(r.Left,r.Top,d,d,180,90);p.AddArc(r.Right-d,r.Top,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.Left,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;}
    internal static void Fill(Graphics g,Rectangle r,Color color,int radius){if(r.Width<1||r.Height<1)return;g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;using var path=Rounded(r,radius);using var brush=new SolidBrush(color);g.FillPath(brush,path);}
}
sealed partial class MainWindow
{
    void ThemeListBox(ListBox box)
    {
        if(box.DrawMode==DrawMode.Normal){box.DrawMode=DrawMode.OwnerDrawFixed;box.DrawItem+=(_,e)=>{
            if(e.Index<0)return;bool selected=(e.State&DrawItemState.Selected)!=0;using var brush=new SolidBrush(selected?accent:surface);e.Graphics.FillRectangle(brush,e.Bounds);
            TextRenderer.DrawText(e.Graphics,box.GetItemText(box.Items[e.Index]),box.Font,e.Bounds,selected?Color.White:ink,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPrefix);e.DrawFocusRectangle();
        };}box.ItemHeight=box.Font.Height+6;box.Invalidate();
    }
    void DrawThemedListRow(object? sender,DrawListViewSubItemEventArgs e)
    {
        if(sender is not ListView list||e.Item==null||e.SubItem==null)return;
        bool selected=e.Item.Selected&&(!list.HideSelection||list.Focused);Color back=selected?accent:list.BackColor,color=selected?Color.White:e.Item.ForeColor;
        using(var brush=new SolidBrush(back))e.Graphics.FillRectangle(brush,e.Bounds);
        var area=e.Bounds;area.Inflate(-4,0);
        if(e.ColumnIndex==0&&list.CheckBoxes){int side=(int)(16*list.DeviceDpi/96f);var box=new Rectangle(area.Left,area.Top+(area.Height-side)/2,side,side);
            using(var fill=new SolidBrush(e.Item.Checked?accent:list.BackColor))e.Graphics.FillRectangle(fill,box);using(var edge=new Pen(selected?Color.White:muted))e.Graphics.DrawRectangle(edge,box);
            if(e.Item.Checked){using var pen=new Pen(Color.White,2);e.Graphics.DrawLines(pen,new Point[]{new(box.Left+3,box.Top+side/2),new(box.Left+side/2-1,box.Bottom-4),new(box.Right-3,box.Top+4)});}
            area.X+=side+7;area.Width-=side+7;
        }
        TextRenderer.DrawText(e.Graphics,e.SubItem.Text,list.Font,area,color,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);
    }
}
