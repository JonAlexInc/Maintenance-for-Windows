using Maintenance.Core;

namespace Maintenance.App;
sealed partial class MainWindow
{
    bool resultsMode;
    readonly Dictionary<string, Outcome> cleanupOutcomes = new(StringComparer.OrdinalIgnoreCase);
    readonly Label eligibleMetric=new(), bytesMetric=new(), skippedMetric=new(), deletedMetric=new();
    readonly List<Label> metricTitles=[];
    Control BuildCleanupMetrics()
    {
        var row=new TableLayoutPanel { Dock=DockStyle.Top,Height=102,ColumnCount=4 };
        var values=new[]{eligibleMetric,bytesMetric,skippedMetric,deletedMetric};
        string[] titles=["ELIGIBLE FILES","LOGICAL SIZE","SKIPPED / REMAINING","DELETED"];
        Color[] colors=[Color.FromArgb(70,145,235),Color.FromArgb(72,174,184),Color.FromArgb(215,159,60),Color.FromArgb(74,180,122)];
        for(int i=0;i<4;i++)
        {
            row.ColumnStyles.Add(new(SizeType.Percent,25)); var card=Card(); card.Padding=new(12); card.Dock=DockStyle.Fill;
            var title=TextLabel(titles[i],7,true); title.Dock=DockStyle.Top; title.Height=28;
            metricTitles.Add(title);
            var value=values[i]; value.Text="—"; value.Dock=DockStyle.Fill; value.Font=new("Segoe UI",14,FontStyle.Bold); value.Tag=colors[i];
            card.Controls.Add(value);card.Controls.Add(title);row.Controls.Add(card,i,0);
        }
        var panel=new Panel{Dock=DockStyle.Top,Height=168};panel.Controls.Add(cleanupVisual);cleanupVisual.Dock=DockStyle.Bottom;panel.Controls.Add(row);return panel;
    }
    void ClearResults()
    {
        if(busy) return;
        renderCancel?.Cancel();renderVersion++;filteredRows=[];pendingFilter=false;files.Enabled=true;previousFiles.Enabled=nextFiles.Enabled=false;
        enableFileSelection.Checked=false;reviewedRoot=null;cleanupOutcomes.Clear(); streamReview=false;driveReview=false; resultsMode=false; scan=null; files.Items.Clear(); files.Groups.Clear(); files.CheckBoxes=true;
        eligibleMetric.Text=bytesMetric.Text=skippedMetric.Text=deletedMetric.Text="—";
        metricTitles[0].Text="ELIGIBLE FILES";metricTitles[1].Text="LOGICAL SIZE";metricTitles[3].Text="DELETED";
        for(int i=0;i<fileHeadings.Length;i++)files.Columns[i].Text=fileHeadings[i];fileColumnsSized=false;
        scanSummary.Text="Results cleared. Scan files for a fresh preview."; filterSummary.Text=""; ShowCleanupVisual();UpdateActions();
    }
    void ShowScanMetrics()
    {
        if(scan is null)return;ShowCleanupVisual();
        metricTitles[3].Text="DELETED";metricTitles[0].Text="ELIGIBLE FILES"; metricTitles[1].Text="LOGICAL SIZE";
        eligibleMetric.Text=$"{scan.Candidates.Count:N0}";bytesMetric.Text=FormatBytes(scan.Candidates.Sum(c=>c.Size));skippedMetric.Text=$"{scan.Skipped.Count:N0}";deletedMetric.Text="—";
    }
    void RecordCleanup(RunResult result,int requested)
    {
        cleanupOutcomes.Clear(); foreach(var outcome in result.Outcomes) cleanupOutcomes[outcome.Path]=outcome;
        resultsMode=true;files.CheckBoxes=false;ShowCleanupVisual();
        fileColumnsSized=false;
        metricTitles[0].Text="REQUESTED FILES"; metricTitles[1].Text="DELETED FILE SIZE";
        eligibleMetric.Text=$"{requested:N0} requested";bytesMetric.Text=FormatBytes(result.Outcomes.Sum(o=>o.Bytes));
        deletedMetric.Text=$"{result.Outcomes.Count(o=>o.Status=="Deleted"):N0}";
        skippedMetric.Text=$"{requested-result.Outcomes.Count(o=>o.Status=="Deleted"):N0}";
    }
}



