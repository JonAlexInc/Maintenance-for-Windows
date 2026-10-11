using System.Diagnostics;
using System.Net.Http;
using System.Security.Cryptography;
namespace Maintenance.App;
sealed partial class MainWindow
{
    readonly AppearanceChoice speedMode=new(){Width=235,Height=38}, speedTarget=new(){Width=310,Height=38}, speedSize=new(){Width=170,Height=38};
    readonly TextBox speedFolder=new(){Width=650};
    readonly Label speedNotes=TextLabel("",10),speedState=TextLabel("Ready",11,true), speedWrite=TextLabel("—",24,true),speedRead=TextLabel("—",24,true);
    readonly ListBox speedHistory=new(){Dock=DockStyle.Fill,HorizontalScrollbar=true,IntegralHeight=false};
    readonly AppearanceChoice internetDirection=new(){Width=250,Height=38,Text="Download + Upload"},internetSize=new(){Width=170,Height=38,Text="100 MB"};
    readonly InternetGauge downloadGauge=new(){Title="DOWNLOAD",Dock=DockStyle.Fill,Accent=Color.FromArgb(24,175,183)},uploadGauge=new(){Title="UPLOAD",Dock=DockStyle.Fill,Accent=Color.FromArgb(139,103,220)};
    CancellationTokenSource? speedCancel;
    readonly Dictionary<string,string> internetUploads=new(){[InternetBenchmark.Cloudflare]="https://speed.cloudflare.com/__up",["Clouvider · Amsterdam"]="https://ams.speedtest.clouvider.net/backend/empty.php",["GARR · Rome"]="https://st-be-rm2.infra.garr.it/empty.php"};
    readonly Dictionary<string,string> internetTargets=new() {
        ["Clouvider · Amsterdam"]="https://ams.speedtest.clouvider.net/backend/garbage.php",
        ["GARR · Rome"]="https://st-be-rm2.infra.garr.it/garbage.php",
        [InternetBenchmark.Cloudflare]="https://speed.cloudflare.com/__down",
        ["US East · Virginia"]="https://was1-vin.speedtest.network.ovh.net/files/100Mb.dat",
        ["US West · Oregon"]="https://pdx1-hil.speedtest.network.ovh.net/files/100Mb.dat",
        ["Europe · London"]="https://lon1-eri.speedtest.network.ovh.net/files/100Mb.dat",
        ["Europe · France"]="https://lil1-rbx.speedtest.network.ovh.net/files/100Mb.dat",
        ["Asia · Singapore"]="https://sin1-sgp.speedtest.network.ovh.net/files/100Mb.dat",
        ["Oceania · Sydney"]="https://syd1-sy2.speedtest.network.ovh.net/files/100Mb.dat"    };
    void BuildSpeedTests()
    {
        var body=Body(Page("Speed tests","Measure a local disk, a writable network folder, or your internet connection."));
        var setup=new FlowLayoutPanel {Dock=DockStyle.Top,Height=320,FlowDirection=FlowDirection.TopDown,WrapContents=false};
        var choices=new FlowLayoutPanel {Width=1100,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,MinimumSize=new(0,52)};
        speedMode.Items.AddRange(["Local disk","Network folder","Internet speed"]);speedMode.SelectedItem="Local disk";
        speedSize.Items.AddRange(DiskBenchmark.Sizes.Keys.ToArray());speedSize.SelectedItem="1 GiB";
        var sizeLabel=new Label{Text="Test file size",AutoSize=true,Padding=new(6,9,4,0)};choices.Controls.AddRange([speedMode,speedTarget,sizeLabel,speedSize]);setup.Controls.Add(choices);var diskOptions=BuildDiskControls();setup.Controls.Add(diskOptions);
        var internetOptions=new FlowLayoutPanel{Width=1100,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,MinimumSize=new(0,52)};internetDirection.Items.AddRange(["Download + Upload","Download only","Upload only"]);internetSize.Items.AddRange(InternetBenchmark.Sizes.Keys);internetOptions.Controls.AddRange([internetDirection,new Label{Text="Size per direction",AutoSize=true,Padding=new(4,9,4,0)},internetSize]);setup.Controls.Add(internetOptions);
        var folderRow=new TableLayoutPanel {Width=1100,Height=50,ColumnCount=2,RowCount=1};folderRow.ColumnStyles.Add(new(SizeType.Percent,100));folderRow.ColumnStyles.Add(new(SizeType.Absolute,185));speedFolder.Dock=DockStyle.Fill;folderRow.Controls.Add(speedFolder,0,0);
        var browse=new AppearanceButton{Text="Choose folder…",Width=165,Height=36};
        browse.Click+=(_,_)=>{using var picker=new FolderBrowserDialog{Description=speedMode.SelectedItem=="Network folder"?"Choose a writable network share or mapped-drive folder":"Choose a writable folder for the temporary speed-test file",UseDescriptionForTitle=true};if(picker.ShowDialog(this)==DialogResult.OK)speedFolder.Text=picker.SelectedPath;};browse.Dock=DockStyle.Fill;folderRow.Controls.Add(browse,1,0);setup.Controls.Add(folderRow);
        setup.Resize+=(_,_)=>{choices.Width=setup.ClientSize.Width;diskOptions.Width=setup.ClientSize.Width;folderRow.Width=setup.ClientSize.Width;internetOptions.Width=setup.ClientSize.Width;speedNotes.Width=Math.Max(100,setup.ClientSize.Width-12); SizeNotes();};
        speedNotes.Width=1100;speedNotes.AutoSize=false;speedNotes.Height=110;speedNotes.AutoEllipsis=false;setup.Controls.Add(speedNotes);
        void SizeNotes() { speedNotes.Height = Math.Max(88, TextRenderer.MeasureText(speedNotes.Text,speedNotes.Font,new Size(speedNotes.Width,int.MaxValue),TextFormatFlags.WordBreak).Height + 14); setup.PerformLayout(); }
        speedNotes.TextChanged += (_,_) => SizeNotes();
        bool sizingSetup = false;
        void FitSetup() {
            if (sizingSetup || !setup.Visible) return;
            sizingSetup = true;
            try { int height = setup.Padding.Vertical + setup.Controls.Cast<Control>().Where(c => c.Visible).Sum(c => c.Height + c.Margin.Vertical); if (setup.Height != height) setup.Height = height; }
            finally { sizingSetup = false; }
        }
        setup.Layout += (_,_) => FitSetup(); setup.VisibleChanged += (_,_) => FitSetup();
        var actions=new FlowLayoutPanel{Dock=DockStyle.Top,Height=60};
        var start=new AppearanceButton{Text="Start test",Width=170,Height=42};var stop=new AppearanceButton{Text="Cancel",Width=130,Height=42,Enabled=false};actions.Controls.AddRange([start,stop]);speedStartButton=start;
        var meters=new TableLayoutPanel{Dock=DockStyle.Top,Height=(int)(330*DeviceDpi/96f),ColumnCount=2,RowCount=1};meters.ColumnStyles.Add(new(SizeType.Percent,50));meters.ColumnStyles.Add(new(SizeType.Percent,50));meters.Controls.Add(downloadGauge,0,0);meters.Controls.Add(uploadGauge,1,0);
        speedState.Dock=DockStyle.Top;speedState.Height=72;speedState.AutoEllipsis=false;
        speedHistory.Dock=DockStyle.Bottom;speedHistory.Height=110;var diskViewport=new Panel{Dock=DockStyle.Fill,AutoScroll=true};diskViewport.Controls.Add(diskCards);body.Controls.Add(diskViewport);body.Controls.Add(speedHistory);body.Controls.Add(speedState);body.Controls.Add(meters);body.Controls.Add(actions);body.Controls.Add(setup);
        void Configure() {
            bool internet=speedMode.SelectedItem=="Internet speed";
            speedTarget.Items.Clear();
            if(internet)speedTarget.Items.AddRange(internetTargets.Keys.Where(k=>internetDirection.Text=="Download only"||internetUploads.ContainsKey(k)).ToArray());
            else if(speedMode.SelectedItem=="Local disk")speedTarget.Items.AddRange(DriveInfo.GetDrives().Where(d=>d.IsReady&&IsLocalStorage(d.DriveType)).Select(d=>$"{d.Name} · {(d.TotalSize-d.AvailableFreeSpace)*100/Math.Max(1,d.TotalSize)}% used · {FormatBytes(d.AvailableFreeSpace)} free").ToArray());
            else {speedFolder.Clear();speedTarget.Items.Add("Browse or enter a network path");speedTarget.Items.AddRange(DriveInfo.GetDrives().Where(d=>d.DriveType==DriveType.Network).Select(d=>d.Name).ToArray());}
            bool network=speedMode.SelectedItem=="Network folder";
            browse.Text=network?"Choose network folder…":"Choose folder…";folderRow.ColumnStyles[1].Width=(network?255:185)*DeviceDpi/96f;
            speedFolder.PlaceholderText=network?@"\\server\share\folder or mapped drive folder":"Writable test folder";
            speedTarget.Visible=true;diskMeasurements.Clear();RenderDiskResults();speedRead.Text="—";speedState.Text="Ready";
            speedTarget.SelectedItem=internet?InternetBenchmark.Cloudflare:speedTarget.Items.FirstOrDefault()??"";folderRow.Visible=!internet;speedSize.Visible=!internet;sizeLabel.Visible=!internet;diskOptions.Visible=!internet;internetOptions.Visible=internet;diskViewport.Visible=!internet;diskCards.Visible=!internet;meters.Visible=internet;
            UpdateNotes();UpdateDiskEstimate();
        }
        void UpdateNotes() {
            bool internet=speedMode.SelectedItem=="Internet speed";

            if(speedMode.SelectedItem=="Local disk")speedFolder.Text=SpeedRunner.DefaultFolder((speedTarget.SelectedItem??"").Split(" · ")[0]);
            if(speedMode.SelectedItem=="Network folder"&&Path.IsPathFullyQualified(speedTarget.SelectedItem??""))speedFolder.Text=speedTarget.SelectedItem;
            speedNotes.Text=internet?$"{InternetBenchmark.Sizes[internetSize.Text]/1e6:N0} MB per direction · {InternetBenchmark.Sizes[internetSize.Text]/1e6*(internetDirection.Text=="Download + Upload"?2:1):N0} MB total test data. Server sees your IP; upload uses generated data only.\nSingle-connection transfers, including request overhead; intentional server pacing is excluded. Regional targets are download-only; Cloudflare, Clouvider and GARR support both directions.":"Timed tests repeatedly access the test file; file size is working space, not total data transferred.\nExtended sustained tests run 60 seconds per direction per pass. Writes can exceed file size. Latency: lower is better; percentiles are averaged across passes.";
        }
        internetDirection.SelectedIndexChanged+=(_,_)=>Configure();internetSize.SelectedIndexChanged+=(_,_)=>UpdateNotes();
        speedMode.SelectedIndexChanged+=(_,_)=>Configure();speedTarget.SelectedIndexChanged+=(_,_)=>UpdateNotes();
        void LockSettings(bool locked) {
            foreach(var control in new Control[]{speedMode,speedTarget,speedSize,speedFolder,browse,diskPasses,diskDuration,diskWork,diskProfile,internetDirection,internetSize})control.Enabled=!locked;
            UpdateCustomCards();
        }
        start.Tag=StartRole;start.Text="Start test";
        speedState.Font=new("Segoe UI",10);speedState.Height=48;
        speedHistory.BorderStyle=BorderStyle.None;speedHistory.Height=66;speedHistory.Font=new("Segoe UI",8);
        stop.Click+=(_,_)=>{speedCancel?.Cancel();speedState.Text="Cancel requested… waiting for the current operation.";};
        start.Click+=async(_,_)=> {
            if(speedCancel!=null||emergencyStopping)return;
            if(speedMode.Text!="Internet speed"&&SelectedWorkloads.Length==0){speedState.Text="Choose at least one test card to start.";return;}
            string mode=speedMode.SelectedItem??"",folder=speedFolder.Text.Trim(),target=speedTarget.SelectedItem??"";
            try { if(mode!="Internet speed") {
                if(!Path.IsPathFullyQualified(folder)){speedState.Text="Choose an absolute writable folder path first.";return;}
                if(mode=="Local disk"&&!IsLocalStorage(new DriveInfo(Path.GetPathRoot(folder)!).DriveType)){speedState.Text="Choose a local drive, or switch to Network folder.";return;}
            }
            } catch(Exception ex) {speedState.Text="Choose a valid folder: "+ex.Message;return;}
            speedRunId=Guid.NewGuid().ToString("N");speedRunMode=mode;speedRunTarget=mode=="Internet speed"?target:folder;speedSaveError="";SaveSpeedRun("Started","Test started; final status pending.");
            speedCancel=new CancellationTokenSource(mode=="Internet speed"?TimeSpan.FromMinutes(15):TimeSpan.FromHours(2));start.Enabled=false;stop.Enabled=true;LockSettings(true);
            speedWrite.Text="—";speedRead.Text="—";speedState.Text="Starting…";
            var progress=new Progress<string>(s=>{if(!IsDisposed)speedState.Text=s;});
            try {
                if(mode=="Internet speed") {
                    bool down=internetDirection.Text!="Upload only",up=internetDirection.Text!="Download only";long bytes=InternetBenchmark.Sizes[internetSize.Text];
                    downloadGauge.Reset(down?"Waiting":"Not selected");uploadGauge.Reset(up?"Waiting":"Not selected");
                    var finished=new bool[2];var live=new Progress<InternetProgress>(p=>{if(IsDisposed||speedCancel==null||finished[p.Upload?1:0])return;(p.Upload?uploadGauge:downloadGauge).Update(p);speedState.Text=p.Status??((p.Upload?"Uploading":"Downloading")+$" · {p.Bytes/1e6:N1} / {p.Total/1e6:N0} MB");});
                    var failures=new List<string>();
                    foreach(bool upload in new[]{false,true}.Where(u=>u?up:down)){
                        speedState.Text=upload?"Starting upload…":"Starting download…";
                        try{
                        var result=await InternetBenchmark.Run(upload,bytes,internetTargets[target],live,speedCancel.Token,internetUploads.GetValueOrDefault(target));
                        finished[upload?1:0]=true;(upload?uploadGauge:downloadGauge).Update(new(upload,result.Bytes,bytes,result.Mbps,true));
                        speedHistory.Items.Insert(0,$"{DateTime.Now:t} · {target} · {(upload?"Upload":"Download")} {result.Mbps:N1} Mbps · {result.Bytes/1e6:N0} MB · {result.Seconds:N2}s");
                        SaveSpeedSample("Internet transfer",upload?"Upload":"Download",1,result.Bytes,result.Mbps,"Mbps",detail:$"Average over {result.Seconds:N2}s including request overhead; intentional server pacing excluded");
                        }catch(Exception ex)when(ex is not OperationCanceledException){finished[upload?1:0]=true;var gauge=upload?uploadGauge:downloadGauge;gauge.State="Failed · no average";gauge.Invalidate();failures.Add((upload?"Upload: ":"Download: ")+ex.Message);SaveSpeed(new(speedRunId,mode,target,"Internet transfer",upload?"Upload":"Download",1,bytes,null,"Mbps",null,null,null,null,"Failed",ex.Message));}
                    }
                    speedState.Text=failures.Count==0?"Complete · Average throughput includes connection / request overhead":"Finished with errors · "+string.Join(" · ",failures);SaveSpeedRun(failures.Count==0?"Completed":"Incomplete",speedState.Text);                } else {
                    long size=DiskBenchmark.Sizes[speedSize.SelectedItem!];
                    int passes=int.Parse(diskPasses.SelectedItem!.Split(' ')[0]),seconds=int.Parse(diskDuration.SelectedItem!.Split(' ')[0]);
                    var workloads=SelectedWorkloads;
                    diskMeasurements.Clear();RenderDiskResults();diskRunContext=$"{DateTime.Now:O} | {folder} | {speedSize.SelectedItem} | {passes} passes | {seconds}s | DiskSpd 2.3 -Sh | Random data";
                    var measured=new InlineProgress<DiskMeasurement>(m=>{SaveSpeedSample(m.Workload,m.Write?"Write":"Read",m.Pass,size,m.Score.MBps,"MB/s",m.Score.Iops,m.Score.Microseconds,m.Score.P95Microseconds,m.Score.P99Microseconds,diskRunContext);BeginInvoke(new Action(()=>{diskMeasurements.Add(m);RenderDiskResults();}));});
                    await Task.Run(()=>DiskBenchmark.Run(folder,size,passes,seconds,workloads,progress,measured,speedCancel.Token));
                    speedState.Text="Complete · Test file removed · Mean results; change units or export individual passes";
                    speedHistory.Items.Insert(0,diskRunContext);SaveSpeedRun("Completed",diskRunContext);                }
            } catch(OperationCanceledException){SaveSpeedRun("Cancelled","Run cancelled or timed out; only completed measurements are retained.");speedState.Text="Stopped or time limit reached. Only completed passes are shown; an incomplete suite is not comparable to a complete run.";}
              catch(UnauthorizedAccessException){SaveSpeedRun("Failed","Folder access denied");speedState.Text="This folder does not allow test-file writes. Use Choose folder to select a writable folder on the same drive. Administrator access is not required.";}
              catch(Exception ex){SaveSpeedRun("Failed",ex.Message);speedState.Text="Test could not finish: "+ex.Message;}
            finally {foreach(var gauge in new[]{downloadGauge,uploadGauge})if(gauge.State.Contains("Sending")||gauge.State.Contains("Receiving")||gauge.State=="Waiting"){gauge.State="Incomplete · no final result";gauge.Invalidate();}speedCancel.Dispose();speedCancel=null;start.Enabled=true;stop.Enabled=false;LockSettings(false);RefreshHistory();if(speedSaveError.Length>0)speedState.Text+=" · "+speedSaveError;foreach(var card in benchmarkCards){card.Active=false;card.Invalidate();}}
        };
        FormClosing+=(_,e)=>{if(speedCancel!=null){speedCancel.Cancel();e.Cancel=true;speedState.Text="Stopping test and removing its file. Close again after it finishes.";}};
        Configure();
    }
}
static class SpeedRunner
{
    internal static string DefaultFolder(string drive)
    {
        if(string.IsNullOrWhiteSpace(drive))return "";
        // Never redirect to a different volume: the selected drive is the test target.
        foreach(var folder in new[]{Path.GetTempPath(),Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)})
            if(Path.GetPathRoot(folder)?.Equals(drive,StringComparison.OrdinalIgnoreCase)==true&&Directory.Exists(folder))return folder;
        return drive;
    }
    internal static async Task<(double Write,double Read)> FileTest(string folder,long bytes,IProgress<string> progress,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if(!Directory.Exists(folder))throw new IOException("Folder is unavailable.");
        string path=Path.Combine(folder,".maintenance-speed-"+Guid.NewGuid().ToString("N")+".tmp");bool created=false;
        byte[] buffer=new byte[1024*1024];RandomNumberGenerator.Fill(buffer);
        try {
            double write;
            using(var file=new FileStream(path,FileMode.CreateNew,FileAccess.ReadWrite,FileShare.None,buffer.Length,FileOptions.Asynchronous|FileOptions.SequentialScan|FileOptions.WriteThrough)) {
                created=true;var timer=Stopwatch.StartNew();long done=0;
                while(done<bytes){token.ThrowIfCancellationRequested();int count=(int)Math.Min(buffer.Length,bytes-done);await file.WriteAsync(buffer.AsMemory(0,count),token);done+=count;if(done%(16*buffer.Length)==0)progress.Report($"Writing · {done*100/bytes}%");}
                file.Flush(true);write=bytes/1_000_000.0/timer.Elapsed.TotalSeconds;
            }
            token.ThrowIfCancellationRequested();progress.Report("Reading test file…");
            using var read=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.None,buffer.Length,FileOptions.Asynchronous|FileOptions.SequentialScan);
            var watch=Stopwatch.StartNew();long total=0;int n;
            while((n=await read.ReadAsync(buffer,token))>0){total+=n;if(total%(16*buffer.Length)==0)progress.Report($"Reading · {total*100/bytes}%");}
            if(total!=bytes)throw new IOException("Test file length changed; result discarded.");
            return(write,total/1_000_000.0/watch.Elapsed.TotalSeconds);
        } finally {
            if(created)try {File.Delete(path);}catch(Exception e){throw new IOException("Unable to remove the test file. Remove this file when the share is available: "+path,e);}
        }
    }
    internal static async Task<HttpResponseMessage> Connect(HttpClient client,string url,CancellationToken token)
    {
        try{return await client.GetAsync(url,HttpCompletionOption.ResponseHeadersRead,token);}
        catch(HttpRequestException ex){var cause=ex;string detail=ex.GetBaseException().Message;throw new IOException($"Could not reach {new Uri(url).Host}: {detail} Try another location; check proxy/VPN or firewall settings if all locations fail.",ex);}
    }
    internal static async Task<(double Rate,long Bytes,double Seconds)> Download(string url,IProgress<string> progress,CancellationToken token)
    {
        using var handler=new SocketsHttpHandler {ConnectTimeout=TimeSpan.FromSeconds(15)};
        using var client=new HttpClient(handler){Timeout=Timeout.InfiniteTimeSpan};
        var watch=Stopwatch.StartNew();
                using var response=await Connect(client,url,token);response.EnsureSuccessStatusCode();
        if(response.Content.Headers.ContentType?.MediaType?.Contains("text",StringComparison.OrdinalIgnoreCase)==true)throw new IOException("Target returned a page instead of test data.");
        await using var stream=await response.Content.ReadAsStreamAsync(token);byte[] buffer=new byte[128*1024];long total=0;const long limit=100_000_000;
        while(total<limit){int n=await stream.ReadAsync(buffer.AsMemory(0,(int)Math.Min(buffer.Length,limit-total)),token);if(n==0)break;total+=n;if(total%(1024*1024)<buffer.Length)progress.Report($"Downloading · {total/1_000_000.0:N1} / 100 MB");}
        if(total<limit)throw new IOException("Target ended before 100 MB; result discarded.");
        return(total/1_000_000.0/watch.Elapsed.TotalSeconds,total,watch.Elapsed.TotalSeconds);
    }
}

























