using Maintenance.Core;
using System.Text.Json;
namespace Maintenance.App;
sealed class InlineProgress<T>(Action<T> receive):IProgress<T>{public void Report(T value)=>receive(value);}
sealed partial class MainWindow
{
    async Task VerifyNewViews(string fixture)
    {
        ShowSettings();
        nameFilter.Clear();minSize.Value=maxSize.Value=0;hideEmpty.Checked=false;reviewFocus.SelectedItem="All files";filterDelay.Stop();
        string ads=Path.Combine(fixture,"sample-export.tmp")+":Zone.Identifier";
        File.WriteAllText(ads,"[ZoneTransfer]\r\nZoneId=3\r\nHostUrl=https://example.invalid/fixture\r\nReferrerUrl=https://example.invalid/");
        scanLocation.SelectedItem=StreamsMode;await ScanStreams(fixture);await renderTask;
        var stream=files.Items.Cast<ListViewItem>().First(i=>((Candidate)i.Tag!).Path==ads+":$DATA");stream.Selected=true;await ShowStreamInfo();
        if(!File.Exists("artifacts/stream-viewer.png"))throw new InvalidOperationException("Stream viewer did not render");
        SelectPage(1);
        scanLocation.SelectedItem=BrowserMode;Refresh();Capture("browser-cleanup");
        if(!browserSources.Visible||cleanupSources.Visible)throw new InvalidOperationException("Browser sources mode failed");
        string browserFixture=Path.Combine(fixture,"browser-profile");Directory.CreateDirectory(browserFixture);
        foreach(string path in BrowserCatalog.CacheRoots(browserFixture,false)){Directory.CreateDirectory(path);string sample=Path.Combine(path,"cache-entry");File.WriteAllText(sample,"cache fixture");File.SetCreationTimeUtc(sample,DateTime.UtcNow.AddDays(-10));File.SetLastWriteTimeUtc(sample,DateTime.UtcNow.AddDays(-10));}
        foreach(string kept in new[]{"Cookies","History","Login Data","Bookmarks","Preferences"})File.WriteAllText(Path.Combine(browserFixture,kept),"preserved fixture");
        var browserEngine=new MultiCleanupEngine(BrowserCatalog.CacheRoots(browserFixture,false));var found=browserEngine.Scan([],CancellationToken.None);
        if(found.Candidates.Count!=3||found.Candidates.Any(c=>Path.GetDirectoryName(c.Path)==browserFixture))throw new InvalidOperationException("Browser rules escaped cache locations");
        scanLocation.SelectedItem=RestoreMode;
        RenderRestore(new([new(2,"Windows Update — demonstration","2026-10-09T12:00:00Z",12),new(1,"Driver installation — demonstration","2026-10-01T12:00:00Z",10)],[new("C:",8UL*1024*1024*1024,10UL*1024*1024*1024,20UL*1024*1024*1024)],"Demonstration fixture · Shared storage is not a per-point reclaim estimate."));
        restoreList.Items[0].Checked=true;if(restoreList.Items[0].Checked)throw new InvalidOperationException("Newest restore-point checkbox not protected");
        Capture("restore-review");
        try{var actual=await RestoreService.Read();File.WriteAllText("artifacts/restore-read-check.json",JsonSerializer.Serialize(actual));}catch(Exception e){File.WriteAllText("artifacts/restore-read-check.json",JsonSerializer.Serialize(new{Unavailable=e.Message}));}
        scanLocation.SelectedItem=UpdateMode;foreach(var source in UpdateLocations()){var row=new ListViewItem(source.Name){Tag=source};row.SubItems.Add("Not measured");row.SubItems.Add("Review fixture");row.SubItems.Add(source.Risk);updateList.Items.Add(row);}updateList.Items[0].Selected=true;Capture("update-review");
        var measured=MeasureUpdateLocation(browserFixture,CancellationToken.None);if(!measured.Exists||measured.Bytes<=0||!measured.Coverage.StartsWith("Measured"))throw new InvalidOperationException("Update size measurement fixture failed");
        scanLocation.SelectedItem=VmMode;vmFiles=[new(@"F:\WSL\ext4.vhdx",671255363584,DateTime.Now,"Virtual disk","VHDX"),new(@"F:\VMWare\Example\snapshot.avhdx",1024*1024*1024,DateTime.Now,"Snapshot / differencing disk","AVHDX")];RenderVm();Capture("vm-review");
        if(StorageKinds.Personal(@"F:\WSL\ext4.vhdx")!=12)throw new InvalidOperationException("VM storage category missing");
        scanLocation.SelectedItem=LeftoverMode;leftoverSummary.Text="App-data review · no deletion performed\nOwnership is uncertain until supported by evidence; portable and per-user apps may still use these folders.";Capture("app-leftover-review");
        if(!DisposableFolderName("Cache")||DisposableFolderName("Settings")||DisposableFolderName("Databases"))throw new InvalidOperationException("App review folder boundary failed");
        scanLocation.SelectedItem=TempMode;
        File.WriteAllText("artifacts/revision-validation.txt","Settings, stream origin/hex viewer, browser cache-only boundary, restore selection/newest protection, Windows-managed update review, VM formats/category, app review folder boundaries rendered/checked. No actual restore points or user caches removed. Restore inventory result recorded separately.");
        void Capture(string name){Refresh();foreach(var panel in new[]{restorePanel,updatePanel,vmPanel,leftoverPanel})if(panel.Visible&&locationRow!=null&&panel.Top<locationRow.Bottom)throw new InvalidOperationException("Review panel covers scan-location selector");using var bitmap=new Bitmap(Width,Height);DrawToBitmap(bitmap,new Rectangle(0,0,Width,Height));bitmap.Save("artifacts/"+name+".png");}
    }
}
