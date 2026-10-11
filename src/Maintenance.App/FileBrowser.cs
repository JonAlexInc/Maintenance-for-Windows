using Maintenance.Core;

namespace Maintenance.App;

sealed partial class MainWindow
{
    readonly TextBox nameFilter = new() { Width = 190, PlaceholderText = "Name, path, or extension", AccessibleName = "Filter name or full path" };
    readonly NumericUpDown minSize = new() { Width = 80, Maximum = 1000000, DecimalPlaces = 2, AccessibleName = "Minimum size in MiB" };
    readonly NumericUpDown maxSize = new() { Width = 80, Maximum = 1000000, DecimalPlaces = 2, AccessibleName = "Maximum size in MiB, zero means any" };
    readonly AppearanceCheckBox groupFolders = new() { Text = "Group by folder", AutoSize = true, Checked = true };
    readonly AppearanceCheckBox hideEmpty = new() { Text = "Hide empty files", AutoSize = true };
    readonly Label filterSummary = new() { Dock = DockStyle.Top, Height = 32 };
    readonly System.Windows.Forms.Timer filterDelay = new() { Interval = 300 };
    bool rebuildingFiles;
    bool pendingFilter;
    int sortColumn;
    bool sortDescending;
    readonly AppearanceCheckBox maxWait = new() { Text="Max wait", AutoSize=true };
    readonly NumericUpDown waitSeconds = new() { Minimum=1, Maximum=3600, Value=60, Width=85, Enabled=false, AccessibleName="Maximum scan duration in seconds" };
    bool fileColumnsSized;
    bool fittingFiles; readonly Dictionary<int,int> manualFileWidths=new();
    readonly AppearanceButton filterToggle = new() { Text="Filters ▸",AutoSize=true,AccessibleName="Show or hide result filters" };
    Panel? filterContainer;
    FlowLayoutPanel? filterBar;
    bool filtersExpanded;
    int ActiveCleanupFilters => (string.IsNullOrWhiteSpace(nameFilter.Text)?0:1)+(minSize.Value>0?1:0)+(maxSize.Value>0?1:0)+(hideEmpty.Checked?1:0)+(!string.IsNullOrEmpty(reviewFocus.Text)&&reviewFocus.Text!="All files"?1:0);
    void UpdateFilterPresentation()
    {
        int count=ActiveCleanupFilters;
        filterToggle.Text="Filters "+(filtersExpanded?"▾":"▸")+(count>0?$" · {count} active":"");
        filterToggle.Tag=count>0?PrimaryRole:null;
        StyleButton(filterToggle);
        if(filterContainer!=null){filterContainer.Height=filtersExpanded?(filterBar?.Height??54):0;filterContainer.Visible=filtersExpanded&&scanLocation.Text!=RestoreMode&&scanLocation.Text!=UpdateMode&&scanLocation.Text!=VmMode&&scanLocation.Text!=LeftoverMode;}
    }
    static readonly string[] fileHeadings = ["Name", "Full path", "Size", "Last modified", "Created", "Type", "Result", "Details"];

