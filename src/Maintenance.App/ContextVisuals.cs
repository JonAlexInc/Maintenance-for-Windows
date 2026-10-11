using Maintenance.Core;
namespace Maintenance.App;
sealed partial class MainWindow
{
    readonly InsightStrip historyVisual=new(),startupVisual=new(),duplicatesVisual=new(),usageVisual=new(),cleanupVisual=new();
    void ShowCleanupVisual()
    {
        if(resultsMode) {
            long removed=cleanupOutcomes.Values.Count(o=>o.Status is "Deleted" or "Recycled"),other=cleanupOutcomes.Count-removed;
            cleanupVisual.ShowData("Cleanup outcomes · file counts",($"Removed / recycled: {removed:N0}",removed,Color.MediumSeaGreen),($"Skipped / failed: {other:N0}",other,Color.Goldenrod));
        } else if(scan!=null)cleanupVisual.ShowData("Scan coverage · entry counts",($"Files to review: {scan.Candidates.Count:N0}",scan.Candidates.Count,Color.MediumTurquoise),($"Omitted entries: {scan.Skipped.Count:N0}",scan.Skipped.Count,Color.Goldenrod));
        else cleanupVisual.ShowData("Scan coverage");
    }
    void ShowDuplicateVisual()
    {
        duplicatesVisual.Visible=duplicateGroup!=null;
        if(duplicateGroup==null)return;
        int remaining=duplicateGroup.Files.Count-1-duplicateSelected.Count;
        duplicatesVisual.ShowData($"{duplicateGroup.Files.Count} identical copies · {FormatBytes(duplicateGroup.ExtraBytes)} extra logical data",("Keep 1",1,Color.MediumSeaGreen),($"Selected {duplicateSelected.Count}",duplicateSelected.Count,Color.Goldenrod),($"Other copies {remaining}",remaining,Color.CornflowerBlue));
    }
    void ShowStartupVisual()
    {
        if(startupTabs.SelectedIndex<0)return;
        var rows=startupGrids[startupTabs.SelectedIndex].Rows.Cast<DataGridViewRow>().Select(r=>r.Tag).OfType<StartupEntry>().ToArray();
        if(rows.Any(r=>r.Kind=="Startup apps")){startupVisual.ShowData("Startup registrations · Windows manages enabled state",($"Registered: {rows.Length} · enablement not assessed",rows.Length,Color.SlateGray));return;}
        startupVisual.ShowData("Shown items · configuration, not measured performance impact",($"Enabled: {rows.Count(r=>r.Enabled)}",rows.Count(r=>r.Enabled),Color.MediumTurquoise),($"Disabled: {rows.Count(r=>!r.Enabled)}",rows.Count(r=>!r.Enabled),Color.SlateGray));
    }
    void ShowUsageVisual()
    {
        var cutoff=DateTimeOffset.UtcNow.AddDays(-(double)unusedDays.Value);
        usageVisual.ShowData($"Recorded usage · last {(int)unusedDays.Value} days · gaps remain unknown",($"Recent record: {usageRows.Count(r=>r.Last>cutoff)}",usageRows.Count(r=>r.Last>cutoff),Color.MediumTurquoise),($"Older record: {usageRows.Count(r=>r.Last<=cutoff)}",usageRows.Count(r=>r.Last<=cutoff),Color.Goldenrod),($"Unknown: {usageRows.Count(r=>r.Last==null)}",usageRows.Count(r=>r.Last==null),Color.SlateGray));
    }
}

sealed class HardwareMiniature : Control
{
    public string Kind {get;init;}="";public int Count {get;init;} public int Occupied {get;init;} public string Caption {get;init;}="";
    public HardwareMiniature(){Height=56;Dock=DockStyle.Top;DoubleBuffered=true;}
    protected override void OnPaint(PaintEventArgs e){
        base.OnPaint(e);using var pen=new Pen(ForeColor,1.5f);using var brush=new SolidBrush(Color.MediumTurquoise);
        int visible=Math.Clamp(Count,1,32);
        for(int i=0;i<visible;i++){var r=new Rectangle(4+i*13,9,9,22);e.Graphics.DrawRectangle(pen,r);if(i<Occupied)e.Graphics.FillRectangle(brush,Rectangle.Inflate(r,-2,-2));}
        int offset=8+visible*13;TextRenderer.DrawText(e.Graphics,Caption,Font,new Rectangle(offset,4,Math.Max(1,Width-offset),45),ForeColor,TextFormatFlags.WordBreak);
    }
}

