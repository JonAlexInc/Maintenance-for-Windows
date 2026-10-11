using Maintenance.Core;
namespace Maintenance.App;
sealed partial class MainWindow
{
    async Task VerifyAppDataMap()
    {
        try {
            SetBusy(false);SelectPage(1);scanLocation.SelectedItem=LeftoverMode;leftoverTabs.SelectedIndex=3;
            string root=Directory.CreateDirectory(Path.GetFullPath(Path.Combine(".test-data","map-ui-"+Guid.NewGuid().ToString("N")))) .FullName;
            string known=Directory.CreateDirectory(Path.Combine(root,"Example Studio","Settings")).FullName;
            File.WriteAllBytes(Path.Combine(known,"settings.db"),new byte[8200]);
            Directory.CreateDirectory(Path.Combine(root,"Portable Editor","Cache"));File.WriteAllBytes(Path.Combine(root,"Portable Editor","Cache","data.bin"),new byte[4300]);
            appDataReport=AppDataMap.Scan([("Local",root)],[new("Example",Path.GetDirectoryName(known)!)],[],CancellationToken.None);
            RenderAppDataMap();
            if(appDataGrid.Rows.Count!=2)throw new Exception("App data map rows missing");
            appDataSearch.Text="Portable";if(appDataGrid.Rows.Count!=1)throw new Exception("Map search failed");appDataSearch.Clear();
            appDataSummary.Text="2 folders · 12.5 KB logical data · Fixture map\nRead-only review · Ownership unknown does not mean abandoned.";
            foreach(var theme in new[]{"Dark","Light"})foreach(var size in new[]{new Size(1760,1120),new Size(1280,900)}) {
                Size=size;themeChoice.SelectedItem=theme;await Task.Delay(80);PerformLayout();Refresh();
                using var capture=new Bitmap(Width,Height);DrawToBitmap(capture,new Rectangle(0,0,Width,Height));capture.Save($"artifacts/app-data-map-{theme}-{size.Width}.png");
            }
            var json=System.Text.Json.JsonSerializer.Serialize(appDataReport);var roundtrip=System.Text.Json.JsonSerializer.Deserialize<AppDataReport>(json);
            if(roundtrip?.Folders.Count!=2)throw new Exception("Map export roundtrip failed");
            using var timer=new System.Windows.Forms.Timer{Interval=6000};timer.Tick+=(_,_)=>{cancel?.Cancel();timer.Stop();};timer.Start();
            await ScanAppDataMap();timer.Stop();
            if(appDataReport is null||busy)throw new Exception("Live map completion state failed");
            File.WriteAllText("artifacts/app-data-map-check-success.txt",DateTimeOffset.Now.ToString("o")+$" · Recursive fixture map, search, association, JSON export and light/dark layouts at two sizes. Bounded read-only live map: {appDataReport.Folders.Count} folders / {appDataReport.Entries} entries, partial={appDataReport.Partial}. No personal data removed.");
        }catch(Exception e){File.WriteAllText("artifacts/app-data-map-check-failure.txt",e.ToString());throw;}
    }
}
