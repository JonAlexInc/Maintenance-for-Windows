using Maintenance.Core;
namespace Maintenance.App;
sealed partial class MainWindow
{
    const string VmMode="Virtual machines";
    readonly Panel vmPanel=new(){Dock=DockStyle.Fill,Visible=false};
    readonly AppearanceChoice vmDrive=new(){Width=300,Height=40,Text="All local drives"};
    readonly AppearanceCheckBox vmRaw=new(){AutoSize=true,Text="Include ambiguous .img / .raw files",Margin=new(12,10,4,4)};
    readonly TextBox vmFind=new(){Width=280,PlaceholderText="Filter name, path, format or category"};
    readonly Label vmSummary=new(){Dock=DockStyle.Top,Height=80,Padding=new(12),Font=new("Segoe UI",11,FontStyle.Bold),Text="Find virtual machines and disk images\nChoose a local drive or scan all local drives. No files are mounted or changed."};
    readonly Label vmNote=new(){Dock=DockStyle.Bottom,Height=75,Padding=new(10),Text="Review only · Identification is based on file formats, not verified VM ownership or activity.\nLogical file sizes are not allocated disk space. Snapshot chains and split disks can depend on other files. WSL disks are included."};
    readonly DataGridView vmGrid=HistoryGrid();List<VmFile> vmFiles=[];
    Control BuildVmPanel()
    {
        vmDrive.Items.Add("All local drives");foreach(var drive in DriveInfo.GetDrives().Where(d=>LocalDriveType(d.DriveType)))if(drive.IsReady)vmDrive.Items.Add(drive.Name);
        var actions=new FlowLayoutPanel{Dock=DockStyle.Top,AutoSize=true,MinimumSize=new(0,60),Padding=new(4,8,0,8)};
        actions.Controls.Add(StartSearch(ScanVirtualMachines));actions.Controls.Add(ScanCancel());actions.Controls.Add(vmDrive);var filters=ResultFilters(vmFind,vmRaw);actions.Controls.Add(Button("Export list…",ExportVm));
        foreach(var (name,width) in new[]{("Name",260),("Size",140),("Format",100),("Category",230),("Last modified",190),("Full path",660)})vmGrid.Columns.Add(new DataGridViewTextBoxColumn{Name=name,Width=width,SortMode=DataGridViewColumnSortMode.Automatic});
        vmGrid.CellFormatting+=(_,e)=>{if(e.ColumnIndex==1&&e.Value is long size){e.Value=FormatBytes(size);e.FormattingApplied=true;}if(e.Value is DateTime date){e.Value=date.ToString("g");e.FormattingApplied=true;}};
        vmFind.TextChanged+=(_,_)=>RenderVm();vmPanel.Controls.Add(vmGrid);vmPanel.Controls.Add(vmSummary);vmPanel.Controls.Add(filters);vmPanel.Controls.Add(actions);vmPanel.Controls.Add(vmNote);return vmPanel;
    }
    void RenderVm()
    {
        vmGrid.Rows.Clear();foreach(var item in vmFiles.Where(f=>string.IsNullOrWhiteSpace(vmFind.Text)||(f.Path+" "+f.Format+" "+f.Category).Contains(vmFind.Text.Trim(),StringComparison.OrdinalIgnoreCase)).OrderByDescending(f=>f.Size))vmGrid.Rows.Add(Path.GetFileName(item.Path),item.Size,item.Format,item.Category,item.Modified,item.Path);
    }
    async Task ScanVirtualMachines()
    {
        if(busy)return;string[] roots=vmDrive.Text=="All local drives"?DriveInfo.GetDrives().Where(d=>LocalDriveType(d.DriveType)&&d.IsReady).Select(d=>d.Name).ToArray():[vmDrive.Text];bool raw=vmRaw.Checked;
        SetBusy(true);cancel=new();vmSummary.Text="Searching for virtual-machine files…\nCancel keeps the files found so far.";
        try{
            var result=await Task.Run(()=>VirtualMachineCatalog.Scan(roots,raw,cancel.Token,new Progress<int>(n=>BeginInvoke(new Action(()=>vmSummary.Text=$"Searching · {n:N0} entries inspected\nCancel keeps partial results.")))));
            vmFiles=result.Files;RenderVm();vmSummary.Text=$"{vmFiles.Count:N0} virtual-machine files · {FormatBytes(vmFiles.Sum(f=>f.Size))} logical size\n{result.Inspected:N0} inspected · {result.Skipped:N0} skipped · {(result.Partial?"Partial coverage":"Scan complete")}";
            using var journal=store.StartJournal();LocalStore.Append(journal,new{Kind="Virtual machines",Location=string.Join(", ",roots),Summary="Scan virtual machines: "+vmSummary.Text.Replace("\n"," · ")});
        }finally{cancel.Dispose();cancel=null;SetBusy(false);RefreshHistory();}
    }
    void ExportVm()
    {
        using var dialog=new SaveFileDialog{Filter="CSV|*.csv",FileName="Maintenance-virtual-machines.csv",Title="Export visible VM files (includes full paths)"};if(dialog.ShowDialog(this)!=DialogResult.OK)return;
        string Cell(object? value){string s=value?.ToString()??"";if(s.Length>0&&"=+-@".Contains(s[0]))s="'"+s;return "\""+s.Replace("\"","\"\"")+"\"";}
        var lines=new List<string>{"Name,Size bytes,Format,Category,Last modified,Full path"};lines.AddRange(vmGrid.Rows.Cast<DataGridViewRow>().Select(r=>string.Join(",",r.Cells.Cast<DataGridViewCell>().Select(c=>Cell(c.Value)))));File.WriteAllLines(dialog.FileName,lines);
    }
}


