using Maintenance.Core;
namespace Maintenance.App;
sealed partial class MainWindow
{
    const string StreamsMode="Alternate data streams (NTFS)";
    bool streamReview;
    string? streamRoot;
    Button? chooseScanTarget;
    async Task ChooseStreamDrive()
    {
        using var dialog=new Form{Text="Choose an NTFS drive or network share",Width=780,Height=480,StartPosition=FormStartPosition.CenterParent,MinimumSize=new(640,400)};
        var list=new ListBox{Dock=DockStyle.Fill};
        var note=new Label{Dock=DockStyle.Top,Height=65,Text="Local, USB and mapped NTFS drives. Network shares must report NTFS and stream support.\nElevated Windows sessions may not see your mapped letters; enter a UNC share below."};
        var path=new TextBox{Dock=DockStyle.Top,PlaceholderText=@"Or enter a network share: \\server\share",Text=streamRoot??""};
        var state=new Label{Dock=DockStyle.Bottom,Height=55,Text="Discovering drives…"};
        var use=new AppearanceButton{Dock=DockStyle.Bottom,Height=42,Text="Use selected drive / share"};
        dialog.Controls.Add(list);dialog.Controls.Add(path);dialog.Controls.Add(note);dialog.Controls.Add(state);dialog.Controls.Add(use);
        list.SelectedIndexChanged+=(_,_)=>{if(list.SelectedItem is StreamDrive d)path.Text=d.Root;};
        dialog.Shown+=async(_,_)=>{
            var drives=DriveInfo.GetDrives().Where(d=>d.DriveType is DriveType.Fixed or DriveType.Removable or DriveType.Network).ToArray();
            await Task.WhenAll(drives.Select(async d=>{
                var item=await Task.Run(()=>{try{return new StreamDrive(d.Name,StreamCatalog.IsNtfs(d.Name,out var reason)?$"{d.DriveType} · NTFS":reason);}catch(Exception e){return new StreamDrive(d.Name,e.Message);}});
                if(!dialog.IsDisposed)list.Items.Add(item);
            }));
            if(!dialog.IsDisposed)state.Text="Select a drive, or enter a UNC share. Only verified NTFS locations can be used.";
        };
        use.Click+=async(_,_)=>{
            use.Enabled=false;state.Text="Checking filesystem…";string target=path.Text.Trim();
            try {
                if(!Path.IsPathFullyQualified(target))throw new IOException("Enter a drive or a full UNC share path.");
                target=Path.GetPathRoot(Path.GetFullPath(target))!;
                var check=await Task.Run(()=>{bool ok=StreamCatalog.IsNtfs(target,out var reason);return(ok,reason);});
                if(dialog.IsDisposed)return;
                if(!check.ok){state.Text=check.reason;return;}
                streamRoot=target;ClearResults();scopeHint.Text="Read-only NTFS stream search · "+target;dialog.DialogResult=DialogResult.OK;
            }catch(Exception e){if(!dialog.IsDisposed)state.Text=e.Message;}
            finally{if(!dialog.IsDisposed)use.Enabled=true;}
        };
        PaintTheme(dialog,surface);dialog.ShowDialog(this);await Task.CompletedTask;
    }
    sealed record StreamDrive(string Root,string Detail){public override string ToString()=>Root+"  ·  "+Detail;}
    async Task ScanStreams(string root)
    {
        ClearResults();streamReview=driveReview=true;files.CheckBoxes=false;reviewFocus.SelectedItem="All files";
        sortColumn=2;sortDescending=true;fileColumnsSized=false;SetBusy(true);cancel=new();
        try {
            status.Text="Finding named streams · No files or streams will be changed.";
            var progress=new Progress<StreamProgress>(p=>{status.Text=$"{p.Entries:N0} entries checked · {p.Streams:N0} streams · {p.Elapsed:mm\\:ss} elapsed";scanSummary.Text=p.Folder;});
            TimeSpan? wait=maxWait.Checked?TimeSpan.FromSeconds((double)waitSeconds.Value):null;
            scan=await Task.Run(()=>StreamCatalog.Read(root,cancel.Token,wait,progress));ShowScanMetrics();
            metricTitles[0].Text="NAMED STREAMS";metricTitles[1].Text="STREAM DATA SIZE";
            scanSummary.Text=$"{root} · {(scan.Cancelled?"Cancelled — partial results":scan.Limited?"Max wait — partial results":"Enumeration finished")} · {scan.Skipped.Count:N0} skipped. Named streams can be normal metadata; this is not a malware verdict.";
            details.Text="File timestamps belong to the owning file. Network servers may limit stream enumeration.\r\n"+string.Join("\r\n",scan.Skipped.Take(1000).Select(s=>$"{s.Reason} | {s.Path}"));
            using var journal=store.StartJournal();LocalStore.Append(journal,new{Kind="Alternate data streams",Location=root,Summary=$"Read-only NTFS stream scan: {scan.Candidates.Count:N0} streams; {scan.Skipped.Count:N0} skipped; cancelled={scan.Cancelled}; limited={scan.Limited}."});
        }finally{cancel.Dispose();cancel=null;SetBusy(false);RebuildFiles(false);RefreshHistory();}
    }
}

