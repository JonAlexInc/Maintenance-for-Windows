using System.Drawing.Drawing2D;
namespace Maintenance.App;
sealed class BenchmarkCard:Control
{
    internal DiskWorkload Workload {get;set;}
    internal DiskMeasurement[] Samples=[];
    internal string Unit="MB/s";
    internal bool Active;
    internal bool Selectable,Chosen;
    internal Color SelectionAccent=Color.Teal;
    internal void ActivateSelection(){if(Selectable){Focus();OnClick(EventArgs.Empty);}}
    protected override void OnKeyDown(KeyEventArgs e){if(Selectable&&e.KeyCode is Keys.Space or Keys.Enter){ActivateSelection();e.Handled=true;e.SuppressKeyPress=true;}base.OnKeyDown(e);}
    protected override void OnGotFocus(EventArgs e){base.OnGotFocus(e);Invalidate();}
    protected override void OnLostFocus(EventArgs e){base.OnLostFocus(e);Invalidate();}
    internal BenchmarkCard(DiskWorkload workload){Workload=workload;DoubleBuffered=true;SetStyle(ControlStyles.ResizeRedraw,true);AccessibleRole=AccessibleRole.Grouping;AccessibleName=workload.Name;}
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
        bool dark=BackColor.GetBrightness()<0.5;Color face=dark?Color.FromArgb(33,42,53):Color.White;
        Color quiet=dark?Color.FromArgb(164,179,198):Color.FromArgb(89,106,126),text=dark?Color.FromArgb(239,244,251):Color.FromArgb(26,42,63);
        Color read=dark?Color.FromArgb(71,205,190):Color.FromArgb(0,116,108),write=dark?Color.FromArgb(148,171,255):Color.FromArgb(76,91,185);
        var rect=new Rectangle(1,1,Width-3,Height-3);if(rect.Width<10||rect.Height<10)return;
        using var shape=new GraphicsPath();int r=20;shape.AddArc(rect.Left,rect.Top,r,r,180,90);shape.AddArc(rect.Right-r,rect.Top,r,r,270,90);shape.AddArc(rect.Right-r,rect.Bottom-r,r,r,0,90);shape.AddArc(rect.Left,rect.Bottom-r,r,r,90,90);shape.CloseFigure();
        if(Chosen)face=Color.FromArgb((face.R*3+SelectionAccent.R)/4,(face.G*3+SelectionAccent.G)/4,(face.B*3+SelectionAccent.B)/4);
        using var fill=new SolidBrush(face);g.FillPath(fill,shape);using var line=new Pen(Chosen?SelectionAccent:Active?read:dark?Color.FromArgb(51,65,81):Color.FromArgb(222,229,239),Chosen?3:Active?2:1);g.DrawPath(line,shape);
        if(Focused&&Selectable)ControlPaint.DrawFocusRectangle(g,Rectangle.Inflate(rect,-6,-6),text,face);
        float scale=DeviceDpi/96f;int width=(int)(Width/scale),height=(int)(Height/scale);int pad=16;bool compact=height<230;int top=pad;
        void TextAt(string value,float size,FontStyle style,Color color,Rectangle bounds){using var font=new Font("Segoe UI",size,style);bounds=new Rectangle((int)(bounds.X*scale),(int)(bounds.Y*scale),(int)(bounds.Width*scale),(int)(bounds.Height*scale));TextRenderer.DrawText(g,value,font,bounds,color,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);}
        string title=Workload.Title??(Workload.Random?"Everyday responsiveness":"Large-file transfers");
        TextAt(title,12,FontStyle.Bold,text,new(pad,top,width-pad*2-(Selectable||Chosen?100:0),28));
        if(Selectable||Chosen)TextAt(Chosen?"Selected":"Select",9,FontStyle.Bold,text,new(width-105,top,90,28));top+=30;
        TextAt($"{Workload.Name}  ·  {(Workload.Queue==1?"Single request":"Multiple requests")}",9,FontStyle.Regular,quiet,new(pad,top,width-pad*2,24));
        int metricY=Math.Max(top+30,height/2-25),half=(width-pad*2)/2;
        string Description(bool isWrite){var samples=Samples.Where(s=>s.Write==isWrite).ToArray();return samples.Length==0?(Active?"Waiting for pass":"Ready to test"):$"{samples.Length} completed pass{(samples.Length==1?"":"es")} · mean";}
        for(int c=0;c<2;c++) {
            bool isWrite=c==1;var samples=Samples.Where(s=>s.Write==isWrite).ToArray();double Value(DiskScore s)=>Unit switch{"GB/s"=>s.MBps/1000,"IOPS"=>s.Iops,"µs"=>s.Microseconds,"P95 µs"=>s.P95Microseconds,"P99 µs"=>s.P99Microseconds,_=>s.MBps};
            int x=pad+c*half;var color=isWrite?write:read;
            TextAt((isWrite?"WRITE":"READ")+"  /  "+Unit,8,FontStyle.Bold,color,new(x,metricY,half-8,22));
            string number=samples.Length==0?"—":samples.Average(s=>Value(s.Score)).ToString("N2");
            TextAt(number,compact?21:28,FontStyle.Bold,text,new(x,metricY+24,half-8,compact?36:48));
            if(height>185)TextAt(Description(isWrite),8,FontStyle.Regular,quiet,new(x,metricY+(compact?61:76),half-8,22));
        }
        AccessibleDescription=$"{Workload.Name}. {Unit}. "+string.Join(". ",Samples.Select(s=>$"{(s.Write?"Write":"Read")} pass {s.Pass}: {s.Score.MBps:N2} MB/s"));
    }
}