    Control BuildFilters()
    {
        files.ColumnWidthChanged+=(_,e)=>{if(!fittingFiles&&files.Capture)manualFileWidths[e.ColumnIndex]=files.Columns[e.ColumnIndex].Width;};
        var bar = filterBar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, MinimumSize = new(0,54), WrapContents = true, Padding = new(0, 4, 0, 4) };
        bar.Controls.Add(SearchField(nameFilter));
        bar.Controls.Add(new Label { Text = "Minimum MiB", AutoSize = true, Padding = new(0, 10, 0, 0) }); bar.Controls.Add(minSize);
        bar.Controls.Add(new Label { Text = "Max (0=any)", AutoSize = true, Padding = new(0, 5, 0, 0) }); bar.Controls.Add(maxSize);
        bar.Controls.Add(hideEmpty);enableFileSelection.CheckedChanged+=(_,_)=>{if(!enableFileSelection.Checked)foreach(ListViewItem row in files.Items)row.Checked=false;UpdateActions();files.Invalidate();};
        bar.Controls.Add(Button("Reset filters", () => { nameFilter.Clear(); minSize.Value = maxSize.Value = 0; hideEmpty.Checked = false; reviewFocus.SelectedItem="All files"; }));
        reviewFocus.Items.AddRange(["All files","Large files","Older than 90 days","Installers & archives"]);bar.Controls.Add(reviewFocus);
        reviewFocus.SelectedIndexChanged+=(_,_)=>{UpdateFilterPresentation();pendingFilter=true;UpdateActions();RebuildFiles(false);};
        previousFiles.Click+=(_,_)=>MoveFilePage(filePage-1);nextFiles.Click+=(_,_)=>MoveFilePage(filePage+1);
        FormClosed+=(_,_)=>renderCancel?.Cancel();
        maxWait.CheckedChanged+=(_,_)=>waitSeconds.Enabled=maxWait.Checked && !busy;
        void Changed(object? sender, EventArgs e) { UpdateFilterPresentation();renderVersion++;renderCancel?.Cancel(); pendingFilter = true; UpdateActions(); filterDelay.Stop(); filterDelay.Start(); }
        nameFilter.TextChanged += Changed; minSize.ValueChanged += Changed; maxSize.ValueChanged += Changed; hideEmpty.CheckedChanged += Changed;
        groupFolders.CheckedChanged += (_, _) => RebuildFiles(true);
        filterDelay.Tick += (_, _) => { filterDelay.Stop(); RebuildFiles(false); };
        FormClosed += (_, _) => filterDelay.Dispose();
        files.ColumnClick += (_, e) => { sortDescending = sortColumn == e.Column && !sortDescending; sortColumn = e.Column; RebuildFiles(true); };
        files.DoubleClick += (_, _) => ShowFilePath();
        var menu = new ContextMenuStrip();
        menu.Items.Add("Copy full path", null, (_, _) => { if (files.SelectedItems.Count > 0) Clipboard.SetText(((Candidate)files.SelectedItems[0].Tag!).Path); });
        menu.Items.Add("Show in File Explorer",null,(_,_)=>
        {
            if(files.SelectedItems.Count==0)return;
            var path=((Candidate)files.SelectedItems[0].Tag!).Path;if(streamReview)path=StreamCatalog.OwnerPath(path);
            if(!File.Exists(path)){MessageBox.Show(this,"This file is no longer available. Scan again to refresh the list.");return;}
            var start=new System.Diagnostics.ProcessStartInfo("explorer.exe") {UseShellExecute=false};start.ArgumentList.Add("/select,"+path);
            try{System.Diagnostics.Process.Start(start);}catch(Exception e){MessageBox.Show(this,"Could not open File Explorer: "+e.Message);}
        });
        files.ContextMenuStrip = menu;
        menu.Items.Add("Exclude selected",null,(_,_)=>excludeButton.PerformClick());
        var filters=filterContainer=new Panel {Dock=DockStyle.Top,Height=0,Visible=false};filters.Controls.Add(bar);
        bar.SizeChanged+=(_,_)=>UpdateFilterPresentation();
        filterToggle.Click+=(_,_)=>{filtersExpanded=!filtersExpanded;UpdateFilterPresentation();};
        UpdateFilterPresentation();
        return filters;
    }
    void ShowFilePath()
    {
        if(streamReview){_ = ShowStreamInfo();return;}
        if (files.SelectedItems.Count == 0) return;
        var c = (Candidate)files.SelectedItems[0].Tag!;
        using var dialog = new Form { Text = "Exact file path", Width = 850, Height = 210, StartPosition = FormStartPosition.CenterParent };
        dialog.Controls.Add(new TextBox { Text = c.Path, ReadOnly = true, Multiline = true, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Vertical });
        PaintTheme(dialog, surface); dialog.ShowDialog(this);
    }
    const int FilePageSize=500;
    List<Candidate> filteredRows=[];
    int filePage, renderVersion;
    long filteredBytes;
    Task renderTask=Task.CompletedTask;
    CancellationTokenSource? renderCancel;
    readonly AppearanceButton previousFiles=new() { Text="Previous",AutoSize=true };
    readonly AppearanceButton nextFiles=new() { Text="Next",AutoSize=true };
    void RebuildFiles(bool preserveSelection)
    {
        if(busy || scan is null)return;
        renderTask=PrepareFileView(preserveSelection);
    }
    async Task PrepareFileView(bool preserveSelection)
    {
        var snapshot=scan!; int version=++renderVersion;
        renderCancel?.Cancel(); var cancellation=new CancellationTokenSource();renderCancel=cancellation;
        var token=cancellation.Token;
        string text=nameFilter.Text.Trim();string focus=reviewFocus.Text; long minimum=(long)(minSize.Value*1048576),maximum=(long)(maxSize.Value*1048576);
        bool excludeEmpty=hideEmpty.Checked,descending=sortDescending;int column=sortColumn;
        var outcomes=new Dictionary<string,Outcome>(cleanupOutcomes,StringComparer.OrdinalIgnoreCase);
        var selected=preserveSelection?files.CheckedItems.Cast<ListViewItem>().Select(i=>((Candidate)i.Tag!).Path).ToHashSet(StringComparer.OrdinalIgnoreCase):[];
        pendingFilter=true; previousFiles.Enabled=nextFiles.Enabled=false; files.Enabled=false;UpdateActions();
        filterSummary.Text=$"Preparing {snapshot.Candidates.Count:N0} files for browsing…";
        List<Candidate> Build()
        {
            var query=snapshot.Candidates.Where(c=> { token.ThrowIfCancellationRequested(); return MatchesReview(c,focus)&&c.Path.Contains(text,StringComparison.OrdinalIgnoreCase)&&c.Size>=minimum&&(maximum==0||c.Size<=maximum)&&(!excludeEmpty||c.Size>0); });
            Func<Candidate,object> key=column switch {1=>c=>c.Path,2=>c=>c.Size,3=>c=>c.Written,4=>c=>c.Created,5=>c=>Path.GetExtension(c.Path),6=>c=>outcomes.GetValueOrDefault(c.Path)?.Status??"Not selected",7=>c=>outcomes.GetValueOrDefault(c.Path)?.Reason??"",_=>c=>Path.GetFileName(c.Path)};
            return(descending?query.OrderByDescending(key):query.OrderBy(key)).ThenBy(c=>c.Path,StringComparer.OrdinalIgnoreCase).ToList();
        }
        try
        {
            var result=snapshot.Candidates.Count>1000?await Task.Run(()=>{var rows=Build();return(rows,bytes:rows.Sum(c=>c.Size));},token):(rows:Build(),bytes:0L);
            if(IsDisposed||version!=renderVersion||!ReferenceEquals(scan,snapshot))return;
            filteredRows=result.rows;filteredBytes=snapshot.Candidates.Count>1000?result.bytes:result.rows.Sum(c=>c.Size);filePage=0;pendingFilter=false;files.Enabled=true;
            RenderFilePage(selected);
        }
        catch(OperationCanceledException) {if(!IsDisposed&&version==renderVersion){pendingFilter=false;files.Enabled=!busy;filterSummary.Text="Result preparation stopped · Previously displayed rows remain. Change a filter to prepare results again.";UpdateActions();}}
        catch(Exception e) { if(!IsDisposed && version==renderVersion) { filterSummary.Text="Could not prepare results: "+e.Message;pendingFilter=false;files.Enabled=true; } }
        finally { if(ReferenceEquals(renderCancel,cancellation))renderCancel=null;cancellation.Dispose(); }
    }
    void RenderFilePage(HashSet<string>? checkedPaths=null)
    {
        if(scan is null)return;
        var selected=checkedPaths??[];
        var rows=filteredRows.Skip(filePage*FilePageSize).Take(FilePageSize).ToList();
        rebuildingFiles = true; files.BeginUpdate();
        try
        {
            files.Items.Clear(); files.Groups.Clear(); files.ShowGroups = groupFolders.Checked;
            var groups = new Dictionary<string, ListViewGroup>(StringComparer.OrdinalIgnoreCase);
            if (groupFolders.Checked)
                foreach (var folder in rows.GroupBy(c => Path.GetDirectoryName(c.Path)!).OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
                { var group = new ListViewGroup($"{folder.Key} — {folder.Count():N0} files · {FormatBytes(folder.Sum(c => c.Size))}"); groups.Add(folder.Key, group); files.Groups.Add(group); }
            foreach (var c in rows)
            {
                var folder = Path.GetDirectoryName(c.Path)!;
                var item = new ListViewItem(Path.GetFileName(c.Path)) { Tag = c, ToolTipText = c.Path, Checked = selected.Contains(c.Path) };
                item.SubItems.Add(c.Path); item.SubItems.Add(FormatBytes(c.Size));
                item.SubItems.Add(DateTime.FromFileTimeUtc(c.Written).ToLocalTime().ToString("g"));
                item.SubItems.Add(DateTime.FromFileTimeUtc(c.Created).ToLocalTime().ToString("g")); item.SubItems.Add(streamReview?"Named $DATA":Path.GetExtension(c.Path));
                                var outcome=cleanupOutcomes.GetValueOrDefault(c.Path);
                item.SubItems.Add(resultsMode ? outcome?.Status ?? "Not selected / not processed" : driveReview ? "Review only" : "Cleanup candidate"); item.SubItems.Add(outcome?.Reason ?? ReviewReason(c));
                if(resultsMode && outcome is not null) item.ForeColor=outcome.Status is "Deleted" or "Recycled" ? Color.FromArgb(65,170,112) : Color.FromArgb(202,142,50);
                if (groupFolders.Checked) item.Group = groups[folder]; files.Items.Add(item);
            }
            for (int i = 0; i < files.Columns.Count; i++) files.Columns[i].Text = (streamReview ? new[]{"File : stream", "Full stream path", "Stream size", "File modified", "File created", "Stream type", "Result", "Details"}[i] : fileHeadings[i]) + (i == sortColumn ? sortDescending ? " ▼" : " ▲" : "");
        }
        finally { files.EndUpdate(); rebuildingFiles = false; }
        FitFileColumns();
        filterSummary.Text = $"Page {filePage+1:N0} of {Math.Max(1,(filteredRows.Count+FilePageSize-1)/FilePageSize):N0} · {filteredRows.Count:N0} matching {(streamReview?"streams":"files")} · {FormatBytes(filteredBytes)} · 500 files per page";
        pagePosition.Text=$"Page {filePage+1:N0} of {Math.Max(1,(filteredRows.Count+FilePageSize-1)/FilePageSize):N0}";jumpPage.Maximum=Math.Max(1,(filteredRows.Count+FilePageSize-1)/FilePageSize);jumpPage.Value=filePage+1;
        if(driveReview)status.Text="Ready. Browse pages or filter all scanned files. Changing pages clears selection.";
        UpdateActions();
        if (rows.Count == 0 && scan.Candidates.Count > 0) emptyFiles.Text = "No files match your filters.\n\nReset filters to see all eligible files.";
    }
    void FitFileColumns()
    {
        fittingFiles=true;
        files.AutoResizeColumns(ColumnHeaderAutoResizeStyle.ColumnContent);
        foreach(ColumnHeader column in files.Columns)
            column.Width=Math.Max(column.Width+18,TextRenderer.MeasureText(column.Text,files.Font).Width+36);
        if(files.Items.Count>0)files.Columns[0].Width=Math.Max(files.Columns[0].Width,files.Items.Cast<ListViewItem>().Max(i=>TextRenderer.MeasureText(i.Text,files.Font).Width)+(int)(16*files.DeviceDpi/96f)+28);
        foreach(var pair in manualFileWidths)if(pair.Key<files.Columns.Count)files.Columns[pair.Key].Width=pair.Value;
        fittingFiles=false;fileColumnsSized=files.Items.Count>0;
    }
}





