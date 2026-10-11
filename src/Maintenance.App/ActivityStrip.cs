using System.Drawing.Drawing2D;
namespace Maintenance.App;

sealed partial class MainWindow
{
    readonly ActivityStrip liveStatus=new(){Dock=DockStyle.Bottom,Height=52};
    readonly ToolTip activityTip=new(){AutoPopDelay=20000};
    readonly System.Windows.Forms.Timer statusTimer=new(){Interval=250};
    void StartStatusStrip(){liveStatus.Controls.Add(expandResults);liveStatus.Controls.Add(emergencyStop);liveStatus.Padding=new(0,5,12,5);expandResults.Click+=(_,_)=>PopOutResults();emergencyStop.Click+=(_,_)=>StopAllOperations();activityTip.SetToolTip(emergencyStop,"Stop all scans, tests and app-owned helpers. Partial results remain. A Windows operation already in progress may need time to settle.");statusTimer.Tick+=(_,_)=>UpdateStatusStrip();statusTimer.Start();FormClosed+=(_,_)=>{statusTimer.Dispose();activityTip.Dispose();};}
    void UpdateStatusStrip(){
        var tasks=new List<string>();
        if(busy)tasks.Add(string.IsNullOrWhiteSpace(status.Text)?"Scanning files…":status.Text);
        if(speedCancel!=null)tasks.Add("Speed test · "+speedState.Text);
        if(storageBusy)tasks.Add("Storage scan · "+string.Join(" · ",driveCategories.Select(p=>p.Key+" "+p.Value.Text.Split('\n')[0])));
        if(inventoryBusy)tasks.Add("Collecting system hardware…");
        if(startupWorking)tasks.Add(status.Text);
        if(restoreBusy)tasks.Add("Reviewing system restore points…");
        if(renderCancel!=null)tasks.Add("Preparing results…");
        if(AppOperations.Count>0&&tasks.Count==0)tasks.Add("Windows helper operation…");
        bool running=OperationsRunning;
        if(emergencyStopping&&!running){emergencyStopping=false;status.Text="Stopped · Partial results and completed outcomes remain available.";}
        emergencyStop.Enabled=running||usageObserver!=null;emergencyStop.Text=emergencyStopping?"■  Stopping…":"■  Stop all";
        expandResults.Enabled=ActiveResults!=null;
        liveStatus.StopRequested=emergencyStopping;liveStatus.ActionWidth=emergencyStop.Width+expandResults.Width+24;
        liveStatus.Running=running;liveStatus.Text=emergencyStopping?"Stopping all operations · Partial results retained. Waiting for pending I/O or Windows calls…":tasks.Count>0?string.Join("   |   ",tasks):status.Text;
        if(activityTip.GetToolTip(liveStatus)!=liveStatus.Text)activityTip.SetToolTip(liveStatus,liveStatus.Text);
        liveStatus.AccessibleDescription=liveStatus.Text;
        liveStatus.Invalidate();
        if(activity!=null&&activity.Visible)activity.Invalidate();
    }
    Control ModernTabs(HardwarePages tabs){var nav=new FlowLayoutPanel{Dock=DockStyle.Top,AutoSize=true,Padding=new(0,6,0,6)};var buttons=new List<Button>();
        for(int i=0;i<tabs.TabPages.Count;i++){int index=i;var b=Button(tabs.TabPages[i].Text,()=>tabs.SelectedIndex=index);b.AutoSize=true;b.Margin=new(0,0,8,6);buttons.Add(b);nav.Controls.Add(b);}
        void Style(){for(int i=0;i<buttons.Count;i++){buttons[i].Tag=i==tabs.SelectedIndex?PrimaryRole:null;StyleButton(buttons[i]);}}
        tabs.SelectedIndexChanged+=(_,_)=>Style();tabs.SelectedIndex=0;Style();return nav;
    }
}
sealed class ActivityPulse:Control
{
    public Color Accent{get;set;}=Color.Teal;
    public ActivityPulse(){DoubleBuffered=true;AccessibleName="Scan in progress";}
    protected override void OnPaint(PaintEventArgs e){int y=Height/2;ThemeShapes.Fill(e.Graphics,new Rectangle(0,y,Width,4),Color.FromArgb(BackColor.GetBrightness()<.5?55:30,Accent),2);int w=Math.Max(40,Width/6),x=(int)(Environment.TickCount64/8%(Math.Max(1,Width)+w))-w;var state=e.Graphics.Save();e.Graphics.SetClip(ClientRectangle);ThemeShapes.Fill(e.Graphics,new Rectangle(x,y,w,4),Accent,2);e.Graphics.Restore(state);}
}
sealed class ActivityStrip:Control
{
    public bool Running{get;set;}
    public bool StopRequested{get;set;}
    public int ActionWidth{get;set;}
    public Color Accent{get;set;}=Color.Teal;
    public ActivityStrip(){DoubleBuffered=true;SetStyle(ControlStyles.ResizeRedraw,true);AccessibleName="Application activity";}
    protected override void OnPaint(PaintEventArgs e){var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
        using var edge=new Pen(Color.FromArgb(80,Accent));g.DrawLine(edge,0,0,Width,0);
        int d=Math.Max(12,Height/4),y=(Height-d)/2;using var halo=new SolidBrush(Color.FromArgb(Running?55:20,Accent));g.FillEllipse(halo,14,y-5,d+10,d+10);
        using var dot=new SolidBrush(Accent);g.FillEllipse(dot,19,y,d,d);
        string label=StopRequested?"STOPPING":Running?"WORKING":"READY";using var font=new Font(Font.FontFamily,9,FontStyle.Bold);
        int offset=Math.Max(140,TextRenderer.MeasureText(label,font).Width+60);
        TextRenderer.DrawText(g,label,font,new Rectangle(44,0,offset-44,Height),ForeColor,TextFormatFlags.VerticalCenter);
        TextRenderer.DrawText(g,Text,Font,new Rectangle(offset,0,Math.Max(0,Width-offset-20-ActionWidth),Height),ForeColor,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);
        if(Running){int w=Math.Max(70,Width/12);int x=(int)(Environment.TickCount64/8%(Width+w))-w;using var glow=new SolidBrush(Accent);g.FillRectangle(glow,x,Height-3,w,3);}
    }
}
sealed class SystemDriveBadge:Control
{
    public Color Accent{get;set;}=Color.Teal;
    public SystemDriveBadge(){DoubleBuffered=true;SetStyle(ControlStyles.ResizeRedraw,true);}
    protected override void OnPaint(PaintEventArgs e){var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;int d=Math.Max(18,Math.Min(Height-4,34*DeviceDpi/96));int y=(Height-d)/2;
        using var halo=new SolidBrush(Color.FromArgb(45,Accent));g.FillEllipse(halo,0,y,d,d);using var brush=new SolidBrush(Accent);
        int s=d/5,x=d/2-s-1,t=y+d/2-s-1;foreach(int dx in new[]{0,s+2})foreach(int dy in new[]{0,s+2})g.FillRectangle(brush,x+dx,t+dy,s,s);
        TextRenderer.DrawText(g,"System drive",Font,new Rectangle(d+6,0,Math.Max(0,Width-d-6),Height),ForeColor,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
    }
}
