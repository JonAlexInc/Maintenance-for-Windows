using Maintenance.Core;
using System.Diagnostics;
using System.Text.Json;

namespace Maintenance.App;
sealed partial class MainWindow
{
    readonly DataGridView appDataGrid = HistoryGrid();
    readonly TextBox appDataSearch = new() { Width = 270, PlaceholderText = "Find app, folder, or evidence" };
    readonly Label appDataSummary = new() { Dock = DockStyle.Top, Height = 94, Padding = new(12), Text = "Where applications keep their data\nScan Local, Roaming and LocalLow for this account. Sizes describe storage, not safely removable space." };
    readonly TextBox appDataDetails = new() { Dock = DockStyle.Bottom, Height = 150, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Text = "Select a folder to see its full path, association evidence and scan coverage. This map does not remove files." };
    AppDataReport? appDataReport;

    void BuildAppDataMap()
    {
        var page = new Panel { Text = "App data map", Dock = DockStyle.Fill };
        var actions = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new(4,8,4,8) };
        actions.Controls.Add(StartSearch(ScanAppDataMap)); actions.Controls.Add(ScanCancel());
        
        appDataView.Items.AddRange(["All app data","Likely leftovers","App present / referenced","Needs review"]);appDataView.SelectedItem="All app data";appDataView.SelectedIndexChanged+=(_,_)=>RenderAppDataMap();var filters=ResultFilters(appDataSearch,appDataView);actions.Controls.Add(Button("Review cache folders…",ReviewMappedCache)); actions.Controls.Add(Button("Export map…", ExportAppDataMap));
        actions.Controls.Add(Button("Open folder…", () => {
            if (appDataGrid.SelectedRows.Count > 0 && appDataGrid.SelectedRows[0].Tag is AppDataFolder item && Directory.Exists(item.Path))
                Process.Start(new ProcessStartInfo("explorer.exe") { UseShellExecute = true, Arguments = "\"" + item.Path + "\"" });
        }));
        foreach (var (name,width) in new[] { ("Folder / publisher",230),("Location",110),("Measured size",145),("Files",95),("Association",210),("Coverage",190),("Full path",520) })
            appDataGrid.Columns.Add(new DataGridViewTextBoxColumn { Name=name, Width=width, SortMode=DataGridViewColumnSortMode.Automatic });
        appDataGrid.CellFormatting += (_,e) => { if(e.ColumnIndex==2 && e.Value is long bytes) { e.Value=FormatBytes(bytes); e.FormattingApplied=true; } };
        appDataSearch.TextChanged += (_,_) => RenderAppDataMap();
        void ShowMapSelection() {
            if(appDataGrid.SelectedRows.Count==0 || appDataGrid.SelectedRows[0].Tag is not AppDataFolder item) return;
            ShowAppDataVisual(item);var assessment=remnantAssessments.GetValueOrDefault(item.Path);
            appDataDetails.Text = item.Path + "\r\n" + (assessment==null?item.Evidence:assessment.State+"\r\n"+assessment.Explanation+"\r\n"+assessment.Source) + "\r\n\r\n" +
                $"{item.Files:N0} measured files · {item.Skipped:N0} inaccessible, linked or cloud-only entries omitted. " +
                (item.Partial ? "Measurement incomplete; size is a lower bound. " : "Folder enumeration completed. ") +
                "Logical file sizes may count hard links more than once; they are not allocated disk space.\r\nMay contain settings, sign-in data, saves, databases and installed binaries. No deletion is offered here.";
        };
        appDataGrid.SelectionChanged+=(_,_)=>ShowMapSelection();appDataGrid.CellClick+=(_,_)=>ShowMapSelection();
        page.Controls.Add(appDataGrid); page.Controls.Add(appDataDetails); page.Controls.Add(appDataVisual);page.Controls.Add(appDataSummary); page.Controls.Add(filters); page.Controls.Add(actions);
        leftoverTabs.TabPages.Add(page); leftoverTabs.Controls.Add(page);
    }

    void RenderAppDataMap()
    {
        appDataGrid.Rows.Clear(); if(appDataReport is null)return;
        string query=appDataSearch.Text.Trim();
        foreach(var item in appDataReport.Folders.Where(i => (i.Name+" "+i.Scope+" "+i.Path+" "+i.Association+" "+i.Evidence+" "+remnantAssessments.GetValueOrDefault(i.Path)?.Application+" "+remnantAssessments.GetValueOrDefault(i.Path)?.State).Contains(query,StringComparison.OrdinalIgnoreCase))) {
            var assessment=remnantAssessments.GetValueOrDefault(item.Path);string state=assessment?.State??item.Association;
            if(appDataView.Text=="Likely leftovers"&&state!="Likely leftovers")continue;
            if(appDataView.Text=="App present / referenced"&&!state.Contains("present",StringComparison.OrdinalIgnoreCase))continue;
            if(appDataView.Text=="Needs review"&&(state.Contains("present",StringComparison.OrdinalIgnoreCase)||state=="Likely leftovers"))continue;
            int row=appDataGrid.Rows.Add(assessment?.Application??item.Name,item.Scope,item.Bytes,item.Files,state,item.Partial?$"Partial · {item.Skipped:N0} omitted":"Measured",item.Path);
            appDataGrid.Rows[row].Tag=item;
        }
    }

    async Task ScanAppDataMap()
    {
        if(busy)return; SetBusy(true); cancel=new(); appDataReport=null; appDataGrid.Rows.Clear();
        appDataSummary.Text="Mapping application data…\nReading folder sizes and association evidence. Cancel retains completed measurements.";
        try {
            var token=cancel.Token;
            var apps=await Task.Run(InstalledApps);
            var processPaths=await Task.Run(() => {
                var paths=new List<string>(); int unavailable=0;
                foreach(var process in Process.GetProcesses()) using(process) { try { string? path=process.MainModule?.FileName; if(path!=null)paths.Add(path); else unavailable++; } catch { unavailable++; } }
                return (paths,unavailable);
            });
            status.Text="Checking Store packages, registered executables, services and scheduled tasks…";
            try{remnantInventory=await CollectRemnantInventory(token);}catch(Exception e){remnantInventory=new([],[],[],false,[e.Message]);}
            remnantPresence=new(apps.Select(a=>a.Name).ToArray(),remnantInventory.Packages,processPaths.paths.Concat(remnantInventory.Paths).ToArray(),remnantInventory.Commands,remnantInventory.Complete);
            string local=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var roots=new[] { ("Roaming",Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)),("LocalLow",Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"AppData","LocalLow")),("Local",local) };
            var progress=new Progress<string>(message=>status.Text=message);
            appDataReport=await Task.Run(()=>AppDataMap.Scan(roots,apps.Select(a=>new AppDataEvidence(a.Name,a.Location)).ToArray(),processPaths.paths,token,progress));
            AssessAppData();RenderAppDataMap();ShowAppDataVisual();
            appDataSummary.Text=$"{appDataReport.Folders.Count:N0} folders · {FormatBytes(appDataReport.Folders.Sum(f=>f.Bytes))} logical data · {appDataReport.Entries:N0} entries\n"+
                (appDataReport.Partial?"Partial map":"Map complete")+$" · {appDataReport.Folders.Sum(f=>f.Skipped):N0} omitted entries · {processPaths.unavailable} process paths unavailable. Ownership unknown does not mean abandoned.";
            appDataDetails.Text="Current account only. Up to 25,000 entries per folder and 250,000 per scan; partial rows can hit those bounds or cancellation. Top-level folders are shown separately by location. Shared publisher folders and Store Packages are not attributed to one application. First-run rules: Visual Studio Code, Discord families and Notepad++. Other apps retain conservative path/name evidence. Process evidence is a point-in-time snapshot; inaccessible processes and absent registrations cannot prove non-use.\r\n"+string.Join("\r\n",appDataReport.Issues.Concat(remnantInventory.Notes));
            status.Text=appDataReport.Partial?"App data map stopped with partial results. Review coverage before drawing conclusions.":"App data map complete. Select a folder for evidence and coverage.";
            using var journal=store.StartJournal(); LocalStore.Append(journal,new{Kind="App data map",Summary=$"Read-only map: {appDataReport.Folders.Count} folders, {appDataReport.Entries} entries; partial={appDataReport.Partial}. No files removed."});
        } catch(Exception e) { if(smoke)throw;appDataReport=null;appDataSummary.Text="App data map could not finish: "+e.Message; status.Text="App data map could not finish."; }
        finally { cancel.Dispose();cancel=null;SetBusy(false);RefreshHistory(); }
    }

    void ExportAppDataMap()
    {
        if(appDataReport is null)return;
        using var dialog=new SaveFileDialog { Filter="JSON report|*.json", FileName="Maintenance-app-data-map.json", Title="Export app data map (includes full paths)" };
        if(dialog.ShowDialog(this)==DialogResult.OK)File.WriteAllText(dialog.FileName,JsonSerializer.Serialize(new{Map=appDataReport,Assessments=remnantAssessments,InventoryCoverage=remnantInventory.Notes},new JsonSerializerOptions{WriteIndented=true}));
    }
}








