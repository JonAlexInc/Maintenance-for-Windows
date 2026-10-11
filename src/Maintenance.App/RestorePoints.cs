using Maintenance.Core;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
namespace Maintenance.App;

sealed record ShadowUsage(string Drive,ulong Used,ulong Allocated,ulong Maximum);
sealed record RestoreSnapshot(RestoreEntry[] Points,ShadowUsage[] Storage,string StorageNote);
static class RestoreService
{
    internal static async Task<RestoreSnapshot> Read(CancellationToken token=default)
    {
        using var operation=AppOperations.Begin(token);token=operation.Token;token.ThrowIfCancellationRequested();
        const string script="""
        $ErrorActionPreference='Stop'
        $ProgressPreference='SilentlyContinue'
        [Console]::OutputEncoding=[Text.UTF8Encoding]::new($false)
        try {
          $points=@(Get-CimInstance -Namespace root/default -ClassName SystemRestore | ForEach-Object {
            $created=$_.CreationTime
            if($created -is [DateTime]){$created=$created.ToUniversalTime().ToString('o')}
            [ordered]@{Id=[uint32]$_.SequenceNumber;Description=[string]$_.Description;Created=[string]$created;Type=[int]$_.RestorePointType}
          })
          $storage=@();$note='Shared VSS storage can also contain snapshots used by backup tools. It is not a per-restore-point reclaim estimate.'
          try {
            $volumes=@{};Get-CimInstance Win32_Volume | ForEach-Object {$volumes[$_.DeviceID]=$_.DriveLetter}
            $storage=@(Get-CimInstance Win32_ShadowStorage | ForEach-Object {
              $id=$_.Volume.DeviceID;$drive=$volumes[$id];if(!$drive){$drive=$id}
              [ordered]@{Drive=[string]$drive;Used=[uint64]$_.UsedSpace;Allocated=[uint64]$_.AllocatedSpace;Maximum=[uint64]$_.MaxSpace}
            })
          }catch{$note='Shadow-storage usage unavailable: '+$_.Exception.Message}
          [ordered]@{Points=$points;Storage=$storage;StorageNote=$note}|ConvertTo-Json -Depth 5 -Compress
        }catch{[Console]::Error.WriteLine($_.Exception.Message);exit 1}
        """;
        var info=new ProcessStartInfo(Path.Combine(Environment.SystemDirectory,@"WindowsPowerShell\v1.0\powershell.exe")){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8};
        info.ArgumentList.Add("-NoProfile");info.ArgumentList.Add("-NonInteractive");info.ArgumentList.Add("-EncodedCommand");info.ArgumentList.Add(Convert.ToBase64String(Encoding.Unicode.GetBytes(script)));
        using var process=Process.Start(info)??throw new IOException("Cannot query Windows restore points");
        var output=process.StandardOutput.ReadToEndAsync();var error=process.StandardError.ReadToEndAsync();
        using var limit=new CancellationTokenSource(TimeSpan.FromSeconds(40));
        using var linked=CancellationTokenSource.CreateLinkedTokenSource(limit.Token,token);try{await process.WaitForExitAsync(linked.Token);}catch(OperationCanceledException){if(!process.HasExited)process.Kill(true);if(token.IsCancellationRequested)throw new OperationCanceledException(token);throw new TimeoutException("Windows restore-point query timed out; no restore points were removed.");}
        string json=await output,reason=await error;if(process.ExitCode!=0)throw new IOException("Restore-point inventory unavailable. "+reason.Trim());
        var snapshot=JsonSerializer.Deserialize<RestoreSnapshot>(json)??throw new IOException("Windows returned an unreadable restore-point inventory");
        return snapshot with {Points=snapshot.Points.Select(p=>p with {Created=RestoreReview.NormalizeDate(p.Created)}).ToArray()};
    }
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]delegate uint RemovePoint(uint id);
    internal static uint Remove(uint id)
    {
        // Microsoft requires runtime loading for System Restore APIs. Load only the system DLL.
        nint library=NativeLibrary.Load(Path.Combine(Environment.SystemDirectory,"srclient.dll"));
        try{return Marshal.GetDelegateForFunctionPointer<RemovePoint>(NativeLibrary.GetExport(library,"SRRemoveRestorePoint"))(id);}finally{NativeLibrary.Free(library);}
    }
}
sealed partial class MainWindow
{
    const string RestoreMode="System Restore";
    readonly Panel restorePanel=new(){Dock=DockStyle.Fill,Visible=false};
    readonly ListView restoreList=new(){Dock=DockStyle.Fill,View=View.Details,CheckBoxes=true,FullRowSelect=true,HideSelection=false};
    readonly Label restoreSummary=new(){Dock=DockStyle.Top,Height=105,Padding=new(14),Font=new("Segoe UI",11,FontStyle.Bold)};
    readonly Label restoreNote=new(){Dock=DockStyle.Bottom,Height=90,Padding=new(12)};
    readonly TextBox restoreDetail=new(){Dock=DockStyle.Bottom,Height=105,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical};
    Button? removeRestore;RestoreSnapshot? restoreSnapshot;bool restoreBusy;
    Control[] normalCleanup=[];Control? locationRow;
    void BuildRestorePanel(Control body,Control location)
    {
        locationRow=location;normalCleanup=body.Controls.Cast<Control>().Where(c=>c!=location).ToArray();
        var actions=new FlowLayoutPanel{Dock=DockStyle.Top,Height=62,Padding=new(4,10,4,4)};
        actions.Controls.Add(StartSearch(ScanRestore));actions.Controls.Add(ScanCancel());removeRestore=Button("Remove selected restore points…",CleanRestore);removeRestore.Enabled=false;actions.Controls.Add(removeRestore);
        actions.Controls.Add(Button("System protection…",()=>Process.Start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory,"SystemPropertiesProtection.exe")){UseShellExecute=true})));
        foreach(var (name,width) in new[]{("Created",205),("Description",420),("Reason",205),("Restore point",150),("Review / result",390)})restoreList.Columns.Add(name,width);
        StyleReviewList(restoreList);
        restoreList.ItemCheck+=(_,e)=>{if(restoreBusy||restoreSnapshot==null)return;if(e.NewValue==CheckState.Checked&&RestoreReview.Refusal((RestoreEntry)restoreList.Items[e.Index].Tag!,restoreSnapshot.Points)!=null)e.NewValue=CheckState.Unchecked;BeginInvoke(new Action(UpdateRestoreSelection));};
        restoreList.SelectedIndexChanged+=(_,_)=>{if(restoreList.SelectedItems.Count>0){var entry=(RestoreEntry)restoreList.SelectedItems[0].Tag!;restoreDetail.Text=$"{entry.Description}\r\nCreated: {RestoreDate(entry.Created)} · ID: {entry.Id} · {RestoreReview.TypeName(entry.Type)}\r\nWindows does not report a reliable individual size. Removing this point permanently removes that recovery option.";}};
        restoreSummary.Text="Review your recovery options\nScan to list Windows restore points. Nothing is selected automatically.";
        restoreNote.Text="Newest restore point is protected. Removal is permanent and does not go to the Recycle Bin.\nShared shadow storage is shown separately; other backup snapshots are not targeted.";
        restorePanel.Controls.Add(restoreList);restorePanel.Controls.Add(restoreSummary);restorePanel.Controls.Add(actions);restorePanel.Controls.Add(restoreDetail);restorePanel.Controls.Add(restoreNote);
        body.Controls.Add(restorePanel);body.Controls.Add(BuildUpdatePanel());body.Controls.Add(BuildVmPanel());body.Controls.Add(BuildLeftoverPanel());restorePanel.BringToFront();
    }
    void StyleReviewList(ListView list)
    {
        list.OwnerDraw=true;list.DrawColumnHeader+=(_,e)=>{using var brush=new SolidBrush(surface);e.Graphics.FillRectangle(brush,e.Bounds);TextRenderer.DrawText(e.Graphics,e.Header?.Text??"",Font,e.Bounds,ink,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);};
        list.DrawItem+=(_,e)=>e.DrawDefault=false;list.DrawSubItem+=DrawThemedListRow;
    }
    void SetCleanupMode()
    {
        bool restore=scanLocation.Text==RestoreMode,update=scanLocation.Text==UpdateMode,vm=scanLocation.Text==VmMode,leftovers=scanLocation.Text==LeftoverMode,special=restore||update||vm||leftovers;
        if(special){foreach(var c in normalCleanup)c.Visible=false;}
        else if(restorePanel.Visible||updatePanel.Visible||vmPanel.Visible||leftoverPanel.Visible){foreach(var c in normalCleanup)c.Visible=true;details.Visible=false;}
        restorePanel.Visible=restore;updatePanel.Visible=update;vmPanel.Visible=vm;leftoverPanel.Visible=leftovers;
        if(special)(restore?restorePanel:update?updatePanel:vm?vmPanel:leftoverPanel).BringToFront();
        cleanupSources.Visible=!special&&scanLocation.Text==TempMode;browserSources.Visible=!special&&scanLocation.Text==BrowserMode;
        UpdateFilterPresentation();
        UpdateActions();
    }
    static string RestoreDate(string date)=>DateTimeOffset.TryParse(date,out var parsed)?parsed.ToLocalTime().ToString("g"):date;
    void UpdateRestoreSelection(){if(removeRestore!=null)removeRestore.Enabled=!busy&&!restoreBusy&&restoreSnapshot!=null&&IsAdmin&&restoreList.CheckedItems.Count>0;}
    void RenderRestore(RestoreSnapshot snapshot)
    {
        restoreSnapshot=snapshot;restoreList.Items.Clear();
        foreach(var entry in snapshot.Points.OrderByDescending(p=>p.Created,StringComparer.Ordinal).ThenByDescending(p=>p.Id)){
            var item=new ListViewItem(RestoreDate(entry.Created)){Tag=entry};item.SubItems.Add(entry.Description);item.SubItems.Add(RestoreReview.TypeName(entry.Type));item.SubItems.Add(entry.Id.ToString());item.SubItems.Add(RestoreReview.Refusal(entry,snapshot.Points)??"Available for review");restoreList.Items.Add(item);
        }
        restoreSummary.Text=$"{snapshot.Points.Length:N0} restore points · {(IsAdmin?"Administrator":"Run as administrator to remove points")}\n"+(snapshot.Points.Length>0?"Newest: "+RestoreDate(snapshot.Points.MaxBy(p=>p.Created)!.Created):"Windows reported no restore points. System Protection may be disabled.");
        restoreDetail.Text=snapshot.Storage.Length==0?"No shadow-storage usage was reported.":string.Join("\r\n",snapshot.Storage.Select(s=>$"{s.Drive} · Shared shadow storage: {FormatBytes((long)Math.Min(s.Used,(ulong)long.MaxValue))} used · {FormatBytes((long)Math.Min(s.Allocated,(ulong)long.MaxValue))} allocated · Limit: {(s.Maximum==ulong.MaxValue?"Unbounded":FormatBytes((long)Math.Min(s.Maximum,(ulong)long.MaxValue)))}"));
        restoreNote.Text="Newest restore point is protected. Removal is permanent; nothing is selected automatically.\n"+snapshot.StorageNote;UpdateRestoreSelection();
    }
    async Task ScanRestore()
    {
        if(restoreBusy||busy)return;restoreBusy=true;SetBusy(true);cancel=new();restoreList.Enabled=false;restoreSummary.Text="Reading Windows restore points…";
        try{var snapshot=await RestoreService.Read(cancel.Token);RenderRestore(snapshot);using var journal=store.StartJournal();LocalStore.Append(journal,new{Kind="System Restore",Location="This computer",Summary=$"Scan System Restore: {snapshot.Points.Length} restore points; review only."});}
        catch(OperationCanceledException){restoreSummary.Text="Search cancelled · Previously loaded restore points remain visible. Windows returns this inventory as a single response.";}catch(Exception e){restoreSummary.Text=e.Message;}
        finally{restoreBusy=false;cancel?.Dispose();cancel=null;SetBusy(false);restoreList.Enabled=true;UpdateRestoreSelection();RefreshHistory();}
    }
    async Task CleanRestore()
    {
        if(smoke||restoreBusy||!IsAdmin||restoreSnapshot==null)return;
        var selected=restoreList.CheckedItems.Cast<ListViewItem>().Select(i=>(RestoreEntry)i.Tag!).ToArray();if(selected.Length==0)return;
        if(MessageBox.Show(this,$"Permanently remove {selected.Length} selected restore points?\n\n"+string.Join("\n",selected.Take(8).Select(p=>RestoreDate(p.Created)+" · "+p.Description))+(selected.Length>8?"\n…and additional selected points":"")+"\n\nYou will lose the ability to restore Windows to these checkpoints. This cannot be undone. The newest point is kept. Freed space is not guaranteed.","Remove recovery options",MessageBoxButtons.OKCancel,MessageBoxIcon.Warning,MessageBoxDefaultButton.Button2)!=DialogResult.OK)return;
        restoreBusy=true;SetBusy(true);cancel=new();restoreList.Enabled=false;int deleted=0;
        try{
            using var journal=store.StartJournal();LocalStore.Append(journal,new{Summary=$"System Restore cleanup: {selected.Length} requested."});
            foreach(var entry in selected){
                cancel.Token.ThrowIfCancellationRequested();string path=$"System Restore #{entry.Id} · {entry.Description}";var current=await RestoreService.Read(cancel.Token);string? refusal=RestoreReview.Refusal(entry,current.Points);string state="Skipped",reason=refusal??"";
                if(refusal==null){
                    cancel.Token.ThrowIfCancellationRequested();
                    LocalStore.Append(journal,new{Path=path,Status="Pending",Reason="Restore-point removal requested; size unavailable"});
                    uint code=await Task.Run(()=>RestoreService.Remove(entry.Id));
                    if(code!=0)reason=new System.ComponentModel.Win32Exception((int)code).Message;
                    else {var verified=await RestoreService.Read(cancel.Token);if(verified.Points.Any(p=>p.Id==entry.Id)){state="Unknown";reason="Windows accepted removal, but the point is still listed. Rescan to verify.";}else{state="Deleted";reason="Restore-point API succeeded; absence verified. Reclaimed size unavailable.";deleted++;}}
                }
                LocalStore.Append(journal,new{Path=path,Status=state,Reason=reason});
                var row=restoreList.Items.Cast<ListViewItem>().First(i=>((RestoreEntry)i.Tag!).Id==entry.Id);row.SubItems[4].Text=state+" · "+reason;row.Checked=false;
            }
            restoreSummary.Text=$"{deleted} restore points removed · {selected.Length-deleted} skipped or unconfirmed\nResults remain below. Scan again for the current recovery inventory.";restoreSnapshot=null;
        }catch(Exception e){restoreSnapshot=null;restoreSummary.Text="Removal stopped: "+e.Message+" Review History; unfinished outcomes are unknown.";}
        finally{restoreBusy=false;cancel?.Dispose();cancel=null;SetBusy(false);restoreList.Enabled=true;removeRestore!.Enabled=false;RefreshHistory();}
    }
}


