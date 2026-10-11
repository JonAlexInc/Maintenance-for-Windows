namespace Maintenance.App;
sealed partial class MainWindow
{
    async Task VerifyStartupUi(){SetBusy(false);SelectPage(7);themeChoice.SelectedItem="Dark";await ReadStartup();
        if(!startupEntries.Any(r=>r.Kind=="Services"))throw new InvalidOperationException("Service inventory missing: "+startupSummary.Text);
        var fixture=new StartupEntry("Services","Fixture","Fixture","Stopped","Auto",@"C:\Fixture\fixture.exe","Fixture only","LocalSystem","","",true,false,0);
        if(StartupTools.Protected(fixture)||!StartupTools.Protected(fixture with{Location=Path.Combine(Environment.SystemDirectory,"svchost.exe")})||!StartupTools.Protected(fixture with{Dependents=1})||!StartupTools.Protected(fixture with{Mode="Boot"}))throw new InvalidOperationException("Service guards failed");
        if(!StartupTools.Protected(fixture with{Kind="Scheduled tasks",Location=@"\Microsoft\Windows\",Definition="fixture"}))throw new InvalidOperationException("Task guard failed");
        var microsoft=fixture with{Name="Spooler",Publisher="Microsoft Corporation"};
        if(!StartupDescriptions.IsMicrosoft(microsoft)||StartupDescriptions.IsMicrosoft(fixture)||StartupDescriptions.IsMicrosoft(fixture with{Publisher="Microsoft Tools Example Company"})||!StartupDescriptions.Purpose(microsoft).Contains("print jobs"))throw new InvalidOperationException("Publisher or explanation classification failed");
        var actual=startupEntries;startupEntries=[microsoft,fixture,fixture with{Name="Third party",Publisher="Example Company"}];startupTabs.SelectedIndex=1;
        hideMicrosoftStartup.Checked=true;RenderStartup();if(startupGrids[1].Rows.Count!=2)throw new InvalidOperationException("Microsoft filter hid unknown or third-party rows");
        startupFind.Text="Example Company";if(startupGrids[1].Rows.Count!=1)throw new InvalidOperationException("Publisher search failed");startupFind.Clear();
        hideMicrosoftStartup.Checked=false;if(startupGrids[1].Rows.Count!=3)throw new InvalidOperationException("Unhide failed");startupEntries=actual;RenderStartup();
        if(!actual.Any(r=>r.Publisher!="Unknown"))throw new InvalidOperationException("No publisher metadata collected");
        foreach(int index in new[]{0,1,2}){startupTabs.SelectedIndex=index;await Task.Delay(300);Refresh();using var image=new Bitmap(Width,Height);DrawToBitmap(image,new Rectangle(0,0,Width,Height));image.Save($"artifacts/startup-{index}.png");}
        hideMicrosoftStartup.Checked=true;startupTabs.SelectedIndex=1;await Task.Delay(300);Refresh();using(var filtered=new Bitmap(Width,Height)){DrawToBitmap(filtered,new Rectangle(0,0,Width,Height));filtered.Save("artifacts/startup-third-party.png");}
        File.WriteAllText("artifacts/startup-check-success.txt",DateTimeOffset.Now.ToString("o")+$" · Read-only inventory: {startupEntries.Count} rows; tab navigation and protection guards passed. No host settings changed.");
    }
}
