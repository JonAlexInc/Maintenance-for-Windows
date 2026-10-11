namespace Maintenance.App;
sealed partial class MainWindow
{
    readonly AppearanceChoice diskPasses=new(){Width=110,Height=38},diskDuration=new(){Width=150,Height=38},diskUnits=new(){Width=120,Height=38},diskWork=new(){Width=225,Height=38};
    readonly TableLayoutPanel diskCards=new(){Dock=DockStyle.Top,ColumnCount=2,RowCount=2,Padding=new(0,0,0,12)};
    readonly AppearanceChoice diskProfile=new(){Width=185,Height=38,Text="Quick benchmark"};
    readonly Label diskEstimate=new(){AutoSize=true,Padding=new(0,8,0,0)};
    void UpdateCustomCards(){bool custom=diskWork.Text==CustomWorkloads;foreach(var card in benchmarkCards){card.Selectable=custom&&speedCancel==null;card.Chosen=custom&&customWorkloads.Contains(card.Workload.Name);card.SelectionAccent=accent;card.Cursor=card.Selectable?Cursors.Hand:Cursors.Default;card.TabStop=card.Selectable;card.Invalidate();}if(speedStartButton!=null)speedStartButton.Enabled=speedCancel==null&&(speedMode.Text=="Internet speed"||SelectedWorkloads.Length>0);}
    DiskWorkload[] ActiveWorkloads=>diskProfile.Text=="Extended tests"?[..DiskBenchmark.Workloads,..DiskBenchmark.Extended]:DiskBenchmark.Workloads;
    const string CustomWorkloads="Custom selection";
    readonly HashSet<string> customWorkloads=[];
    Button? speedStartButton;
    string AllWorkloadsLabel=>$"All {ActiveWorkloads.Length} tests";
    DiskWorkload[] SelectedWorkloads=>ActiveWorkloads.Where(w=>diskWork.Text==CustomWorkloads?customWorkloads.Contains(w.Name):diskWork.Text==AllWorkloadsLabel||w.Name==diskWork.Text).ToArray();
    void SizeDiskCards(){float height=164*diskCards.DeviceDpi/96f;foreach(RowStyle style in diskCards.RowStyles)style.Height=height;diskCards.Height=(int)(height*diskCards.RowCount)+diskCards.Padding.Vertical;}
    void RebuildDiskCards(){diskCards.SuspendLayout();foreach(var old in benchmarkCards){diskCards.Controls.Remove(old);old.Dispose();}benchmarkCards.Clear();diskCards.RowStyles.Clear();diskCards.RowCount=(ActiveWorkloads.Length+1)/2;for(int row=0;row<diskCards.RowCount;row++)diskCards.RowStyles.Add(new(SizeType.Absolute,164));for(int i=0;i<ActiveWorkloads.Length;i++){var card=new BenchmarkCard(ActiveWorkloads[i]){Dock=DockStyle.Fill,Margin=new(0,0,i%2==0?12:0,12)};card.Click+=(_,_)=>{if(diskWork.Text!=CustomWorkloads||speedCancel!=null)return;if(!customWorkloads.Add(card.Workload.Name))customWorkloads.Remove(card.Workload.Name);UpdateDiskEstimate();};benchmarkCards.Add(card);diskCards.Controls.Add(card,i%2,i/2);PaintTheme(card,canvas);}SizeDiskCards();diskCards.ResumeLayout(true);}
    void UpdateDiskEstimate(){UpdateCustomCards();if(!int.TryParse(diskPasses.Text.Split(' ')[0],out int passes)||!int.TryParse(diskDuration.Text.Split(' ')[0],out int seconds))return;int total=SelectedWorkloads.Sum(w=>((w.Duration>0?w.Duration:seconds)+1)*(w.WritePercent.HasValue?1:2)*passes);diskEstimate.Text=(diskWork.Text==CustomWorkloads?$"{SelectedWorkloads.Length} selected · Click cards to choose · ":"")+$"About {TimeSpan.FromSeconds(total):m\\:ss} + file preparation · Duration is per read or write pass";}
    readonly List<BenchmarkCard> benchmarkCards=[];
    readonly List<DiskMeasurement> diskMeasurements=[];
    string diskRunContext="";
    FlowLayoutPanel BuildDiskControls()
    {
        var row=new FlowLayoutPanel{Width=1100,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,WrapContents=true};
        diskPasses.Items.AddRange(Enumerable.Range(1,9).Select(n=>$"{n} pass"+(n==1?"":"es")).ToArray());diskPasses.SelectedItem="3 passes";
        diskDuration.Width=200;diskDuration.Items.AddRange(["3 seconds / pass","5 seconds / pass","10 seconds / pass","30 seconds / pass"]);diskDuration.SelectedItem="5 seconds / pass";diskDuration.AccessibleName="Test duration per read or write pass";
        diskUnits.Items.AddRange(["MB/s","GB/s","IOPS","µs","P95 µs","P99 µs"]);diskUnits.SelectedItem="MB/s";
        diskWork.Items.Add(AllWorkloadsLabel);diskWork.Items.Add(CustomWorkloads);diskWork.Items.AddRange(DiskBenchmark.Workloads.Select(w=>w.Name).ToArray());diskWork.SelectedItem=AllWorkloadsLabel;
        diskProfile.Items.AddRange(["Quick benchmark","Extended tests"]);row.Controls.AddRange([diskProfile,diskWork,diskPasses,diskDuration,diskUnits]);
        var export=new AppearanceButton{Text="Export results…",Width=165,Height=38};row.Controls.Add(export);
        export.Click+=(_,_)=>{if(diskMeasurements.Count==0)return;using var d=new SaveFileDialog{Filter="CSV results|*.csv",FileName="Maintenance-disk-benchmark.csv"};if(d.ShowDialog(this)!=DialogResult.OK)return;try{string Q(string s)=>"\""+s.Replace("\"","\"\"")+"\"";File.WriteAllLines(d.FileName,new[]{"Context,Workload,Direction,Pass,MB/s,IOPS,Average latency us,P95 latency us,P99 latency us"}.Concat(diskMeasurements.Select(m=>string.Join(",",Q(diskRunContext),Q(m.Workload),m.Write?"Write":"Read",m.Pass.ToString(),m.Score.MBps.ToString("R",System.Globalization.CultureInfo.InvariantCulture),m.Score.Iops.ToString("R",System.Globalization.CultureInfo.InvariantCulture),m.Score.Microseconds.ToString("R",System.Globalization.CultureInfo.InvariantCulture),m.Score.P95Microseconds.ToString("R",System.Globalization.CultureInfo.InvariantCulture),m.Score.P99Microseconds.ToString("R",System.Globalization.CultureInfo.InvariantCulture)))));}catch(Exception ex){speedState.Text="Export failed: "+ex.Message;}};
        diskCards.ColumnStyles.Add(new(SizeType.Percent,50));diskCards.ColumnStyles.Add(new(SizeType.Percent,50));
        row.SetFlowBreak(export,true);row.Controls.Add(diskEstimate);
        diskCards.AutoScroll=false;diskCards.Resize+=(_,_)=>SizeDiskCards();RebuildDiskCards();
        diskProfile.SelectedIndexChanged+=(_,_)=>{diskWork.Items.Clear();diskWork.Items.Add(AllWorkloadsLabel);diskWork.Items.Add(CustomWorkloads);diskWork.Items.AddRange(ActiveWorkloads.Select(w=>w.Name));diskWork.SelectedItem=AllWorkloadsLabel;RebuildDiskCards();diskMeasurements.Clear();RenderDiskResults();UpdateDiskEstimate();};
        diskPasses.SelectedIndexChanged+=(_,_)=>UpdateDiskEstimate();diskDuration.SelectedIndexChanged+=(_,_)=>UpdateDiskEstimate();diskWork.SelectedIndexChanged+=(_,_)=>UpdateDiskEstimate();UpdateDiskEstimate();
        speedState.TextChanged+=(_,_)=>{foreach(var card in benchmarkCards){card.Active=speedCancel!=null&&speedState.Text.Contains(card.Workload.Name);card.Invalidate();}};        diskUnits.SelectedIndexChanged+=(_,_)=>RenderDiskResults();RenderDiskResults();return row;
    }
    void RenderDiskResults()
    {
        string unit=diskUnits.SelectedItem??"MB/s";
        foreach(var card in benchmarkCards) {
            card.Unit=unit;card.Samples=diskMeasurements.Where(m=>m.Workload==card.Workload.Name).ToArray();card.Invalidate();
        }
    }
}








