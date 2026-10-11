using System.Text;
namespace Maintenance.App;

sealed partial class MainWindow
{
    readonly AppearanceButton expandResults=new(){Text="Expand results",Dock=DockStyle.Right,Width=154,AccessibleName="Open focused results window"};
    Control? duplicateResultSurface;Form? focusedResults;
    Control? ActiveResults=>activePage switch {
        1=>scanLocation.Text==RestoreMode?restoreList:scanLocation.Text==UpdateMode?updateList:scanLocation.Text==VmMode?vmGrid:scanLocation.Text==LeftoverMode?leftoverTabs.SelectedIndex switch{0=>leftoverGrid,1=>installedGrid,2=>unusedGrid,_=>appDataGrid}:fileArea,
        4=>historyTabs.SelectedIndex switch{0=>history,1=>scanHistoryGrid,2=>removedHistoryGrid,3=>otherHistoryGrid,_=>speedHistoryGrid},
        5=>hardwarePanels.ElementAtOrDefault(hardwareTabs.SelectedIndex),
        7=>startupGrids.ElementAtOrDefault(startupTabs.SelectedIndex),
        8=>duplicateResultSurface,_=>null
    };
    TextBox? ActiveResultsFilter=>activePage switch{1=>scanLocation.Text==VmMode?vmFind:scanLocation.Text==LeftoverMode&&leftoverTabs.SelectedIndex==3?appDataSearch:scanLocation.Text is TempMode or BrowserMode or "Downloads" or "Desktop" or "Chosen folder" or StreamsMode?nameFilter:null,4=>historyFind,5=>systemSearch,7=>startupFind,8=>duplicateFind,_=>null};
    internal void PopOutResults(Control? requested=null)
    {
        if(focusedResults!=null){focusedResults.Activate();return;}
        var active=ActiveResults;var result=requested==null||active?.Contains(requested)==true?active:requested;if(result?.Parent is not Control parent)return;
        int index=parent.Controls.GetChildIndex(result);var dock=result.Dock;var bounds=result.Bounds;var anchor=result.Anchor;
        var dialog=new Form {Text="Maintenance · Focused results",WindowState=FormWindowState.Maximized,MinimumSize=new(900,600),StartPosition=FormStartPosition.CenterParent,Font=Font,Icon=Icon};focusedResults=dialog;
        var actions=WorkflowRow();actions.Dock=DockStyle.Top;actions.Padding=new(16,12,8,8);
        var returnButton=new AppearanceButton {Text="Return to app",AutoSize=true,MinimumSize=new(104,42)};returnButton.Click+=(_,_)=>dialog.Close();actions.Controls.Add(returnButton);
        var stop=new AppearanceButton {Text="■  Stop all",Tag=CancelRole,Size=new(132,42)};stop.Click+=(_,_)=>StopAllOperations();actions.Controls.Add(stop);
        var export=Button("Export visible results…",()=>ExportFocused(result));actions.Controls.Add(export);
        var added=new List<Button>{export};
        if(result is ResultGrid grid){var fit=Button("Fit columns",()=>grid.FitResults());actions.Controls.Add(fit);added.Add(fit);}
        // Reuse existing command handlers and their enabled state. Selection belongs
        // to the original control, so review safeguards and journals remain intact.
        var commands=new List<Button>();
        if(result==fileArea){commands.AddRange(new[]{cleanButton,selectAllButton,clearButton});if(streamReview&&viewStreamButton!=null)commands.Add(viewStreamButton);}
        if(result==duplicateResultSurface)commands.Add(duplicateRecycle);
        if(result==restoreList&&removeRestore!=null)commands.Add(removeRestore);
        if(result is DataGridView startupGrid&&startupGrids.Contains(startupGrid)){commands.Add(startupDisable);commands.Add(startupRestore);}
        foreach(var command in commands){var proxy=new AppearanceButton {Text=command.Text,Enabled=command.Enabled,AutoSize=true,MinimumSize=new(104,42),Tag=command.Tag};proxy.Click+=(_,_)=>command.PerformClick();actions.Controls.Add(proxy);StyleButton(proxy);}
        var search=new TextBox {Width=440,PlaceholderText="Filter names, paths, or details"};var sourceFilter=ActiveResultsFilter;
        if(sourceFilter!=null)search.Text=sourceFilter.Text;
        var filters=ResultFilters(search);filters.Padding=new(16,4,8,8);
        var toggles=new List<(AppearanceCheckBox Original,AppearanceCheckBox Proxy,EventHandler Mirror)>();
        if(result==fileArea)foreach(var original in new[]{enableFileSelection,groupFolders}) {
            var proxy=new AppearanceCheckBox {Text=original==enableFileSelection?"Enable selection":"Group by folder",Checked=original.Checked,AutoSize=true};proxy.CheckedChanged+=(_,_)=>original.Checked=proxy.Checked;
            EventHandler toggleMirror=(_,_)=>proxy.Checked=original.Checked;original.CheckedChanged+=toggleMirror;toggles.Add((original,proxy,toggleMirror));filters.Controls.Add(proxy);
        }
        var note=new Label {Dock=DockStyle.Bottom,Height=36,Padding=new(16,8,16,0),Text="Same results and selection · Right-click a result for file actions · Closing returns to the app",Tag=MutedRole};
        var placeholder=new Label {Dock=dock,Bounds=bounds,Text="Results are open in the focused window. Use Return to app or close that window.",TextAlign=ContentAlignment.MiddleCenter,Padding=new(24),Tag=MutedRole};
        parent.Controls.Remove(result);parent.Controls.Add(placeholder);parent.Controls.SetChildIndex(placeholder,index);
        result.Dock=DockStyle.Fill;dialog.Controls.Add(result);dialog.Controls.Add(note);dialog.Controls.Add(filters);dialog.Controls.Add(actions);
        var list=result as ListView;var pool=list?.Items.Cast<ListViewItem>().ToArray();var lastShown=pool;var lastRows=(result as DataGridView)?.Rows.Cast<DataGridViewRow>().ToArray();bool filtering=false;
        void ApplyFilter(){if(filtering)return;filtering=true;try {
            if(sourceFilter!=null){sourceFilter.Text=search.Text;return;}
            string query=search.Text.Trim();
            if(result is DataGridView data){data.CurrentCell=null;foreach(DataGridViewRow row in data.Rows)row.Visible=query.Length==0||row.Cells.Cast<DataGridViewCell>().Any(c=>(Convert.ToString(c.Value)??"").Contains(query,StringComparison.OrdinalIgnoreCase));}
            if(list!=null&&pool!=null){list.BeginUpdate();list.Items.Clear();list.Items.AddRange(pool.Where(i=>query.Length==0||i.SubItems.Cast<ListViewItem.ListViewSubItem>().Any(s=>s.Text.Contains(query,StringComparison.OrdinalIgnoreCase))).ToArray());lastShown=list.Items.Cast<ListViewItem>().ToArray();list.EndUpdate();}
        }finally{filtering=false;}}
        search.TextChanged+=(_,_)=>ApplyFilter();
        EventHandler mirror=(_,_)=>{if(sourceFilter!=null&&search.Text!=sourceFilter.Text)search.Text=sourceFilter.Text;};if(sourceFilter!=null)sourceFilter.TextChanged+=mirror;
        var timer=new System.Windows.Forms.Timer {Interval=200};timer.Tick+=(_,_)=>{stop.Enabled=OperationsRunning||usageObserver!=null;foreach(var toggle in toggles)toggle.Proxy.Enabled=!busy;
            if(sourceFilter==null&&!busy){if(list!=null&&!list.Items.Cast<ListViewItem>().SequenceEqual(lastShown??[])){pool=list.Items.Cast<ListViewItem>().ToArray();ApplyFilter();}if(result is DataGridView data&&!data.Rows.Cast<DataGridViewRow>().SequenceEqual(lastRows??[])){lastRows=data.Rows.Cast<DataGridViewRow>().ToArray();ApplyFilter();}}
            int offset=3+(result is ResultGrid?1:0);for(int i=0;i<commands.Count;i++)((Button)actions.Controls[offset+i]).Enabled=commands[i].Enabled;note.Text=emergencyStopping?"Stopping all operations · Partial results retained; pending Windows calls may take time.":liveStatus.Text;};timer.Start();
        dialog.FormClosed+=(_,_)=>{timer.Dispose();if(sourceFilter!=null)sourceFilter.TextChanged-=mirror;foreach(var toggle in toggles)toggle.Original.CheckedChanged-=toggle.Mirror;
            if(!result.IsDisposed&&!parent.IsDisposed&&!parent.Disposing){if(result is DataGridView data)foreach(DataGridViewRow row in data.Rows)row.Visible=true;if(list!=null&&pool!=null){if(!list.Items.Cast<ListViewItem>().SequenceEqual(lastShown??[]))pool=list.Items.Cast<ListViewItem>().ToArray();list.Items.Clear();list.Items.AddRange(pool);}dialog.Controls.Remove(result);parent.Controls.Remove(placeholder);result.Dock=dock;result.Anchor=anchor;result.Bounds=bounds;parent.Controls.Add(result);parent.Controls.SetChildIndex(result,Math.Min(index,parent.Controls.Count-1));}
            placeholder.Dispose();foreach(var button in added)mutationButtons.Remove(button);focusedResults=null;Activate();};
        PaintTheme(dialog,canvas);dialog.Show(this);search.Focus();
    }
    void ExportFocused(Control result)
    {
        if(result is HardwareCardGrid){ExportSystem();return;}
        using var dialog=new SaveFileDialog {Filter="CSV report|*.csv",FileName="Maintenance-results.csv",Title="Export visible results (includes paths)"};if(dialog.ShowDialog(focusedResults??this)!=DialogResult.OK)return;
        string Quote(string value){if(value.Length>0&&"=+-@\t\r".Contains(value[0]))value="'"+value;return "\""+value.Replace("\"","\"\"")+"\"";}
        var rows=new List<string[]>();
        if(result is DataGridView grid){rows.Add(grid.Columns.Cast<DataGridViewColumn>().Where(c=>c.Visible).Select(c=>c.HeaderText).ToArray());foreach(DataGridViewRow row in grid.Rows.Cast<DataGridViewRow>().Where(r=>r.Visible))rows.Add(row.Cells.Cast<DataGridViewCell>().Where(c=>grid.Columns[c.ColumnIndex].Visible).Select(c=>Convert.ToString(c.FormattedValue)??"").ToArray());}
        else if(result is ListView list){rows.Add(list.Columns.Cast<ColumnHeader>().Select(c=>c.Text).ToArray());rows.AddRange(list.Items.Cast<ListViewItem>().Select(i=>i.SubItems.Cast<ListViewItem.ListViewSubItem>().Select(s=>s.Text).ToArray()));}
        else if(result==fileArea){rows.Add(files.Columns.Cast<ColumnHeader>().Select(c=>c.Text).ToArray());rows.AddRange(files.Items.Cast<ListViewItem>().Select(i=>i.SubItems.Cast<ListViewItem.ListViewSubItem>().Select(s=>s.Text).ToArray()));}
        else if(result==duplicateResultSurface){rows.Add(["Group","Copies","Extra size"]);rows.AddRange(duplicateGroups.Rows.Cast<DataGridViewRow>().Select(r=>r.Cells.Cast<DataGridViewCell>().Select(c=>Convert.ToString(c.FormattedValue)??"").ToArray()));}
        else if(result is ListBox box)rows.AddRange(box.Items.Cast<object>().Select(i=>new[]{i.ToString()??""}));
        else {ExportSystem();return;}
        File.WriteAllLines(dialog.FileName,rows.Select(r=>string.Join(",",r.Select(Quote))),new UTF8Encoding(true));
    }
}
