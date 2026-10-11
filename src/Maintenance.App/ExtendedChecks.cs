using System.Text.Json;
namespace Maintenance.App;
sealed partial class MainWindow
{
    void VerifyBenchmarkProfiles()
    {
        SetBusy(false);SelectPage(3);themeChoice.SelectedItem="Dark";
        foreach(var (profile,count) in new[]{("Quick benchmark",4),("Extended tests",8),("Quick benchmark",4),("Extended tests",8)}){
            diskProfile.SelectedItem=profile;Refresh();
            if(benchmarkCards.Count!=count||SelectedWorkloads.Length!=count||diskWork.Items.Count!=count+2||diskCards.RowCount!=count/2)throw new InvalidOperationException("Profile cards, picker and run selection differ");
            if(!DiskBenchmark.Workloads.All(w=>ActiveWorkloads.Contains(w)))throw new InvalidOperationException("Extended profile lost a quick workload");
            foreach(var work in ActiveWorkloads){diskWork.SelectedItem=work.Name;if(SelectedWorkloads.Length!=1||SelectedWorkloads[0]!=work)throw new InvalidOperationException("Individual workload selection failed");}
            diskWork.SelectedItem=AllWorkloadsLabel;
        }
        diskWork.SelectedItem=CustomWorkloads;customWorkloads.Clear();UpdateDiskEstimate();
        if(speedStartButton!.Enabled||SelectedWorkloads.Length!=0)throw new InvalidOperationException("Empty custom selection can start");
        benchmarkCards[0].ActivateSelection();benchmarkCards[5].ActivateSelection();
        if(SelectedWorkloads.Length!=2||!benchmarkCards[0].Chosen||!benchmarkCards[5].Chosen||!speedStartButton.Enabled)throw new InvalidOperationException("Custom card selection failed");
        benchmarkCards[0].ActivateSelection();if(SelectedWorkloads.Length!=1)throw new InvalidOperationException("Card deselection failed");
        speedCancel=new();UpdateCustomCards();benchmarkCards[1].ActivateSelection();if(SelectedWorkloads.Length!=1)throw new InvalidOperationException("Selection changed while running");speedCancel.Dispose();speedCancel=null;UpdateCustomCards();benchmarkCards[0].ActivateSelection();
        accentChoice.SelectedItem="Teal";Refresh();
        diskWork.PerformClick();if(diskWork.ActiveMenu is {} menu){using var image=new Bitmap(menu.Width,menu.Height);menu.DrawToBitmap(image,new Rectangle(Point.Empty,menu.Size));image.Save("artifacts/extended-all-tests-menu.png");menu.Close();}
        using(var image=new Bitmap(Width,Height)){DrawToBitmap(image,new Rectangle(0,0,Width,Height));image.Save("artifacts/extended-eight-cards.png");}
        File.WriteAllText("artifacts/profile-check-success.txt",DateTimeOffset.Now.ToString("o"));
    }
    async Task VerifyExtendedRevision(string fixture)
    {
        SetBusy(false);themeChoice.SelectedItem="Dark";SelectPage(3);diskProfile.SelectedItem="Extended tests";
        Refresh();
        if(benchmarkCards.Select(c=>c.Height).Distinct().Count()!=1||benchmarkCards[0].Height>230*DeviceDpi/96f)throw new InvalidOperationException("Unequal or oversized benchmark cards");
        var samples=new List<DiskMeasurement>();
        await Task.Run(()=>DiskBenchmark.Run(fixture,64L<<20,1,1,DiskBenchmark.Extended.Select(w=>w with{Duration=1}).ToArray(),new InlineProgress<string>(_=>{}),new InlineProgress<DiskMeasurement>(m=>samples.Add(m)),CancellationToken.None));
        if(samples.Count!=8||samples.Any(m=>m.Score.MBps<=0||m.Score.P95Microseconds<=0||m.Score.P99Microseconds<m.Score.P95Microseconds))throw new InvalidOperationException("Extended workload/latency results missing");
        if(Directory.GetFiles(fixture,".maintenance-benchmark-*").Length!=0)throw new InvalidOperationException("Benchmark fixture not removed");
        diskMeasurements.AddRange(samples);diskUnits.SelectedItem="P99 µs";RenderDiskResults();Refresh();Capture("extended-tests-preview");
        diskProfile.SelectedItem="Quick benchmark";Refresh();if(benchmarkCards.Any(c=>c.Workload.WritePercent.HasValue))throw new InvalidOperationException("Quick workloads changed");Capture("quick-tests-preview");
        var saved=preferences;
        var custom=new Preferences(saved.Measurements,new(){{StorageKinds.Labels[0],Color.OrangeRed.ToArgb()}});
        preferences=JsonSerializer.Deserialize<Preferences>(JsonSerializer.Serialize(custom))!;ApplyCategoryColors();
        if(StorageKinds.Colors[0].ToArgb()!=Color.OrangeRed.ToArgb())throw new InvalidOperationException("Custom palette roundtrip failed");
        preferences=new();ApplyCategoryColors();if(!StorageKinds.Colors.SequenceEqual(StorageKinds.DefaultColors))throw new InvalidOperationException("Palette reset failed");
        preferences=saved;ApplyCategoryColors();ShowSettings();
        SelectPage(4);historyTabs.SelectedIndex=2;Refresh();Capture("history-themed-preview");
        SelectPage(3);speedMode.SelectedItem="Internet speed";downloadGauge.Update(new(false,100,100,12345.6,true));Refresh();
        Capture("download-value-preview");
        File.WriteAllText("artifacts/extended-check-success.txt",DateTimeOffset.Now.ToString("o")+" · 8 real direction results; mixed ratios, latency, cleanup, compact layout, palette serialization/reset, history");
        void Capture(string name){using var capture=new Bitmap(Width,Height);DrawToBitmap(capture,new Rectangle(0,0,Width,Height));capture.Save("artifacts/"+name+".png");}
    }
}


