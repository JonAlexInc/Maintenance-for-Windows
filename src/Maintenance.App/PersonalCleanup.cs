using Maintenance.Core;
namespace Maintenance.App;
sealed partial class MainWindow
{
    readonly AppearanceCheckBox enableFileSelection=new(){Text="File & folder selection",AutoSize=true,Checked=true};
    readonly Label cleanupNotice=new(){Dock=DockStyle.Fill,Padding=new(0,8,0,0),Font=new("Segoe UI",8)};
    string? reviewedRoot;
    bool PersonalSelection=>driveReview&&!streamReview&&enableFileSelection.Checked;
    async Task RecycleSelected()
    {
        if(smoke||!PersonalSelection||scan is null||resultsMode||reviewedRoot is null)return;
        var selected=files.CheckedItems.Cast<ListViewItem>().Select(i=>(Candidate)i.Tag!).ToArray();
        if(selected.Length==0)return;
        if(MessageBox.Show(this,$"Send {selected.Length:N0} selected files ({FormatBytes(selected.Sum(c=>c.Size))}) to the Recycle Bin?\n\nFolder checkboxes select only files shown on the current page. Hidden files and other pages are not selected. Recycling does not free disk space until you empty the Recycle Bin.","Review selected files",MessageBoxButtons.OKCancel,MessageBoxIcon.Question,MessageBoxDefaultButton.Button2)!=DialogResult.OK)return;
        resultsMode=true;files.CheckBoxes=false;SetBusy(true);cancel=new();string root=reviewedRoot;var excluded=exclusions.ToArray();var token=cancel.Token;
        try {
            using var journal=store.StartJournal();LocalStore.Append(journal,new{Summary=$"Recycle requested: {selected.Length} selected personal files."});
            var result=await RecycleShell.OnSta(()=>PersonalReview.Recycle(root,selected,excluded,token,o=>LocalStore.Append(journal,o),RecycleShell.Move));
            RecordCleanup(result,selected.Length);int moved=result.Outcomes.Count(o=>o.Status=="Recycled");
            metricTitles[1].Text="RECYCLED FILE SIZE";metricTitles[3].Text="RECYCLED";deletedMetric.Text=moved.ToString("N0");skippedMetric.Text=(selected.Length-moved).ToString("N0");
            scanSummary.Text=$"{moved:N0} recycled · {selected.Length-moved:N0} skipped / remaining · Results retained. Restore files through the Windows Recycle Bin.";
            LocalStore.Append(journal,new{Summary=scanSummary.Text});status.Text="Recycling finished. Results are saved in History.";
        }catch{scanSummary.Text="Recycling interrupted. Review History for recorded outcomes.";throw;}
        finally{cancel.Dispose();cancel=null;SetBusy(false);RebuildFiles(false);RefreshHistory();}
    }
}
