using Maintenance.Core;
using Microsoft.Win32;
using System.Diagnostics;
namespace Maintenance.App;
sealed record InstalledApp(string Name,string Publisher,string Location,string Version,string Evidence,long? SizeBytes=null,DateTimeOffset? InstalledAt=null);
sealed record AppCacheReview(string Owner,string Path,string Category,long Size,string Coverage,string Evidence);
sealed partial class MainWindow
{
    const string LeftoverMode="Apps & leftovers";
    readonly Panel leftoverPanel=new(){Dock=DockStyle.Fill,Visible=false};
    readonly DataGridView leftoverGrid=HistoryGrid(),installedGrid=HistoryGrid();
    readonly Label leftoverSummary=new(){Dock=DockStyle.Top,Height=90,Padding=new(12),Text="Review application caches and installed apps\nA missing registration is not proof of an uninstall. Nothing is selected automatically."};
    readonly TextBox leftoverDetail=new(){Dock=DockStyle.Bottom,Height=105,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical};
    readonly HardwarePages leftoverTabs=new(){Dock=DockStyle.Fill};
    string? remnantReviewOwner;
    Button? cacheBackButton; int cacheReturnTab;
    void ReturnToAppCache(){if(busy)return;scanLocation.SelectedItem=LeftoverMode;leftoverTabs.SelectedIndex=cacheReturnTab;status.Text="Returned to your app cache results. Your selection is preserved.";}
    Control BuildLeftoverPanel()
    {
        var actions=new FlowLayoutPanel{Dock=DockStyle.Top,AutoSize=true,MinimumSize=new(0,60),Padding=new(4,8,0,8)};
        actions.Controls.Add(StartSearch(ScanAppLeftovers));actions.Controls.Add(ScanCancel());actions.Controls.Add(Button("Review highlighted cache…",ReviewAppCache));actions.Controls.Add(Button("Windows installed apps…",()=>Process.Start(new ProcessStartInfo("ms-settings:appsfeatures"){UseShellExecute=true})));
        foreach(var (name,width) in new[]{("Possible owner",210),("Data category",150),("Size",130),("Coverage",170),("Ownership evidence / confidence",570),("Folder",650)})leftoverGrid.Columns.Add(new DataGridViewTextBoxColumn{Name=name,Width=width,SortMode=DataGridViewColumnSortMode.Automatic});
        foreach(var (name,width) in new[]{("Application",320),("Publisher",260),("Version",180),("Install location",450),("Evidence",290)})installedGrid.Columns.Add(new DataGridViewTextBoxColumn{Name=name,Width=width,SortMode=DataGridViewColumnSortMode.Automatic});
        leftoverGrid.CellFormatting+=(_,e)=>{if(e.ColumnIndex==2&&e.Value is long n){e.Value=FormatBytes(n);e.FormattingApplied=true;}};
        leftoverGrid.SelectionChanged+=(_,_)=>{if(leftoverGrid.SelectedRows.Count>0&&leftoverGrid.SelectedRows[0].Tag is AppCacheReview item)leftoverDetail.Text=item.Path+"\r\n"+item.Evidence+"\r\nReview only candidate: cache/log folder names do not prove every contained file is disposable. Selected files can be sent to the Recycle Bin after review.";};
        var candidates=new Panel{Text="Caches & leftovers",Dock=DockStyle.Fill};candidates.Controls.Add(leftoverGrid);candidates.Controls.Add(leftoverSummary);candidates.Controls.Add(actions);candidates.Controls.Add(leftoverDetail);leftoverTabs.TabPages.Add(candidates);leftoverTabs.Controls.Add(candidates);var installed=new Panel{Text="Installed apps",Dock=DockStyle.Fill};installed.Controls.Add(installedGrid);var installedActions=WorkflowRow();installedActions.Controls.Add(StartSearch(ScanAppLeftovers));installedActions.Controls.Add(ScanCancel());installedActions.Controls.Add(Button("Windows installed apps…",()=>Process.Start(new ProcessStartInfo("ms-settings:appsfeatures"){UseShellExecute=true})));installed.Controls.Add(installedActions);leftoverTabs.TabPages.Add(installed);leftoverTabs.Controls.Add(installed);
        BuildUnusedApps();BuildAppDataMap();leftoverPanel.Controls.Add(leftoverTabs);leftoverPanel.Controls.Add(ModernTabs(leftoverTabs));return leftoverPanel;
    }
    internal static List<InstalledApp> InstalledApps()
    {
        var apps=new List<InstalledApp>();
        foreach(var hive in new[]{Registry.CurrentUser,Registry.LocalMachine})foreach(string path in new[]{@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",@"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"}){
            using var parent=hive.OpenSubKey(path);if(parent==null)continue;foreach(string id in parent.GetSubKeyNames()){using var app=parent.OpenSubKey(id);string name=app?.GetValue("DisplayName")?.ToString()??"";if(name.Length==0)continue;apps.Add(new(name,app?.GetValue("Publisher")?.ToString()??"",app?.GetValue("InstallLocation")?.ToString()??"",app?.GetValue("DisplayVersion")?.ToString()??"",hive==Registry.CurrentUser?"Current-user uninstall registration":"Machine uninstall registration",long.TryParse(app?.GetValue("EstimatedSize")?.ToString(),out long size)&&size>0&&size<long.MaxValue/1024?size*1024:null,DateTime.TryParseExact(app?.GetValue("InstallDate")?.ToString(),"yyyyMMdd",System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.AssumeLocal,out var installed)?new DateTimeOffset(installed):null));}
        }
        return apps.DistinctBy(a=>(a.Name,a.Location,a.Version)).OrderBy(a=>a.Name).ToList();
    }
    internal static bool DisposableFolderName(string name)=>new[]{"Cache","Code Cache","GPUCache","Logs","Log","Temp","CrashDumps"}.Contains(name,StringComparer.OrdinalIgnoreCase);
    async Task ScanAppLeftovers()
    {
        if(busy)return;SetBusy(true);cancel=new();leftoverGrid.Rows.Clear();installedGrid.Rows.Clear();leftoverSummary.Text="Reviewing current-account app data…\nSettings, databases and whole application folders are not cleanup targets.";
        try{
            var apps=await Task.Run(InstalledApps);foreach(var app in apps)installedGrid.Rows.Add(app.Name,app.Publisher,app.Version,app.Location,app.Evidence);
            var token=cancel.Token;var candidates=await Task.Run(()=>{
                var found=new List<AppCacheReview>();string local=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var ignored=new[]{"Microsoft","Packages","Programs","Temp","Google","Mozilla","BraveSoftware","Vivaldi","Chromium","Opera Software","NVIDIA","AMD","MaintenanceForWindows"};
                foreach(string owner in Directory.EnumerateDirectories(local)){
                    if(token.IsCancellationRequested)break;string name=Path.GetFileName(owner);if(ignored.Contains(name,StringComparer.OrdinalIgnoreCase))continue;
                    try{
                        if((File.GetAttributes(owner)&FileAttributes.ReparsePoint)!=0)continue;
                        var levels=Directory.EnumerateDirectories(owner).ToArray();var targets=levels.Where(p=>DisposableFolderName(Path.GetFileName(p))).ToList();
                        foreach(string child in levels.Where(p=>!DisposableFolderName(Path.GetFileName(p)))){
                            if(token.IsCancellationRequested)break;if((File.GetAttributes(child)&FileAttributes.ReparsePoint)!=0)continue;
                            try{targets.AddRange(Directory.EnumerateDirectories(child).Where(p=>DisposableFolderName(Path.GetFileName(p))));}catch(Exception e)when(e is IOException or UnauthorizedAccessException){}
                        }
                        var matched=apps.Where(a=>a.Name.Contains(name,StringComparison.OrdinalIgnoreCase)||(!string.IsNullOrWhiteSpace(a.Location)&&a.Location.StartsWith(owner+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))).Take(3).ToArray();
                        bool executable=Directory.EnumerateFiles(owner,"*.exe",SearchOption.TopDirectoryOnly).Any()||levels.Any(p=>File.Exists(Path.Combine(p,Path.GetFileName(p)+".exe")));
                        string evidence=matched.Length>0?"Installed-app match: "+string.Join(", ",matched.Select(a=>a.Name))+". Not classified as abandoned.":executable?"Executable present: possible per-user / portable installation. Not classified as abandoned.":"Uncertain: no match in user + machine uninstall catalogs; portable, Store, shared or custom installs may still use this folder. Abandonment not established.";
                        foreach(string target in targets){if(token.IsCancellationRequested)break;var size=MeasureUpdateLocation(target,token);found.Add(new(name,target,Path.GetFileName(target),size.Bytes,size.Coverage,evidence));}
                    }catch(Exception e)when(e is IOException or UnauthorizedAccessException){}
                }return found;
            });
            foreach(var item in candidates.OrderByDescending(c=>c.Size)){int index=leftoverGrid.Rows.Add(item.Owner,item.Category,item.Size,item.Coverage,item.Evidence,item.Path);leftoverGrid.Rows[index].Tag=item;}
            leftoverSummary.Text=$"{candidates.Count} cache / log folders · {FormatBytes(candidates.Sum(c=>c.Size))} measured · {apps.Count} registered apps\n{(token.IsCancellationRequested?"Cancelled — partial review. ":"")}Ownership remains uncertain. Installed-app usage is not inferred; use Windows to uninstall apps you no longer need.";
            leftoverDetail.Text="Coverage: current-account AppData\\Local, two folder levels, known cache/log names only. Store package folders, common shared vendors and browser profiles are excluded. No age-based abandoned-app verdicts. Settings, credentials, databases and user content are retained outside these review targets.";
            using var journal=store.StartJournal();LocalStore.Append(journal,new{Kind="App data review",Location=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),Summary="Scan app data: "+candidates.Count+" possible cache/log folders; ownership unconfirmed; no deletion."});
        }finally{cancel.Dispose();cancel=null;SetBusy(false);RefreshHistory();}
    }
    async Task ReviewAppCache()
    {
        if(busy||leftoverTabs.SelectedIndex!=0||leftoverGrid.SelectedRows.Count==0||leftoverGrid.SelectedRows[0].Tag is not AppCacheReview item)return;
        await OpenCacheReview(item,0);
    }
    async Task OpenCacheReview(AppCacheReview item,int returnTab)
    {
        cacheReturnTab=returnTab;
        chosenReviewFolder=item.Path;RefreshScanLocations();scanLocation.SelectedItem="Chosen folder";await ScanLocalDrive(item.Path);remnantReviewOwner=item.Owner;if(cacheBackButton!=null)cacheBackButton.Visible=true;scopeHint.Text="Apps & leftovers › "+item.Owner+" › "+item.Category;
        scanSummary.Text="App cache review · "+item.Owner+" · "+item.Evidence;cleanupNotice.Text="Review every selected file. Use Recycle selected for recovery through the Windows Recycle Bin; no whole app folder is removed.";
    }
}








