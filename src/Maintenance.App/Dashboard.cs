using Maintenance.Core;
using System.Data;
using System.Runtime.InteropServices;

namespace Maintenance.App;

sealed partial class MainWindow
{
    readonly Label cpuValue = new();
    readonly System.Windows.Forms.Timer liveTimer = new() { Interval = 2000 };
    readonly CancellationTokenSource dashboardCancel = new();
    long lastIdle, lastKernel, lastUser;
    readonly FlowLayoutPanel drivesPanel = new() { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
    readonly Dictionary<string, StorageMeasure> storageCoverage = new();
    readonly Dictionary<string, Label> driveCategories = new();
    readonly Dictionary<string, CategoryBar> categoryBars = new();
    readonly Dictionary<string, UsageBar> usageBars = new();
    readonly DataGridView systemGrid = HistoryGrid();
    readonly TextBox systemSearch = new() { Width = 350, PlaceholderText = "Search components, properties, values" };
    readonly AppearanceChoice systemCategory = new() { Width=230, Height=42, Text="All categories" };
    readonly Label systemState = new() { Dock = DockStyle.Top, Height = 45, Text = "Collecting Windows inventory…" };
    List<SystemFact> systemFacts = [];
    bool inventoryBusy, storageBusy;
    CancellationTokenSource? storageRun;

    void BuildDashboard()
    {
        var body = Body(Page("Your computer", "Live activity, storage capacity, and a closer look at your hardware.")); body.AutoScroll = true;
        var layout = new Panel { Dock = DockStyle.Top, Height = 850 };
        var stats = new TableLayoutPanel { Dock = DockStyle.Top, Height = 125, ColumnCount = 3 };
        for (int i=0;i<3;i++) stats.ColumnStyles.Add(new(SizeType.Percent, 33.333f));
        AddStat(stats, 0, "OPERATING SYSTEM", osValue); AddStat(stats, 1, "CPU · LIVE", cpuValue); AddStat(stats, 2, "MEMORY · USED / TOTAL", ramValue);
        var title = TextLabel("Storage volumes", 14, true); title.Dock = DockStyle.Top; title.Height = 42;
        var actions = Bar(64); actions.Dock = DockStyle.Top;
        actions.Controls.Add(Button("Refresh storage", async () => { PopulateDrives(); await AnalyzeStorage(); }));
        actions.Controls.Add(Button("Stop storage scan",()=>storageRun?.Cancel()));
        actions.Controls.Add(Primary("Scan temporary files", async () => { scanLocation.SelectedItem=TempMode; SelectPage(1); await Scan(); }));
        var note = TextLabel("Local drives only · Estimated categories use launcher manifests, known folder layouts and file types. Classification does not mean safe to delete.", 8); note.Dock = DockStyle.Top; note.Height = 52;
        latestActivity.Dock = DockStyle.Top; latestActivity.Height = 48;
        layout.Controls.Add(latestActivity); layout.Controls.Add(drivesPanel); layout.Controls.Add(note); layout.Controls.Add(actions); layout.Controls.Add(title); layout.Controls.Add(stats);
        body.Controls.Add(layout);
        body.Resize += (_, _) => { drivesPanel.Width = body.ClientSize.Width; foreach (Control c in drivesPanel.Controls) c.Width = Math.Max(500, body.ClientSize.Width - 28); };
        drivesPanel.SizeChanged += (_, _) => layout.Height = 125 + 42 + 64 + 52 + drivesPanel.Height + 60;
        Shown += async (_, _) => { PopulateDrives(); UpdateLiveMetrics(); if (!smoke) { liveTimer.Start(); await Task.WhenAll(RefreshSystem(), AnalyzeStorage()); } };
        liveTimer.Tick += (_, _) => UpdateLiveMetrics();
        FormClosed += (_, _) => { liveTimer.Dispose(); dashboardCancel.Cancel(); dashboardCancel.Dispose(); };
    }
    void UpdateLiveMetrics()
    {
        var memory = new SystemProbe.Memory { Length = (uint)Marshal.SizeOf<SystemProbe.Memory>() };
        if (SystemProbe.GlobalMemoryStatusEx(ref memory)) ramValue.Text = $"{FormatBytes((long)(memory.Total-memory.Available))} / {FormatBytes((long)memory.Total)} · {memory.Load}%";
        else ramValue.Text = "Unavailable";
        ramValue.Font = new("Segoe UI", 11, FontStyle.Bold);
        if (SystemProbe.GetSystemTimes(out var idle, out var kernel, out var user))
        {
            var total = kernel - lastKernel + user - lastUser;
            cpuValue.Text = lastKernel == 0 || total <= 0 ? "Sampling…" : $"{Math.Clamp(100.0 * (total - (idle-lastIdle))/total, 0, 100):N1}%";
            lastIdle=idle; lastKernel=kernel; lastUser=user;
        }
        else cpuValue.Text = "Unavailable";
    }
    void PopulateDrives()
    {
        if (storageBusy) return;
        foreach (Control c in drivesPanel.Controls.Cast<Control>().ToArray()) { drivesPanel.Controls.Remove(c); c.Dispose(); }
        storageCoverage.Clear(); driveCategories.Clear(); categoryBars.Clear(); usageBars.Clear(); RefreshScanLocations();
        foreach (var drive in DriveInfo.GetDrives().Where(d => IsLocalStorage(d.DriveType)))
        {
            var card = new Panel { Width = Math.Max(600, content.ClientSize.Width-65), Height = 425, Padding = new(16), Margin = new(0,0,0,12), Tag = CardRole };
            var heading = TextLabel(drive.Name, 12, true); heading.Dock = DockStyle.Fill;
            var driveHeader=new Panel{Dock=DockStyle.Top,Height=40};driveHeader.Controls.Add(heading);
            if(string.Equals(drive.Name,Path.GetPathRoot(Environment.SystemDirectory),StringComparison.OrdinalIgnoreCase)){
                var badge=new SystemDriveBadge{Dock=DockStyle.Right,Width=TextRenderer.MeasureText("System drive",Font).Width+64,AccessibleName="Windows system drive"};driveHeader.Controls.Add(badge);
            }
            var detail = TextLabel("Not ready / unavailable", 9); detail.Dock = DockStyle.Top; detail.Height=32;
            var bar = new UsageBar { Dock = DockStyle.Top, Height=68, AccessibleName=drive.Name+" capacity", Available=false };
            usageBars[drive.Name]=bar;
            var category = TextLabel("Waiting for local analysis…", 8); category.Dock = DockStyle.Fill; category.Padding = new(0,8,0,0);
            try
            {
                if (drive.IsReady)
                {
                    var used = drive.TotalSize-drive.TotalFreeSpace;
                    heading.Text = $"{drive.Name}  {drive.VolumeLabel} · {drive.DriveType} · {drive.DriveFormat}";
                    detail.Text = $"{FormatBytes(used)} used  /  {FormatBytes(drive.TotalSize)} total     ·     {FormatBytes(drive.TotalFreeSpace)} free";
                    bar.Fraction = drive.TotalSize == 0 ? 0 : (double)used/drive.TotalSize;
                    bar.Available=drive.TotalSize>0;
                    bar.UsedBytes=used;
                    bar.AccessibleDescription=$"{bar.Fraction:P1} used; {FormatBytes(drive.TotalFreeSpace)} free of {FormatBytes(drive.TotalSize)}.";
                    if (IsLocalStorage(drive.DriveType)) driveCategories[drive.Name] = category;
                    else category.Text = "Capacity shown; category analysis is limited to fixed local volumes.";
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { detail.Text = "Volume unavailable"; }
            var categoryBar = new CategoryBar { Dock = DockStyle.Bottom, Height = 170 }; categoryBars[drive.Name] = categoryBar;
            var coverage = new LinkLabel {Text="Scan coverage ›",Dock=DockStyle.Bottom,Height=26,Font=new("Segoe UI",8),LinkBehavior=LinkBehavior.HoverUnderline,AccessibleName=drive.Name+" scan coverage details"};coverage.LinkClicked+=(_,_)=>ShowStorageCoverage(drive.Name);
            categoryBar.SizeChanged += (_, _) => { int columns = Math.Clamp(categoryBar.Width / 210, 2, 6); int height = ((StorageKinds.Labels.Length + columns - 1) / columns) * 54 + 12; if (categoryBar.Height != height) categoryBar.Height = height; card.Height = 32 + 40 + 32 + 68 + 94 + 26 + height; };
            card.Controls.Add(category); card.Controls.Add(coverage); card.Controls.Add(categoryBar); card.Controls.Add(bar); card.Controls.Add(detail); card.Controls.Add(driveHeader); drivesPanel.Controls.Add(card); PaintTheme(card, canvas);
        }
    }
    void ShowStorageCoverage(string root)
    {
        if (!storageCoverage.TryGetValue(root, out var result)) { MessageBox.Show(this, "Run the storage analysis first. Coverage details appear as the scan progresses.", "Storage coverage"); return; }
        var summary = $"{root} · {result.Entries:N0} entries examined · {result.Skipped:N0} omitted\r\n\r\n" + string.Join("\r\n", result.Reasons.Select(r => $"{r.Key}: {r.Value:N0}")) + "\r\n\r\nA skipped folder counts once, even if it contains many files. These are measurement omissions, not recommendations to delete.\r\nUnclassified used space can include protected files, shadow copies, filesystem metadata and allocation differences.\r\n\r\nExamples (up to 200):\r\n" + string.Join("\r\n", result.Examples.Select(f => f.Reason + " — " + f.Path));
        using var dialog = new Form { Text = "Storage scan coverage", Width = 1080, Height = 680, StartPosition = FormStartPosition.CenterParent, Padding = new(20) };
        dialog.Controls.Add(new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false, Text = summary }); PaintTheme(dialog, canvas); dialog.ShowDialog(this);
    }
    internal static bool LocalDriveType(DriveType type) => IsLocalStorage(type);
    static bool IsLocalStorage(DriveType type) => type is DriveType.Fixed or DriveType.Removable;
    async Task AnalyzeStorage()
    {
        if (storageBusy||emergencyStopping) return; storageBusy=true;
        using var run=CancellationTokenSource.CreateLinkedTokenSource(dashboardCancel.Token);storageRun=run;
        var storageToken=run.Token;
        try
        {
            await Task.WhenAll(driveCategories.Select(async entry =>
            {
                var (root,label)=entry;
                label.Text="Analyzing local files… All local drives run concurrently.";
                try
                {
                    long used=new DriveInfo(root).TotalSize-new DriveInfo(root).TotalFreeSpace;
                    void Display(StorageMeasure usage,bool running) {
                        if(IsDisposed)return;
                        storageCoverage[root] = usage;
                        categoryBars[root].Values=usage.Values;categoryBars[root].UsedBytes=used;categoryBars[root].Invalidate();
                        usageBars[root].Values=usage.Values;usageBars[root].UsedBytes=used;usageBars[root].Invalidate();
                        long measured=usage.Values.Sum();
                        label.Text=$"{(running?"Measuring — totals still growing":usage.Partial?"Complete with omissions":"Measurement complete")} · {usage.Entries:N0} entries · {usage.Skipped:N0} omitted — open Scan coverage for reasons.\n{FormatBytes(measured)} logical data measured · {FormatBytes(Math.Max(0,used-measured))} used space not classified. Includes inaccessible data and filesystem overhead; logical sizes can differ from disk usage.";
                    }
                    var progress=new Progress<StorageMeasure>(p=>Display(p,true));
                    var usage=await Task.Run(()=>StorageMeasure.Read(root,storageToken,progress));
                    if(IsDisposed)return;
                    Display(usage,false);
                    using(var journal=store.StartJournal())Maintenance.Core.LocalStore.Append(journal,new{Kind="Storage analysis",Location=root,Summary=$"Scan storage: {usage.Entries:N0} entries; {usage.Skipped:N0} skipped; {FormatBytes(usage.Values.Sum())} logical data measured; incomplete={usage.Partial}."});
                    RefreshHistory();
                }
                catch(OperationCanceledException) { if(!IsDisposed)label.Text="Stopped — incomplete totals shown. Refresh storage to start again.\nGray space has not been explained by the measured categories."; }
                catch(Exception e) when(e is IOException or UnauthorizedAccessException) { if(!IsDisposed)label.Text="This local volume is currently unavailable."; }
            }).ToArray());
        }
        finally {storageBusy=false;storageRun=null;}
    }
    Button? systemStop;CancellationTokenSource? systemSearchCancel;
    async Task RefreshSystem()
    {
        if(inventoryBusy||emergencyStopping) return; inventoryBusy=true;systemSearchCancel=new();if(systemStop!=null)systemStop.Enabled=true; systemState.Text="Collecting hardware, firmware, drivers, and Windows configuration…";
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(dashboardCancel.Token,systemSearchCancel.Token); timeout.CancelAfter(TimeSpan.FromSeconds(100));
        try { systemFacts=await SystemProbe.Read(timeout.Token); if (!IsDisposed) { systemCategory.Items.Clear(); systemCategory.Items.Add("All categories"); systemCategory.Items.AddRange(systemFacts.Select(f=>f.Section).Distinct().Order()); RenderSystem(); systemState.Text=$"Updated {DateTime.Now:t} · Choose a hardware tab. Export includes the complete inventory."; } }
        catch(Exception e) { if(!IsDisposed) systemState.Text=e is OperationCanceledException ? "Search stopped · Previously loaded hardware remains visible. Windows returns this inventory as a single response." : "Inventory unavailable: "+e.Message; }
        finally { inventoryBusy=false;systemSearchCancel?.Dispose();systemSearchCancel=null;if(systemStop!=null)systemStop.Enabled=false; }
    }
}
sealed class UsageBar : Control
{
    public double Fraction {get;set;}
    public long UsedBytes {get;set;}
    public long[] Values {get;set;}=[];
    public bool Available {get;set;}=true;
    public UsageBar() { DoubleBuffered=true;SetStyle(ControlStyles.ResizeRedraw,true); }
    protected override void OnPaint(PaintEventArgs e)
    {
        if(Width<30||Height<44)return;
        var g=e.Graphics;g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        double value=double.IsFinite(Fraction)?Math.Clamp(Fraction,0,1):0;
        bool dark=BackColor.GetBrightness()<.5;
        Color muted=dark?Color.FromArgb(169,189,210):Color.FromArgb(78,99,120);
        string Percent(double f)=>f>0&&f<.001?(f*100).ToString("0.00"):(f*100).ToString("0.0");
        using var labelFont=new Font(Font.FontFamily,11,FontStyle.Bold);
        TextRenderer.DrawText(g,Available?$"{Percent(value)}% USED":"CAPACITY UNAVAILABLE",labelFont,new Rectangle(0,0,Width,28),ForeColor,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPadding);
        TextRenderer.DrawText(g,Available?$"{Percent(1-value)}% FREE":"",Font,new Rectangle(0,0,Width,28),muted,TextFormatFlags.Right|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPadding);
        var track=new RectangleF(2,34,Width-4,23);
        using var outline=Rounded(track,11);
        using var empty=new SolidBrush(dark?Color.FromArgb(49,62,77):Color.FromArgb(221,230,239));g.FillPath(empty,outline);
        var state=g.Save();g.SetClip(outline);
        using(var hatch=new Pen(dark?Color.FromArgb(71,88,106):Color.FromArgb(191,205,218),1))for(float x=track.Left-24;x<track.Right;x+=12)g.DrawLine(hatch,x,track.Bottom,x+23,track.Top);
        float filled=(float)(track.Width*value);
        if(Available&&filled>0){
            using var unknown=new SolidBrush(dark?Color.FromArgb(87,97,109):Color.FromArgb(151,163,176));
            g.FillRectangle(unknown,track.Left,track.Top,filled,track.Height);
            double scale=Math.Max(UsedBytes,Values.Sum());float position=track.Left;
            for(int i=0;i<Math.Min(Values.Length,StorageKinds.Colors.Length);i++){
                float width=scale<=0?0:(float)(filled*Values[i]/scale);
                using var brush=new SolidBrush(StorageKinds.Colors[i]);g.FillRectangle(brush,position,track.Top,width,track.Height);position+=width;
            }
        }
        using(var tick=new Pen(Color.FromArgb(dark?65:95,BackColor),2))for(int i=1;i<20;i++){float x=track.Left+track.Width*i/20;g.DrawLine(tick,x,track.Top,x,track.Bottom);}
        g.Restore(state);
        using(var edge=new Pen(dark?Color.FromArgb(92,111,134):Color.FromArgb(165,184,204)))g.DrawPath(edge,outline);
        if(Available){float x=Math.Clamp(track.Left+filled,track.Left+4,track.Right-4);using var shadow=new Pen(Color.FromArgb(110,0,0,0),6);using var marker=new Pen(dark?Color.White:Color.FromArgb(28,49,74),3);g.DrawLine(shadow,x,track.Top-3,x,track.Bottom+3);g.DrawLine(marker,x,track.Top-3,x,track.Bottom+3);}
    }
    static System.Drawing.Drawing2D.GraphicsPath Rounded(RectangleF r,float radius){var p=new System.Drawing.Drawing2D.GraphicsPath();float d=radius*2;p.AddArc(r.Left,r.Top,d,d,180,90);p.AddArc(r.Right-d,r.Top,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.Left,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;}
}
sealed record StorageMeasure(long[] Values,int Entries,int Skipped,bool Partial)
{
    public IReadOnlyDictionary<string,int> Reasons { get; init; } = new Dictionary<string,int>();
    public IReadOnlyList<Finding> Examples { get; init; } = [];
    public long Other=>Values[5];
    public static int Category(string root,string path,IReadOnlyList<string>? games=null,bool development=false)
    {
        var relative=Path.GetRelativePath(root,path).Replace('/','\\').TrimStart('\\');
        if(relative.Contains("\\Temp\\",StringComparison.OrdinalIgnoreCase)||relative.StartsWith("Temp\\",StringComparison.OrdinalIgnoreCase))return 4;
        if(relative.StartsWith("Windows\\",StringComparison.OrdinalIgnoreCase)||new[]{"pagefile.sys","hiberfil.sys","swapfile.sys"}.Contains(relative,StringComparer.OrdinalIgnoreCase))return 0;
        int special=StorageKinds.Special(path,games);if(special>=0)return special;
        if(relative.StartsWith("Program Files\\",StringComparison.OrdinalIgnoreCase)||relative.StartsWith("Program Files (x86)\\",StringComparison.OrdinalIgnoreCase))return 1;
        if(relative.Contains("\\AppData\\",StringComparison.OrdinalIgnoreCase)||relative.StartsWith("ProgramData\\",StringComparison.OrdinalIgnoreCase))return 2;
        if(development)return 9;
        int personal=StorageKinds.Personal(path);if(personal>=0)return personal;
        if(relative.StartsWith("Users\\",StringComparison.OrdinalIgnoreCase)||new[]{".jpg",".jpeg",".png",".heic",".mp4",".mkv",".mp3",".wav",".pdf",".docx",".xlsx",".pptx"}.Contains(Path.GetExtension(path),StringComparer.OrdinalIgnoreCase))return 3;
        return 5;
    }
    public static StorageMeasure Read(string root,CancellationToken token,IProgress<StorageMeasure>? progress=null)
    {
        var reasons = new Dictionary<string,int>(); var examples = new List<Finding>();
        var gameRoots=StorageKinds.GameRoots();var values=new long[StorageKinds.Labels.Length]; int entries=0,skipped=0;bool partial=false;
        var watch=System.Diagnostics.Stopwatch.StartNew();long lastProgress=0;var dirs=new Queue<(string Path,bool Development)>();dirs.Enqueue((root,false));
        void Skip(string path, string reason) { skipped++; reasons[reason] = reasons.GetValueOrDefault(reason) + 1; if (examples.Count < 200) examples.Add(new(path, reason)); }
        StorageMeasure Snapshot(bool running) => new((long[])values.Clone(),entries,skipped,running || skipped > 0) { Reasons = new Dictionary<string,int>(reasons), Examples = examples.ToArray() };
        while(dirs.Count>0)
        {
            token.ThrowIfCancellationRequested();
            var (dir,inheritedDevelopment)=dirs.Dequeue();
            try
            {
                // Recheck directory attributes before enumeration; never follow mount points or network redirects.
                var dirAttr = File.GetAttributes(dir);
                if (NativeFile.NeedsRecall(dirAttr)) { Skip(dir,"Cloud-only / offline"); continue; }
                if((dirAttr&FileAttributes.ReparsePoint)!=0){Skip(dir,"Folder link or cloud-managed folder (not followed)");continue;}
                var children=new DirectoryInfo(dir).EnumerateFileSystemInfos().ToArray();
                bool development=inheritedDevelopment||StorageKinds.ProjectDirectory(dir,children.Select(c=>c.Name));
                foreach(var info in children)
                {
                    token.ThrowIfCancellationRequested();
                    entries++;
                    if(watch.ElapsedMilliseconds-lastProgress>=1000){progress?.Report(Snapshot(true));lastProgress=watch.ElapsedMilliseconds;}
                    try
                    {
                        string path=info.FullName;var attr=info.Attributes;
                        if (NativeFile.NeedsRecall(attr)) { Skip(path,"Cloud-only / offline"); continue; }
                        if((attr&FileAttributes.ReparsePoint)!=0){Skip(path,"Link or cloud-managed entry (not followed)");continue;}
                        if((attr&FileAttributes.Directory)!=0){dirs.Enqueue((path,development));continue;}
                        values[Category(root,path,gameRoots,development)]+=((FileInfo)info).Length;
                    }
                    catch(Exception e) when(e is IOException or UnauthorizedAccessException){Skip(info.FullName,e is UnauthorizedAccessException?"Access denied":"Unavailable / changed during scan");}
                }
            }
            catch(Exception e) when(e is IOException or UnauthorizedAccessException){Skip(dir,e is UnauthorizedAccessException?"Access denied":"Unavailable / changed during scan");}
            if(partial)break;
        }
        return Snapshot(false);
    }
}
sealed class CategoryBar : Control
{
    public long[] Values {get;set;}=new long[StorageKinds.Labels.Length];
    public long UsedBytes {get;set;}
    public CategoryBar(){DoubleBuffered=true;}
    protected override void OnPaint(PaintEventArgs e)
    {
        double total=Math.Max(UsedBytes,Values.Sum());
        var colors=StorageKinds.Colors;var labels=StorageKinds.Labels;
        for(int i=0;i<labels.Length;i++)
        {
            using var brush=new SolidBrush(colors[i]);
            int columns = Math.Clamp(Width / 210, 2, 6);
            var area=new Rectangle((i%columns)*Width/columns,8+(i/columns)*54,Width/columns,24);
            TextRenderer.DrawText(e.Graphics,labels[i],Font,area,colors[i],TextFormatFlags.Left|TextFormatFlags.EndEllipsis);
            area.Y+=24;TextRenderer.DrawText(e.Graphics,total<=0?"—":MainWindow.FormatBytes(Values[i]),Font,area,ForeColor,TextFormatFlags.Left);
        }
    }
}








