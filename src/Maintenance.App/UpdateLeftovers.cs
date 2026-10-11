using System.Diagnostics;
using Maintenance.Core;
namespace Maintenance.App;
sealed record UpdateLocation(string Name,string Path,string Meaning,string Risk);
sealed partial class MainWindow
{
    const string UpdateMode="Update & setup leftovers";
    readonly Panel updatePanel=new(){Dock=DockStyle.Fill,Visible=false};
    readonly ListView updateList=new(){Dock=DockStyle.Fill,View=View.Details,FullRowSelect=true,HideSelection=false};
    readonly Label updateSummary=new(){Dock=DockStyle.Top,Height=86,Padding=new(12),Font=new("Segoe UI",11,FontStyle.Bold),Text="Review Windows-managed storage\nFolder sizes are not a promise of safely reclaimable space."};
    readonly TextBox updateDetail=new(){Dock=DockStyle.Bottom,Height=150,ReadOnly=true,Multiline=true,ScrollBars=ScrollBars.Vertical};
    Control BuildUpdatePanel()
    {
        var actions=new FlowLayoutPanel{Dock=DockStyle.Top,AutoSize=true,MinimumSize=new(0,60),Padding=new(4,8,0,8)};
        actions.Controls.Add(StartSearch(ScanUpdateStorage));actions.Controls.Add(ScanCancel());
        actions.Controls.Add(Button("Windows temporary files…",()=>Process.Start(new ProcessStartInfo("ms-settings:storagesense"){UseShellExecute=true})));
        actions.Controls.Add(Button("Disk Cleanup…",()=>Process.Start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory,"cleanmgr.exe")){UseShellExecute=true})));
        actions.Controls.Add(Button("Windows Update…",()=>Process.Start(new ProcessStartInfo("ms-settings:windowsupdate"){UseShellExecute=true})));
        foreach(var (name,width) in new[]{("Location",310),("Measured size",150),("Coverage",260),("Purpose / risk",560)})updateList.Columns.Add(name,width);
        StyleReviewList(updateList);
        updateList.SelectedIndexChanged+=(_,_)=>{if(updateList.SelectedItems.Count==0)return;var source=(UpdateLocation)updateList.SelectedItems[0].Tag!;updateDetail.Text=source.Name+"\r\n"+source.Path+"\r\n\r\n"+source.Meaning+"\r\n"+source.Risk;};
        updateDetail.Text="Select a location to see its path and risks. Use Windows to decide what can be removed. No files are selected for deletion in this view.";
        updatePanel.Controls.Add(updateList);updatePanel.Controls.Add(updateSummary);updatePanel.Controls.Add(actions);updatePanel.Controls.Add(updateDetail);return updatePanel;
    }
    internal static UpdateLocation[] UpdateLocations()
    {
        string win=Environment.GetFolderPath(Environment.SpecialFolder.Windows),root=Path.GetPathRoot(win)!;
        return [
            new("Previous Windows installation",Path.Combine(root,"Windows.old"),"Files retained from a previous Windows installation.","Removing these can prevent going back to the previous Windows version. Check for personal files first; use Windows cleanup."),
            new("Windows upgrade staging",Path.Combine(root,"$WINDOWS.~BT"),"Upgrade staging, setup logs and possible rollback material.","May belong to an active, pending or reversible feature update. Do not remove manually."),
            new("Windows setup downloads",Path.Combine(root,"$WINDOWS.~WS"),"Files used by Windows Setup / installation media preparation.","Setup may still need these files. Windows decides whether they are removable."),
            new("Recovery update staging",Path.Combine(root,"$WinREAgent"),"Recovery-environment update / rollback staging.","May be needed to finish or roll back a recovery update. Do not remove manually."),
            new("Windows Update downloads",Path.Combine(win,"SoftwareDistribution","Download"),"Downloaded update payloads; not all are leftovers.","Staged updates may need these files. Removal can force downloads again or interrupt servicing; use Windows cleanup."),
            new("Delivery Optimization cache",Path.Combine(win,"ServiceProfiles","NetworkService","AppData","Local","Microsoft","Windows","DeliveryOptimization","Cache"),"Default cache location for downloaded / shared update content; Windows may use other locations.","Access may be restricted. Use Windows temporary-files cleanup rather than changing ownership or permissions."),
            new("Windows Installer cache",Path.Combine(win,"Installer"),"Installer packages and patches used to repair, update and uninstall applications.","Protected reference only — not a junk folder. Manual removal can break repair and uninstall. Not offered for cleanup."),
            new("Component store",Path.Combine(win,"WinSxS"),"Windows components and servicing versions. Logical size includes shared hard-linked files.","Reference only: measured size can overstate physical usage. Use Windows servicing tools; never delete this folder manually.")];
    }
    async Task ScanUpdateStorage()
    {
        if(busy)return;SetBusy(true);cancel=new();updateList.Items.Clear();updateSummary.Text="Measuring Windows-managed locations…\nCancel keeps completed measurements. Nothing will be removed.";
        int measured=0;
        try{
            foreach(var source in UpdateLocations()){
                if(cancel.IsCancellationRequested)break;
                var usage=await Task.Run(()=>MeasureUpdateLocation(source.Path,cancel.Token));
                var row=new ListViewItem(source.Name){Tag=source};row.SubItems.Add(!usage.Exists?"—":usage.Coverage.Contains("unknown")||usage.Coverage.Contains("omitted")?"Unknown":(usage.Coverage.StartsWith("Partial")?"≥ ":"")+FormatBytes(usage.Bytes));row.SubItems.Add(usage.Coverage);row.SubItems.Add(source.Risk);updateList.Items.Add(row);measured++;
            }
            updateSummary.Text=$"{measured} Windows-managed locations reviewed · {(cancel.IsCancellationRequested?"Cancelled — partial results":"Review complete")}\nLogical sizes can include shared files; only Windows can determine eligible cleanup.";
            using var journal=store.StartJournal();LocalStore.Append(journal,new{Kind="Update / installation storage",Location=Environment.GetFolderPath(Environment.SpecialFolder.Windows),Summary="Scan update storage: review only; "+string.Join("; ",updateList.Items.Cast<ListViewItem>().Select(i=>i.Text+" "+i.SubItems[1].Text+" ("+i.SubItems[2].Text+")"))});
        }finally{cancel.Dispose();cancel=null;SetBusy(false);RefreshHistory();}
    }
    internal static (bool Exists,long Bytes,string Coverage) MeasureUpdateLocation(string root,CancellationToken token)
    {
        long bytes=0;int skipped=0,count=0;var clock=Stopwatch.StartNew();var pending=new Queue<string>();pending.Enqueue(root);
        try{if((File.GetAttributes(root)&FileAttributes.ReparsePoint)!=0)return(true,0,"Redirected folder omitted");}catch(FileNotFoundException){return(false,0,"Not present");}catch(DirectoryNotFoundException){return(false,0,"Not present");}catch(UnauthorizedAccessException){return(true,0,"Access denied — unknown size");}catch(IOException){return(true,0,"Unavailable — unknown size");}
        while(pending.Count>0&&!token.IsCancellationRequested&&clock.Elapsed<TimeSpan.FromSeconds(30)){
            string folder=pending.Dequeue();try{foreach(var info in new DirectoryInfo(folder).EnumerateFileSystemInfos()){
                if(token.IsCancellationRequested||clock.Elapsed>=TimeSpan.FromSeconds(30))break;
                count++;try{if((info.Attributes&(FileAttributes.ReparsePoint|FileAttributes.Offline))!=0){skipped++;continue;}if(info is DirectoryInfo)pending.Enqueue(info.FullName);else bytes+=((FileInfo)info).Length;}catch(Exception e)when(e is IOException or UnauthorizedAccessException){skipped++;}
            }}catch(Exception e)when(e is IOException or UnauthorizedAccessException){skipped++;}
        }
        bool partial=token.IsCancellationRequested||clock.Elapsed>=TimeSpan.FromSeconds(30)||skipped>0;
        return(true,bytes,(partial?"Partial · ":"Measured · ")+count.ToString("N0")+" entries"+(skipped>0?$" · {skipped} skipped":""));
    }
}

