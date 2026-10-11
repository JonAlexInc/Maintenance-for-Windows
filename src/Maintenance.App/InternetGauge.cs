using System.Drawing.Drawing2D;
namespace Maintenance.App;
sealed class InternetGauge:Control
{
    public string Title="DOWNLOAD";
    public Color Accent=Color.Teal;
    public string State="Ready";
    public double Rate,Fraction;
    double displayed,scaleMaximum=1000,peak;
    readonly System.Windows.Forms.Timer animation=new(){Interval=30};
    readonly Queue<double> history=[];
    public InternetGauge(){DoubleBuffered=true;SetStyle(ControlStyles.ResizeRedraw,true);animation.Tick+=(_,_)=>{displayed+=(Rate-displayed)*.22;if(Math.Abs(displayed-Rate)<.1){displayed=Rate;animation.Stop();}Invalidate();};}
    protected override void Dispose(bool disposing){if(disposing)animation.Dispose();base.Dispose(disposing);}
    public void Reset(string state){animation.Stop();Rate=Fraction=displayed=peak=0;scaleMaximum=1000;history.Clear();State=state;Invalidate();}
    public void Update(InternetProgress progress){Rate=progress.Mbps;Fraction=(double)progress.Bytes/progress.Total;State=progress.Status??(progress.Complete?"Average":$"{progress.Bytes/1e6:N1} / {progress.Total/1e6:N0} MB · {(progress.Upload?"Sending":"Receiving")}");if(!progress.Complete){history.Enqueue(Rate);peak=Math.Max(peak,Rate);}while(history.Count>90)history.Dequeue();scaleMaximum=Math.Max(scaleMaximum,Math.Pow(10,Math.Ceiling(Math.Log10(Math.Max(1,Rate)))));animation.Start();Invalidate();}
    protected override void OnPaint(PaintEventArgs e){var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;bool dark=BackColor.GetBrightness()<.5;var face=dark?Color.FromArgb(32,41,53):Color.White;ThemeShapes.Fill(g,new Rectangle(1,1,Width-3,Height-3),face,18);
        float s=DeviceDpi/96f;float diameter=Math.Min(Width-64*s,210*s),x=(Width-diameter)/2,y=42*s;
        using var track=new Pen(dark?Color.FromArgb(58,72,89):Color.FromArgb(220,229,238),10*s){StartCap=LineCap.Round,EndCap=LineCap.Round};
        using var arc=new Pen(Accent,10*s){StartCap=LineCap.Round,EndCap=LineCap.Round};double max=Math.Max(100,Math.Pow(10,Math.Ceiling(Math.Log10(Math.Max(1,Rate)))));
        max=scaleMaximum;float sweep=(float)Math.Max(.1,240*Math.Min(1,displayed/max));
        using(var ambient=new LinearGradientBrush(new Rectangle(2,2,Width-4,Height-4),Color.FromArgb(dark?30:14,Accent),Color.Transparent,90f))g.FillRectangle(ambient,2,2,Width-4,Height-4);
        g.DrawArc(track,x,y,diameter,diameter,150,240);
        for(int i=0;i<=40;i++){double angle=(150+6*i)*Math.PI/180;float outer=diameter/2+16*s,inner=outer-(i%5==0?8:4)*s;var center=new PointF(x+diameter/2,y+diameter/2);using var tick=new Pen(i<=40*displayed/max?Accent:Color.FromArgb(dark?85:170,ForeColor),1*s);g.DrawLine(tick,center.X+(float)Math.Cos(angle)*inner,center.Y+(float)Math.Sin(angle)*inner,center.X+(float)Math.Cos(angle)*outer,center.Y+(float)Math.Sin(angle)*outer);}
        for(int glow=3;glow>0;glow--){using var halo=new Pen(Color.FromArgb(16,Accent),(10+glow*4)*s){StartCap=LineCap.Round,EndCap=LineCap.Round};g.DrawArc(halo,x,y,diameter,diameter,150,sweep);}
        g.DrawArc(arc,x,y,diameter,diameter,150,sweep);
        double tip=(150+sweep)*Math.PI/180;float tx=x+diameter/2+(float)Math.Cos(tip)*diameter/2,ty=y+diameter/2+(float)Math.Sin(tip)*diameter/2;using(var dot=new SolidBrush(dark?Color.White:Accent))g.FillEllipse(dot,tx-4*s,ty-4*s,8*s,8*s);
        void Text(string text,float size,int top,int height,Color color){using var font=new Font("Segoe UI",size,FontStyle.Bold);TextRenderer.DrawText(g,text,font,new Rectangle(12,(int)(top*s),Width-24,(int)(height*s)),color,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);}
        Text(Title,10,10,24,ForeColor);Text(Rate.ToString("N1"),30,100,48,ForeColor);Text("Mbps",10,146,24,Accent);Text($"Scale 0–{max:N0}  ·  Peak {peak:N1} Mbps",8,210,22,ForeColor);
        int graphY=(int)(238*s),graphH=(int)(28*s);var values=history.ToArray();using(var grid=new Pen(Color.FromArgb(35,ForeColor)))for(int row=0;row<3;row++)g.DrawLine(grid,22,graphY+row*graphH/2,Width-22,graphY+row*graphH/2);
        if(values.Length>1){double ceiling=Math.Max(1,values.Max());var pts=values.Select((v,i)=>new PointF(22+i*(Width-44f)/(values.Length-1),graphY+graphH-(float)(v/ceiling*graphH))).ToArray();using var area=new GraphicsPath();area.AddLines(pts);area.AddLine(pts[^1],new PointF(Width-22,graphY+graphH));area.AddLine(new PointF(Width-22,graphY+graphH),new PointF(22,graphY+graphH));area.CloseFigure();using var shade=new LinearGradientBrush(new Rectangle(22,graphY,Width-44,graphH),Color.FromArgb(95,Accent),Color.FromArgb(5,Accent),90f);g.FillPath(shade,area);using var pen=new Pen(Accent,2*s);g.DrawLines(pen,pts);}
        Text(State,9,272,26,ForeColor);var progress=new Rectangle(22,(int)(307*s),Math.Max(1,Width-44),(int)(5*s));ThemeShapes.Fill(g,progress,dark?Color.FromArgb(58,72,89):Color.Gainsboro,3);progress.Width=(int)(progress.Width*Math.Clamp(Fraction,0,1));ThemeShapes.Fill(g,progress,Accent,3);
    }
}

