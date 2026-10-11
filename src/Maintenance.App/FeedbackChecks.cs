using Maintenance.Core;
namespace Maintenance.App;
sealed partial class MainWindow
{
    async Task VerifyFeedbackUi()
    {
        try {
            await VerifyAppDataMap();SetBusy(false);SelectPage(1);scanLocation.SelectedItem=LeftoverMode;leftoverTabs.SelectedIndex=3;
            var root=Directory.CreateDirectory(Path.GetFullPath(Path.Combine(".test-data","feedback-"+Guid.NewGuid().ToString("N")))).FullName;
            File.WriteAllText(Path.Combine(root,new string('L',100)+".log"),"generated cache fixture");
            var fixture=Path.Combine(root,"context-target.txt");File.WriteAllText(fixture,"context fixture");
            using(var contextGrid=new ResultGrid()) {
                contextGrid.Columns.Add("Path","Path");contextGrid.Rows.Add(fixture);contextGrid.CurrentCell=contextGrid.Rows[0].Cells[0];
                if(ResultLocations.Resolve(null,[fixture])!=fixture||!contextGrid.ContextMenuStrip!.Items.Cast<ToolStripItem>().Any(i=>i.Text=="Open item")||!contextGrid.ContextMenuStrip.Items.Cast<ToolStripItem>().Any(i=>i.Text=="Open folder"))throw new Exception("Result path context actions missing");
                var removed=Path.Combine(root,"removed.txt");if(ResultLocations.Resolve(null,[removed])!=removed)throw new Exception("Deleted result cannot resolve its remaining folder");
                if(ResultLocations.Resolve(null,["https://example.com"])!=null)throw new Exception("Web target treated as a file");
            }
            using(var start=StartSearch(()=>Task.CompletedTask))using(var stop=ScanCancel()) {
                StyleButton(start);StyleButton(stop);if(start.BackColor.G<=start.BackColor.R||stop.BackColor.R<=stop.BackColor.G)throw new Exception("Start/Cancel semantic colors missing");
                scanCancelButtons.Remove(stop);mutationButtons.Remove(start);
            }
            using(var cancelled=new CancellationTokenSource()) {
                cancelled.Cancel();try {await StartupTools.Run("Start-Sleep -Seconds 30",token:cancelled.Token);throw new Exception("Inventory query ignored cancellation");}catch(OperationCanceledException){}
            }
            int before=appDataGrid.Rows.Count;
            await OpenCacheReview(new("Fixture app",root,"Logs",22,"Measured","Fixture ownership evidence"),3);
            if(cacheBackButton?.Visible!=true||scanLocation.Text!="Chosen folder")throw new Exception("Cache back navigation missing");
            ReturnToAppCache();if(leftoverTabs.SelectedIndex!=3||appDataGrid.Rows.Count!=before)throw new Exception("Cache return lost originating results");
            appDataGrid.Capture=true;appDataGrid.Columns[0].Width=205;appDataGrid.Capture=false;((ResultGrid)appDataGrid).FitResults();
            if(appDataGrid.Columns[0].Width!=205)throw new Exception("User column width did not survive sizing");
            appDataGrid.ContextMenuStrip!.Items[0].PerformClick();
            RefreshScanLocations();if(scanLocation.Items.Count(i=>i=="Choose folder…")!=1||scanLocation.Items.Contains("Chosen folder")||scanLocation.Items.Count!=scanLocation.Items.Distinct().Count())throw new Exception("Folder menu has redundant actions");
            using var grid=new ResultGrid();grid.Columns.Add("Details","Details");grid.Rows.Add("Short");grid.FitResults();int shortWidth=grid.Columns[0].Width;
            grid.Rows.Clear();grid.Rows.Add(new string('W',100));grid.FitResults();if(grid.Columns[0].Width<=shortWidth)throw new Exception("Columns did not grow after new results");
            grid.Rows.Clear();grid.Rows.Add(string.Join(" ",Enumerable.Repeat("long explanatory phrase",40)));grid.FitResults();if(grid.Rows[0].Height<=38)throw new Exception("Long content did not wrap into expanded rows");
            scanLocation.SelectedItem=LeftoverMode;leftoverTabs.SelectedIndex=3;Size=new(1760,1120);themeChoice.SelectedItem="Dark";
            await Task.Delay(300);using var capture=new Bitmap(Width,Height);DrawToBitmap(capture,new Rectangle(0,0,Width,Height));capture.Save("artifacts/feedback-app-map.png");
            File.WriteAllText("artifacts/feedback-check-success.txt",DateTimeOffset.Now.ToString("o")+" · Cache review/back preserves origin, one folder chooser, content resizing and wrapping, live bounded evidence collection and fixture UI passed. No personal files removed.");
        }catch(Exception e){File.WriteAllText("artifacts/feedback-check-failure.txt",e.ToString());throw;}
    }
}
