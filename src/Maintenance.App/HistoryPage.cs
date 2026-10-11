using Maintenance.Core;
using System.Globalization;
using System.Text;
namespace Maintenance.App;
sealed partial class MainWindow
{
    readonly HardwarePages historyTabs=new(){Dock=DockStyle.Fill};
    readonly TextBox historyFind=new(){Width=360,PlaceholderText="Find file, folder, scan, or reason"};
    readonly Label historyTotals=new(){Dock=DockStyle.Top,Height=88,Padding=new(8),Font=new("Segoe UI",10,FontStyle.Bold)};
    readonly Label historyCoverage=new(){Dock=DockStyle.Bottom,Height=42,Padding=new(8),Font=new("Segoe UI",8)};
    readonly DataGridView scanHistoryGrid=HistoryGrid(),removedHistoryGrid=HistoryGrid(),otherHistoryGrid=HistoryGrid();
    readonly TextBox historyExact=new(){Dock=DockStyle.Bottom,Height=78,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical};
    HistoryCatalog historyCatalog=new([],[],0);
    readonly Dictionary<string,string> historyJournalSearch=new(StringComparer.OrdinalIgnoreCase);
    static DataGridView HistoryGrid()=>new ResultGrid();
    void BuildHistoryTabs(Control body,Control activityList,Control activityActions)
    {
        var all=new Panel{Text="All activity",Dock=DockStyle.Fill};all.Controls.Add(activityList);all.Controls.Add(activityActions);historyTabs.TabPages.Add(all);historyTabs.Controls.Add(all);
        foreach(var (title,grid) in new[]{("Scans",scanHistoryGrid),("Deleted & recycled",removedHistoryGrid),("Skipped & incomplete",otherHistoryGrid)}) {
            var tab=new Panel{Text=title,Dock=DockStyle.Fill};tab.Controls.Add(grid);historyTabs.TabPages.Add(tab);historyTabs.Controls.Add(tab);
            string[] columns=grid==scanHistoryGrid?["When","Scan type","Location","Summary"]:["When","Outcome","File name","Size","Full path","Details"];
            foreach(string column in columns)grid.Columns.Add(new DataGridViewTextBoxColumn{Name=column,HeaderText=column,Width=column is "Full path" or "Location"?420:column is "Summary" or "Details"?520:column=="When"?190:column=="File name"?250:140,SortMode=DataGridViewColumnSortMode.Automatic});
            grid.CellFormatting+=(_,e)=>{
                if(e.Value is DateTimeOffset at){e.Value=at==DateTimeOffset.MinValue?"Not recorded":at.LocalDateTime.ToString("g");e.FormattingApplied=true;}
                if(grid.Columns[e.ColumnIndex].Name=="Size"){e.Value=e.Value is long bytes?FormatBytes(bytes):"Not recorded";e.FormattingApplied=true;}
            };
            grid.SelectionChanged+=(_,_)=>{
                if(grid.SelectedRows.Count==0)return;
                historyExact.Text=grid.SelectedRows[0].Tag switch {HistoryFile f=>$"{f.Path}\r\n{f.Status} · {(f.Size.HasValue?f.Size.Value.ToString("N0")+" bytes":"Size not recorded")} · {f.At.LocalDateTime:g}\r\n{f.Reason}",HistoryScan s=>$"{s.Location}\r\n{s.Summary}",SpeedHistoryEntry r=>$"{r.Target}\r\n{r.Mode} · {r.Workload} · {r.Outcome}\r\n{r.Detail}",_=>""};
            };
        }
        BuildSpeedHistoryTab();
        var tools=new FlowLayoutPanel{Dock=DockStyle.Top,Height=54,Padding=new(0,6,0,0)};
        var filters=ResultFilters(historyFind,Button("Clear filter",()=>historyFind.Clear()));tools.Controls.Add(Button("Refresh history",RefreshHistory));tools.Controls.Add(Button("Export this tab…",ExportHistoryTab));
        historyFind.TextChanged+=(_,_)=>RenderHistoryDetails();historyTabs.SelectedIndexChanged+=(_,_)=>{
            historyExact.Clear();historyExact.Visible=historyTabs.SelectedIndex!=0;
            var grid=historyTabs.SelectedIndex==1?scanHistoryGrid:historyTabs.SelectedIndex==2?removedHistoryGrid:historyTabs.SelectedIndex==4?speedHistoryGrid:otherHistoryGrid;
            if(historyTabs.SelectedIndex!=0&&grid.SelectedRows.Count>0)historyExact.Text=grid.SelectedRows[0].Tag switch {HistoryFile f=>$"{f.Path}\r\n{f.Status} · {(f.Size.HasValue?f.Size.Value.ToString("N0")+" bytes":"Size not recorded")}\r\n{f.Reason}",HistoryScan s=>$"{s.Location}\r\n{s.Summary}",SpeedHistoryEntry r=>$"{r.Target}\r\n{r.Mode} · {r.Workload} · {r.Outcome}\r\n{r.Detail}",_=>""};
        };
        var nav=new FlowLayoutPanel{Dock=DockStyle.Top,AutoSize=true,Padding=new(0,4,0,10)};var buttons=new List<Button>();
        for(int i=0;i<historyTabs.TabPages.Count;i++){int index=i;var button=Button(historyTabs.TabPages[i].Text,()=>historyTabs.SelectedIndex=index);buttons.Add(button);nav.Controls.Add(button);}
        historyTabs.SelectedIndexChanged+=(_,_)=>{for(int i=0;i<buttons.Count;i++){buttons[i].Tag=i==historyTabs.SelectedIndex?PrimaryRole:null;StyleButton(buttons[i]);}};
        historyTabs.SelectedIndex=0;historyExact.Visible=false;
        body.Controls.Add(historyTabs);body.Controls.Add(historyExact);body.Controls.Add(historyCoverage);body.Controls.Add(nav);body.Controls.Add(historyVisual);body.Controls.Add(historyTotals);body.Controls.Add(filters);body.Controls.Add(tools);
    }
    void RefreshHistoryDetails()
    {
        ReadSpeedHistory();historyCatalog=HistoryCatalog.Read(store.History());historyJournalSearch.Clear();
        foreach(string path in store.History())try {
            var text=new StringBuilder();
            void Append(System.Text.Json.JsonElement value){switch(value.ValueKind){case System.Text.Json.JsonValueKind.Object:foreach(var property in value.EnumerateObject())Append(property.Value);break;case System.Text.Json.JsonValueKind.Array:foreach(var item in value.EnumerateArray())Append(item);break;case System.Text.Json.JsonValueKind.String:text.Append(value.GetString()).Append(' ');break;case System.Text.Json.JsonValueKind.Number:text.Append(value.GetRawText()).Append(' ');break;}}
            foreach(string line in File.ReadLines(path))try{using var record=System.Text.Json.JsonDocument.Parse(line);Append(record.RootElement);}catch(System.Text.Json.JsonException){}
            historyJournalSearch[path]=text.ToString();
        }catch(Exception e)when(e is IOException or UnauthorizedAccessException){}
        RenderHistoryDetails();
    }
    void RenderHistoryDetails()
    {
        string search=historyFind.Text.Trim();RenderSpeedHistory(search);
        bool Matches(params string[] values)=>search.Length==0||values.Any(v=>v.Contains(search,StringComparison.OrdinalIgnoreCase));
        scanHistoryGrid.Rows.Clear();removedHistoryGrid.Rows.Clear();otherHistoryGrid.Rows.Clear();
        foreach(var s in historyCatalog.Scans.Where(s=>Matches(s.Kind,s.Location,s.Summary))) {
            int row=scanHistoryGrid.Rows.Add(s.At,s.Kind,s.Location.Length==0?"Not recorded in this older entry":s.Location,s.Summary);scanHistoryGrid.Rows[row].Tag=s;
        }
        var matching=historyCatalog.Files.Where(f=>Matches(f.Path,f.Status,f.Reason)).ToArray();
        var matchedJournals=matching.Select(f=>f.Journal).Concat(historyCatalog.Scans.Where(s=>Matches(s.Kind,s.Location,s.Summary)).Select(s=>s.Journal)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        string? selectedJournal=(history.SelectedItem as HistoryItem)?.Path;
        history.BeginUpdate();history.Items.Clear();
        foreach(string path in store.History()){string label=ActivityLabel(path);if(Matches(label,historyJournalSearch.GetValueOrDefault(path,""))||matchedJournals.Contains(path))history.Items.Add(new HistoryItem(path,label));}
        history.EndUpdate();
        int selected=history.Items.Cast<HistoryItem>().ToList().FindIndex(h=>h.Path==selectedJournal);
        if(history.Items.Count>0)history.SelectedIndex=Math.Max(0,selected);
        historyEmpty.Visible=history.Items.Count==0;
        foreach(var f in matching) {
            var grid=f.Status is "Deleted" or "Recycled"?removedHistoryGrid:otherHistoryGrid;
            int row=grid.Rows.Add(f.At,f.Status,Path.GetFileName(f.Path),f.Size,f.Path,f.Reason);grid.Rows[row].Tag=f;
            grid.Rows[row].Cells[1].Style.ForeColor=f.Status=="Deleted"?Color.IndianRed:f.Status=="Recycled"?Color.SeaGreen:Color.DarkOrange;
        }
        int deleted=matching.Count(f=>f.Status=="Deleted"),recycled=matching.Count(f=>f.Status=="Recycled");
        long removed=matching.Where(f=>f.Status=="Deleted").Sum(f=>f.Size??0),bin=matching.Where(f=>f.Status=="Recycled").Sum(f=>f.Size??0);
        historyVisual.ShowData("Outcomes in this search · file counts",($"Deleted: {deleted:N0}",deleted,Color.MediumSeaGreen),($"Recycled: {recycled:N0}",recycled,Color.MediumTurquoise),($"Skipped / unconfirmed: {otherHistoryGrid.Rows.Count:N0}",otherHistoryGrid.Rows.Count,Color.Goldenrod));
        historyTotals.Text=$"{scanHistoryGrid.Rows.Count:N0} scans   ·   {deleted:N0} permanently deleted / {FormatBytes(removed)}   ·   {recycled:N0} recycled / {FormatBytes(bin)}\n{speedHistoryGrid.Rows.Count:N0} speed records · {otherHistoryGrid.Rows.Count:N0} skipped or unconfirmed · Totals reflect the search";
        historyCoverage.Text=$"Retained local records only; older purged history cannot be recovered. Recycled means sent to the bin at that time, not necessarily still there. {historyCatalog.Unreadable:N0} unreadable records. Sizes are logical, not reclaimed disk space.";
    }
    void ExportHistoryTab()
    {
        if(historyTabs.SelectedIndex==0){Report();return;}
        var grid=historyTabs.SelectedIndex==1?scanHistoryGrid:historyTabs.SelectedIndex==2?removedHistoryGrid:historyTabs.SelectedIndex==4?speedHistoryGrid:otherHistoryGrid;
        using var dialog=new SaveFileDialog{Filter="CSV file|*.csv",FileName="Maintenance-history.csv",Title="Export visible history — includes full file paths"};
        if(dialog.ShowDialog(this)!=DialogResult.OK)return;
        string Csv(object? value){string text=value is DateTimeOffset at?at.ToString("O"):Convert.ToString(value,CultureInfo.InvariantCulture)??"";if(text.Length>0&&"=+-@\t\r".Contains(text[0]))text="'"+text;return "\""+text.Replace("\"","\"\"")+"\"";}
        var lines=new List<string>{string.Join(",",grid.Columns.Cast<DataGridViewColumn>().Select(c=>Csv(c.Name=="Size"?"Size (bytes)":c.HeaderText)))};
        lines.AddRange(grid.Rows.Cast<DataGridViewRow>().Select(r=>string.Join(",",r.Cells.Cast<DataGridViewCell>().Select(c=>Csv(c.Value)))));
        File.WriteAllLines(dialog.FileName,lines,new UTF8Encoding(true));status.Text="History export saved. The CSV includes full paths.";
    }
}







