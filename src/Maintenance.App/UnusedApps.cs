using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text.Json;
using System.Xml.Linq;
namespace Maintenance.App;

sealed record UsageObservation(string Path,DateTimeOffset At,string Source,string Scope,string? ExecutableVersion=null);
sealed record UsageRow(InstalledApp App,DateTimeOffset? Last,string Source,string Coverage,string State);
sealed partial class MainWindow
{
    readonly DataGridView unusedGrid=HistoryGrid();
    readonly NumericUpDown unusedDays=new(){Minimum=30,Maximum=(DateTime.Today-DateTime.Today.AddYears(-5)).Days,Value=90,Width=90};
    readonly AppearanceChoice unusedPeriod=new(){Width=170,Height=40};
    readonly AppearanceChoice unusedView=new(){Width=230,Height=40};
    readonly AppearanceCheckBox observeUsage=new(){Text="Observe foreground use while open",AutoSize=true};
    readonly Label unusedStatus=new(){Dock=DockStyle.Top,Height=85,Padding=new(8),Text="Review recorded app usage. Missing history never means never used. Choose Read usage history to begin."};
    List<UsageRow> usageRows=[];HashSet<string> keptApps=new(StringComparer.OrdinalIgnoreCase);
    ForegroundObserver? usageObserver;
    string UsageFile=>Path.Combine(store.Folder,"usage-observations.jsonl");
    string KeptFile=>Path.Combine(store.Folder,"kept-apps.json");
    static string AppKey(InstalledApp a)=>a.Name+"|"+a.Publisher+"|"+a.Location;
    void BuildUnusedApps(){
        var page=new Panel{Text="Unused apps",Dock=DockStyle.Fill};leftoverTabs.TabPages.Add(page);leftoverTabs.Controls.Add(page);
        var actions=new FlowLayoutPanel{Dock=DockStyle.Top,AutoSize=true,Padding=new(0,8,0,8)};
        unusedPeriod.Items.AddRange(["30 days","60 days","90 days","180 days","1 year","2 years","5 years","Custom days"]);unusedPeriod.SelectedItem="90 days";
        unusedView.Items.AddRange(["Supported inactivity","Possible candidates","Usage unknown","All recorded apps","Kept apps"]);unusedView.SelectedItem="All recorded apps";
        unusedPeriod.SelectedIndexChanged+=(_,_)=>{int i=unusedPeriod.Items.IndexOf(unusedPeriod.Text);if(i>=0&&i<7)unusedDays.Value=Math.Min(unusedDays.Maximum,new[]{30,60,90,180,365,730,1826}[i]);unusedDays.Enabled=i==7;RenderUnusedApps();};unusedDays.Enabled=false;
        unusedDays.ValueChanged+=(_,_)=>RenderUnusedApps();unusedView.SelectedIndexChanged+=(_,_)=>RenderUnusedApps();
        actions.Controls.Add(StartSearch(ReadUnusedApps));actions.Controls.Add(ScanCancel());var filters=WorkflowRow();filters.Controls.Add(TextLabel("Review period",9,true));filters.Controls.Add(unusedPeriod);filters.Controls.Add(unusedDays);filters.Controls.Add(unusedView);
        actions.Controls.Add(Button("Keep / undo keep",()=>{if(unusedGrid.SelectedRows.Count==0||unusedGrid.SelectedRows[0].Tag is not UsageRow r)return;string key=AppKey(r.App);var next=new HashSet<string>(keptApps,StringComparer.OrdinalIgnoreCase);if(!next.Add(key))next.Remove(key);try{File.WriteAllText(KeptFile,JsonSerializer.Serialize(next));keptApps=next;RenderUnusedApps();}catch(Exception e)when(e is IOException or UnauthorizedAccessException){unusedStatus.Text="Could not save keep preference: "+e.Message;}}));
        actions.Controls.Add(Button("Manage installed apps…",()=>Process.Start(new ProcessStartInfo("ms-settings:appsfeatures"){UseShellExecute=true})));
        var privacy=new FlowLayoutPanel{Dock=DockStyle.Top,AutoSize=true,Padding=new(4,6,4,8)};privacy.Controls.Add(observeUsage);
        var note=TextLabel("Local only · Off by default · Records executable path and time, never window titles or command lines. Closed-app periods and source gaps remain unknown.",8);note.Width=950;note.Height=44;privacy.Controls.Add(note);
        observeUsage.CheckedChanged+=(_,_)=>{usageObserver?.Dispose();usageObserver=null;try{if(observeUsage.Checked&&!smoke)usageObserver=new ForegroundObserver(UsageFile);}catch(Exception e){observeUsage.Checked=false;unusedStatus.Text="Observation unavailable: "+e.Message;}};
        FormClosed+=(_,_)=>usageObserver?.Dispose();
        foreach(string c in new[]{"Application","Publisher","Last recorded use","Days since record","Evidence","Assessment","Coverage","Install location","Size"})unusedGrid.Columns.Add(new DataGridViewTextBoxColumn{Name=c,HeaderText=c,Width=c is "Coverage" or "Install location"?380:190,SortMode=DataGridViewColumnSortMode.Automatic});
        unusedGrid.Columns[2].DefaultCellStyle.NullValue="Unknown";unusedGrid.Columns[8].DefaultCellStyle.NullValue="Unavailable";unusedGrid.Columns[3].DefaultCellStyle.Format="N0";
        unusedGrid.CellFormatting+=(_,e)=>{if(e.ColumnIndex==8&&e.Value is long bytes){e.Value=FormatBytes(bytes)+" (registry estimate)";e.FormattingApplied=true;}else if(e.Value is DateTimeOffset t){e.Value=t.LocalDateTime.ToString("g");e.FormattingApplied=true;}};
        unusedGrid.SelectionChanged+=(_,_)=>{if(unusedGrid.SelectedRows.Count>0&&unusedGrid.SelectedRows[0].Tag is UsageRow r)leftoverDetail.Text=$"{r.App.Name}\r\n{r.App.Location}\r\n{r.Source} · {r.Coverage}\r\nAn old record does not prove there was no newer launch. Uninstall through Windows or the game's launcher; this view does not delete app data.";};
        page.Controls.Add(unusedGrid);page.Controls.Add(usageVisual);page.Controls.Add(unusedStatus);page.Controls.Add(privacy);page.Controls.Add(filters);page.Controls.Add(actions);
        try{keptApps=JsonSerializer.Deserialize<HashSet<string>>(File.ReadAllText(KeptFile))??[];}catch(Exception e)when(e is IOException or UnauthorizedAccessException or JsonException){}
    }
    async Task ReadUnusedApps(){if(busy)return;SetBusy(true);cancel=new();var token=cancel.Token;status.Text="Reading recorded application usage…";unusedStatus.Text="Reading existing Security event history and local foreground observations…";
        try{
            var result=await Task.Run(()=>{
                var observations=new List<UsageObservation>();var coverage=new List<string>();
                try{foreach(string line in File.ReadLines(UsageFile)){if(token.IsCancellationRequested)break;try{var r=JsonSerializer.Deserialize<UsageObservation>(line);if(r!=null&&!string.IsNullOrEmpty(r.Path))observations.Add(r);}catch(JsonException){}}}catch(IOException){}catch(UnauthorizedAccessException){}
                string sid=WindowsIdentity.GetCurrent().User?.Value??"";int inspected=0;var timer=Stopwatch.StartNew();
                try{using var reader=new EventLogReader(new EventLogQuery("Security",PathType.LogName,"*[System[(EventID=4688)]]"){ReverseDirection=true});
                    while(!token.IsCancellationRequested&&inspected<50000&&timer.Elapsed<TimeSpan.FromSeconds(15)){
                        using var evt=reader.ReadEvent(TimeSpan.FromSeconds(1));if(evt==null)break;inspected++;
                        var xml=XDocument.Parse(evt.ToXml());var data=xml.Descendants().Where(e=>e.Name.LocalName=="Data"&&e.Attribute("Name")!=null).ToDictionary(e=>e.Attribute("Name")!.Value,e=>e.Value);
                        string target=data.GetValueOrDefault("TargetUserSid","");string account=target is "" or "S-1-0-0"?data.GetValueOrDefault("SubjectUserSid",""):target;
                        if(account!=sid||evt.TimeCreated==null)continue;string path=data.GetValueOrDefault("NewProcessName","");if(Path.IsPathFullyQualified(path))observations.Add(new(path,new DateTimeOffset(evt.TimeCreated.Value),"Windows Security 4688 · execution, not proof of interaction","Current user; existing audit history only"));
                    }coverage.Add($"Security history: {inspected:N0} events inspected; auditing was not enabled or changed. Retention, auditing gaps and background activity prevent continuous-use claims.");
                }catch(Exception e)when(e is EventLogException or UnauthorizedAccessException or System.Security.SecurityException){coverage.Add("Security history unavailable; process auditing may be disabled or access denied.");}
                foreach(var process in Process.GetProcesses())using(process){if(token.IsCancellationRequested)break;try{string? path=process.MainModule?.FileName;if(path!=null)observations.Add(new(path,DateTimeOffset.UtcNow,"Running now · observed process","Point-in-time process snapshot; not proof of interaction"));}catch{}}
                var apps=InstalledApps();return(apps,observations,coverage);
            });
            usageRows=result.apps.Select(a=>{
                var matches=result.observations.Where(o=>UsageMatches(a,o.Path)&&!BackgroundExecutable(o.Path)).OrderByDescending(o=>o.At).ToArray();var last=matches.FirstOrDefault(o=>a.InstalledAt==null||o.At>=a.InstalledAt);
                return new UsageRow(a,last?.At,last?.Source??"No attributed record",last==null?"Unknown · registration locations may be missing; portable/Store apps and unregistered games are not fully covered":"Incomplete · foreground events only while enabled/open; historical execution can be background activity; reinstalls cannot be established",last==null?"Unknown":"Historical evidence only");
            }).ToList();
            unusedStatus.Text=$"{usageRows.Count:N0} registered apps · No app currently has enough coverage for a supported inactivity recommendation. Use Possible candidates or Usage unknown.\n"+string.Join(" ",result.coverage);RenderUnusedApps();status.Text=token.IsCancellationRequested?"Search cancelled · Partial usage results retained.":"App usage review complete · Coverage limitations are shown with each result.";
        }catch(Exception e){unusedStatus.Text="Usage review could not finish: "+e.Message;status.Text=unusedStatus.Text;}finally{cancel?.Dispose();cancel=null;SetBusy(false);}
    }
    internal static bool UsageMatches(InstalledApp app,string path){if(!Path.IsPathFullyQualified(app.Location)||!Path.IsPathFullyQualified(path))return false;try{string root=Path.GetFullPath(app.Location).TrimEnd('\\')+"\\";if(new[]{Environment.SpecialFolder.ProgramFiles,Environment.SpecialFolder.ProgramFilesX86,Environment.SpecialFolder.Windows,Environment.SpecialFolder.LocalApplicationData}.Any(f=>root.Equals(Environment.GetFolderPath(f).TrimEnd('\\')+"\\",StringComparison.OrdinalIgnoreCase)))return false;return root.Length>3&&Path.GetFullPath(path).StartsWith(root,StringComparison.OrdinalIgnoreCase);}catch{return false;}}
    internal static bool PossibleUnused(UsageRow r,DateTimeOffset cutoff)=>r.Last.HasValue&&r.Last<=cutoff&&(r.App.InstalledAt==null||r.App.InstalledAt<=cutoff)&&r.Last>=r.App.InstalledAt.GetValueOrDefault(DateTimeOffset.MinValue);
    internal static bool BackgroundExecutable(string path){string name=Path.GetFileNameWithoutExtension(path);return new[]{"update","installer","uninstall","crash","helper","service"}.Any(s=>name.Contains(s,StringComparison.OrdinalIgnoreCase));}
    void RenderUnusedApps(){ShowUsageVisual();unusedGrid.Rows.Clear();DateTimeOffset now=DateTimeOffset.UtcNow;var cutoff=unusedPeriod.Text=="5 years"?now.AddYears(-5):unusedPeriod.Text=="2 years"?now.AddYears(-2):unusedPeriod.Text=="1 year"?now.AddYears(-1):now.AddDays(-(double)unusedDays.Value);
        foreach(var r in usageRows.OrderBy(r=>r.Last??DateTimeOffset.MaxValue).ThenBy(r=>r.App.Name)){bool kept=keptApps.Contains(AppKey(r.App));bool show=unusedView.Text switch{"Supported inactivity"=>false,"Possible candidates"=>!kept&&PossibleUnused(r,cutoff),"Usage unknown"=>!kept&&!r.Last.HasValue,"Kept apps"=>kept,_=>true};if(!show)continue;
            int i=unusedGrid.Rows.Add(r.App.Name,r.App.Publisher,r.Last,r.Last.HasValue?Math.Max(0,(now-r.Last.Value).TotalDays):null,r.Source,kept?"Kept":r.State,r.Coverage,r.App.Location,r.App.SizeBytes);unusedGrid.Rows[i].Tag=r;
        }
    }
}

