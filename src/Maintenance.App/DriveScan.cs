using Maintenance.Core;

namespace Maintenance.App;
sealed partial class MainWindow
{
    readonly AppearanceChoice scanLocation=new() { Width=360,Height=42,Text=TempMode,AccessibleName="Scan location" };
    readonly Dictionary<string,string> scanDrives=new();
    bool driveReview;
    Control BuildScanLocation()
    {
        var row=new FlowLayoutPanel { Dock=DockStyle.Top,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,MinimumSize=new(0,62),Padding=new(0,6,0,0) };
        row.Controls.Add(new Label { Text="Scan location",AutoSize=true,Padding=new(0,10,8,0) });
        scanLocation.Items.Add(TempMode);row.Controls.Add(scanLocation);cacheBackButton=Button("‹ Back to Apps & leftovers",ReturnToAppCache);cacheBackButton.Visible=false;row.Controls.Add(cacheBackButton);
        chooseScanTarget=Button("Choose folder…",async()=>{if(scanLocation.Text==StreamsMode)await ChooseStreamDrive();else ChooseReviewFolder();});chooseScanTarget.Visible=false;row.Controls.Add(chooseScanTarget); scopeHint.Text="Temporary files: cleanup candidates. Personal folders: review only.";row.Controls.Add(scopeHint);
        scanLocation.SelectedIndexChanged+=(_,_)=> { if(!busy) { if(scanLocation.Text!="Chosen folder"){remnantReviewOwner=null;if(cacheBackButton!=null)cacheBackButton.Visible=false;} ClearResults();SetCleanupMode();chooseScanTarget.Visible=scanLocation.Text==StreamsMode;chooseScanTarget.Text=scanLocation.Text==StreamsMode?"Choose drive…":"Choose folder…";scopeHint.Text=scanLocation.Text==RestoreMode?"Recovery options · Review before permanent removal":scanLocation.Text==UpdateMode?"Windows-managed cleanup · Review sizes and risks":scanLocation.Text==VmMode?"Read-only virtual-machine inventory":scanLocation.Text==LeftoverMode?"Ownership evidence · Review caches · Recycle for recovery":scanLocation.Text==BrowserMode?"Cache-only cleanup · Close selected browsers first":scanLocation.Text==StreamsMode?"Read-only NTFS stream search · "+(streamRoot??"Choose a drive or share"):scanLocation.Text==TempMode?"Cleanup candidates · Nothing selected automatically":"Review only · Personal files are not marked safe to delete";if(scanLocation.Text=="Choose folder…")ChooseReviewFolder(); } };
        return row;
    }
    void RefreshScanLocations()
    {
        scanDrives.Clear();scanLocation.Items.Clear();scanLocation.SectionStarts.Clear();
        scanLocation.Items.AddRange([TempMode,BrowserMode,LeftoverMode,UpdateMode,RestoreMode]);
        scanLocation.SectionStarts[TempMode]="Cleanup";
        scanLocation.SectionStarts[UpdateMode]="Windows & recovery";
        scanLocation.SectionStarts["Downloads"]="Personal folders";
        scanLocation.SectionStarts[VmMode]="Advanced scans";
        var locations=new Dictionary<string,string> { ["Downloads"]=DownloadsFolder(),["Desktop"]=Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory) };
        if(chosenReviewFolder is not null)scanDrives["Chosen folder"]=chosenReviewFolder;
        foreach(var (title,path) in locations) { scanLocation.Items.Add(title);if(!string.IsNullOrWhiteSpace(path))scanDrives[title]=path; }
        scanLocation.Items.AddRange(["Choose folder…",VmMode,StreamsMode]);
        if(!scanLocation.Items.Contains(scanLocation.Text)&&!(scanLocation.Text=="Chosen folder"&&chosenReviewFolder!=null))scanLocation.SelectedItem=TempMode;
    }
    async Task ScanLocalDrive(string root)
    {
        reviewFocus.SelectedItem="All files";sortColumn=2;sortDescending=true;groupFolders.Checked=false;
        reviewedRoot=root;streamReview=false;driveReview=true;resultsMode=false;enableFileSelection.Checked=false;cleanupOutcomes.Clear();fileColumnsSized=false;scan=null;files.Items.Clear();files.CheckBoxes=false;
        SetBusy(true);cancel=new();status.Text=$"Scanning {root} for review. No files will be deleted.";
        TimeSpan? wait=maxWait.Checked?TimeSpan.FromSeconds((double)waitSeconds.Value):null;
        try
        {
            var progress=new Progress<DriveProgress>(p=>{status.Text=$"Scanning {root} · {p.Count:N0} entries · {p.Elapsed:mm\\:ss} elapsed · Cancel keeps partial results";scanSummary.Text="Reading: "+p.Folder;});
            scan=await Task.Run(()=>DriveCatalog.Read(root,cancel.Token,wait,progress));
            ShowScanMetrics();metricTitles[0].Text="FILES TO REVIEW";metricTitles[1].Text="TOTAL FILE SIZE";
            scanSummary.Text=$"{root} · Review only · {(scan.Cancelled?"Cancelled — partial results":scan.Limited?"Max wait reached — partial results":"Scan complete")}. These are personal files, not recoverable-space estimates.";
            details.Text=$"Showing up to 1,000 of {scan.Skipped.Count:N0} skipped entries.\r\n"+string.Join("\r\n",scan.Skipped.Take(1000).Select(s=>$"{s.Reason} | {s.Path}"));
            status.Text="Scan finished. Preparing pages for browsing…";
            using var journal=store.StartJournal();LocalStore.Append(journal,new { Kind="Personal folder",Location=root,Summary=$"Scan local drive {DateTimeOffset.Now:g}: {scan.Candidates.Count} files; {scan.Skipped.Count} skipped; cancelled={scan.Cancelled}; limited={scan.Limited}; read-only." });
        }
        finally {cancel.Dispose();cancel=null;SetBusy(false);RebuildFiles(false);RefreshHistory();}
    }
}
sealed record DriveProgress(int Count,TimeSpan Elapsed,string Folder);
static class DriveCatalog
{
    public static ScanResult Read(string root,CancellationToken token,TimeSpan? wait,IProgress<DriveProgress>? progress=null)
    {
        if(!MainWindow.LocalDriveType(new DriveInfo(Path.GetPathRoot(root)!).DriveType))throw new IOException("Only local drives are supported.");
        var good=new List<Candidate>();var skipped=new List<Finding>();var dirs=new Queue<string>();dirs.Enqueue(root);
        var clock=System.Diagnostics.Stopwatch.StartNew();bool limited=false;int count=0;long lastProgress=-250;
        while(dirs.Count>0 && !token.IsCancellationRequested && !limited)
        {
            if(wait.HasValue && clock.Elapsed>=wait.Value){limited=true;break;}
            var dir=dirs.Dequeue();
            if(clock.ElapsedMilliseconds-lastProgress>=250){progress?.Report(new(count,clock.Elapsed,dir));lastProgress=clock.ElapsedMilliseconds;}
            try
            {
                if((File.GetAttributes(dir)&FileAttributes.ReparsePoint)!=0){skipped.Add(new(dir,"Redirected folder omitted"));continue;}
                foreach(var path in Directory.EnumerateFileSystemEntries(dir))
                {
                    if(token.IsCancellationRequested)break;
                    if(wait.HasValue && clock.Elapsed>=wait.Value){limited=true;break;}
                    count++; if(clock.ElapsedMilliseconds-lastProgress>=250){progress?.Report(new(count,clock.Elapsed,dir));lastProgress=clock.ElapsedMilliseconds;}
                    try
                    {
                        var info=new FileInfo(path);var attr=info.Attributes;
                        if((attr&(FileAttributes.ReparsePoint|FileAttributes.Offline))!=0){skipped.Add(new(path,"Redirected or offline item omitted"));continue;}
                        if((attr&FileAttributes.Directory)!=0){dirs.Enqueue(path);continue;}
                        good.Add(PersonalReview.Snapshot(path));
                    }
                    catch(Exception e) when(e is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception){skipped.Add(new(path,"Unavailable or access denied"));}
                }
            }
            catch(Exception e) when(e is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception){skipped.Add(new(dir,"Unavailable or access denied"));}
        }
        return new(DateTimeOffset.Now,good,skipped,token.IsCancellationRequested,limited);
    }
}









