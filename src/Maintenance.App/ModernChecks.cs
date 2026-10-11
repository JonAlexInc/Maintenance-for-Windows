namespace Maintenance.App;
sealed partial class MainWindow
{
    async Task VerifyModernUi(){
        SetBusy(false);SelectPage(0);themeChoice.SelectedItem="Dark";PopulateDrives();
        if(machineDetail.Parent!=null)throw new InvalidOperationException("Overview detail lines still present");
        IEnumerable<Control> Desc(Control c)=>c.Controls.Cast<Control>().SelectMany(x=>new[]{x}.Concat(Desc(x)));
        var controls=Desc(this).ToArray();if(controls.OfType<SystemDriveBadge>().Count()!=1)throw new InvalidOperationException("System drive badge missing or duplicated");
        if(controls.OfType<TabControl>().Any())throw new InvalidOperationException("Native tab frame remains");
        SetBusy(true);status.Text="Scanning temporary files · 12,450 entries reviewed";await Task.Delay(350);
        if(!liveStatus.Running||!liveStatus.Text.Contains("12,450"))throw new InvalidOperationException("Live status not reporting scan");
        void Capture(string name){Refresh();using var b=new Bitmap(Width,Height);DrawToBitmap(b,new Rectangle(0,0,Width,Height));b.Save("artifacts/"+name+".png");}
        Capture("modern-overview");SelectPage(4);await Task.Delay(300);if(!liveStatus.Running)throw new InvalidOperationException("Activity lost across pages");Capture("modern-history");
        SetBusy(false);status.Text="Review complete.";await Task.Delay(300);if(liveStatus.Running)throw new InvalidOperationException("Activity did not stop");
        SelectPage(1);scanLocation.SelectedItem=LeftoverMode;Capture("modern-app-tabs");themeChoice.SelectedItem="Light";Capture("modern-light");
        VerifyUsageRules();leftoverTabs.SelectedIndex=2;await ReadUnusedApps();unusedView.SelectedItem="Usage unknown";Capture("unused-apps-preview");
        File.WriteAllText("artifacts/modern-check-success.txt",DateTimeOffset.Now.ToString("o")+" · System badge, simplified overview, tab replacement and global activity state passed.");
    }
    void VerifyUsageRules(){
        var now=new DateTimeOffset(2026,10,10,12,0,0,TimeSpan.Zero);var cutoff=now.AddDays(-90);var app=new InstalledApp("Fixture game","Fixture",@"C:\Fixture\Game","1","fixture",1024,now.AddDays(-200));
        var old=new UsageRow(app,cutoff,"fixture","incomplete","possible");
        if(!UsageMatches(app,@"C:\Fixture\Game\game.exe")||UsageMatches(app,@"C:\Fixture\GameExtra\game.exe"))throw new InvalidOperationException("App identity path boundary failed");
        if(!PossibleUnused(old,cutoff)||PossibleUnused(old with{Last=cutoff.AddTicks(1)},cutoff)||PossibleUnused(old with{Last=null},cutoff)||PossibleUnused(old with{App=app with{InstalledAt=now.AddDays(-5)}},cutoff))throw new InvalidOperationException("Usage threshold/new-install handling failed");
        if(!PossibleUnused(old with{Last=cutoff.ToOffset(TimeSpan.FromHours(9))},cutoff)||!BackgroundExecutable(@"C:\Fixture\Game\updater.exe"))throw new InvalidOperationException("Timezone/helper handling failed");
        if(unusedDays.Minimum!=30||unusedDays.Value!=90||observeUsage.Checked)throw new InvalidOperationException("Usage defaults failed");
        usageRows=[old];unusedView.SelectedItem="Supported inactivity";RenderUnusedApps();if(unusedGrid.Rows.Count!=0)throw new InvalidOperationException("Incomplete history promoted to supported inactivity");
        File.WriteAllText("artifacts/usage-rules-success.txt",DateTimeOffset.Now.ToString("o")+" · identity boundaries, exact threshold, new installs, missing records, time zones, helper exclusion and conservative defaults passed.");
    }
}