// Event driven foreground observation. It deliberately makes no claim about use while the app is closed.
sealed class ForegroundObserver:IDisposable
{
    delegate void WinEvent(IntPtr hook,uint evt,IntPtr window,int obj,int child,uint thread,uint time);
    [DllImport("user32.dll")]static extern IntPtr SetWinEventHook(uint min,uint max,IntPtr module,WinEvent callback,uint process,uint thread,uint flags);
    [DllImport("user32.dll")]static extern bool UnhookWinEvent(IntPtr hook);
    [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr hwnd,out uint pid);
    readonly string file;readonly WinEvent callback;IntPtr hook;string last="";DateTimeOffset lastAt;
    public ForegroundObserver(string file){this.file=file;callback=OnForeground;Write(new("",DateTimeOffset.UtcNow,"Observation session started","Foreground changes only; no continuous inactivity coverage"));hook=SetWinEventHook(3,3,IntPtr.Zero,callback,0,0,2);if(hook==IntPtr.Zero)throw new IOException("Windows foreground hook unavailable");}
    void Write(UsageObservation record)=>File.AppendAllText(file,JsonSerializer.Serialize(record)+Environment.NewLine);
    void OnForeground(IntPtr h,uint evt,IntPtr hwnd,int obj,int child,uint thread,uint time){try{GetWindowThreadProcessId(hwnd,out uint pid);using var process=Process.GetProcessById((int)pid);string path=process.MainModule?.FileName??"";if(path.Length==0)return;var now=DateTimeOffset.UtcNow;if(path==last&&now-lastAt<TimeSpan.FromMinutes(1))return;Write(new(path,now,"Foreground window observation","Local session; executable path and version only",FileVersionInfo.GetVersionInfo(path).FileVersion));last=path;lastAt=now;}catch{try{Write(new("",DateTimeOffset.UtcNow,"Observation gap","Process inaccessible or write failed"));}catch{}}}
    public void Dispose(){if(hook==IntPtr.Zero)return;UnhookWinEvent(hook);hook=IntPtr.Zero;try{Write(new("",DateTimeOffset.UtcNow,"Observation session ended","Closed-app periods unknown"));}catch{}}
}




