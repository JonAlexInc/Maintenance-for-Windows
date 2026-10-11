using Maintenance.Core;
using Microsoft.Win32;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace Maintenance.App;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        bool smoke = args.Contains("--smoke-test");
        if(smoke)Application.ThreadException+=(_,e)=>{Directory.CreateDirectory("artifacts");File.WriteAllText("artifacts/ui-smoke-failure.txt",e.Exception.ToString());Environment.Exit(1);};
        if(!smoke&&!args.Contains("--standard-user")&&!new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator)) {
            try { Process.Start(new ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=true,Verb="runas",Arguments="--standard-user"});return; }
            catch(System.ComponentModel.Win32Exception e) { if(e.NativeErrorCode!=1223)MessageBox.Show("Administrator launch was unavailable. Continuing with current-user cleanup sources.\n"+e.Message); }
        }
        using var instance = new Mutex(true, "Local\\MaintenanceForWindows-" + WindowsIdentity.GetCurrent().User?.Value + (smoke ? "-smoke" : ""), out var first);
        if (!first) { MessageBox.Show("Maintenance for Windows is already running."); return; }
        if (Environment.OSVersion.Version.Build < 22000 || RuntimeInformation.OSArchitecture != Architecture.X64)
        { MessageBox.Show("This preview requires Windows 11 x64."); return; }
        try { Application.Run(new MainWindow(smoke)); }
        catch (Exception e) { MessageBox.Show("Maintenance could not continue. No further cleanup will run.\n\n" + e.Message, "Maintenance for Windows"); }
    }
}

sealed partial class MainWindow : Form
{
    readonly Panel content = new() { Dock = DockStyle.Fill };
    readonly List<Panel> pages = [];
    readonly Label status = new() { AutoSize = false, Dock = DockStyle.Bottom, Height = 42, Padding = new(16, 10, 0, 0), Text = "Ready. Scanning does not delete files." };
    readonly FileListView files = new() { Dock = DockStyle.Fill, View = View.Details, CheckBoxes = true, FullRowSelect = true, HideSelection = false };
    readonly TextBox details = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false, Dock = DockStyle.Bottom, Height = 145, Visible = false };
    readonly ListBox exclusionsList = new() { Dock = DockStyle.Fill };
    readonly ListBox history = new() { Dock = DockStyle.Fill };
    readonly Label scanSummary = new() { Dock = DockStyle.Top, Height = 64, Padding = new(0, 8, 0, 0), Text = "Scan to review temporary files. Nothing is selected automatically.\nOnly files created and last modified at least seven days ago qualify." };
    readonly List<Button> mutationButtons = [];
    readonly LocalStore store;
    MultiCleanupEngine? engine;
    ScanResult? scan;
    CancellationTokenSource? cancel;
    List<string> exclusions = [];
    bool busy;
    readonly bool smoke;
    public MainWindow(bool smoke)
    {
        this.smoke = smoke;
        if(Environment.ProcessPath is {} executable)Icon=Icon.ExtractAssociatedIcon(executable);
        Text = "Maintenance for Windows — 0.01 Preview"; Width = Math.Min(1760, Screen.PrimaryScreen!.WorkingArea.Width - 60); Height = Math.Min(1120, Screen.PrimaryScreen!.WorkingArea.Height - 60); MinimumSize = new(1280, 900);
        Font = new("Segoe UI", 9); StartPosition = FormStartPosition.CenterScreen;
        store = new(smoke ? Path.GetFullPath(".test-data/ui-smoke") : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MaintenanceForWindows"));
        exclusions = store.Exclusions(); // Fail closed if saved exclusions cannot be read.
        try { engine = new MultiCleanupEngine([CleanupEngine.ForCurrentUser().Root]); } catch (Exception e) { status.Text = e.Message; }
        LoadPreferences();BuildUi();
        RefreshExclusions(); RefreshHistory(); RefreshOverview();
        if (smoke) SetBusy(true);
        FormClosing += (_, e) => { if (busy) { cancel?.Cancel(); e.Cancel = true; status.Text = "Stopping safely. Close again after the operation finishes."; } };
        if (smoke) Shown += (_, _) =>
        {
            var timer = new System.Windows.Forms.Timer { Interval = 900 };
            timer.Tick += async (_, _) =>
            {
                timer.Stop(); Directory.CreateDirectory("artifacts");
                if(Environment.GetCommandLineArgs().Contains("--compact-cleanup-check")){await VerifyCompactCleanup();Close();return;}
                if(Environment.GetCommandLineArgs().Contains("--stop-all-check")){await VerifyEmergencyStop();Close();return;}
                if(Environment.GetCommandLineArgs().Contains("--focused-results-check")){await VerifyFocusedResults();Close();return;}
                if(Environment.GetCommandLineArgs().Contains("--feedback-check")){await VerifyFeedbackUi();Close();return;}
                if(Environment.GetCommandLineArgs().Contains("--app-map-check")){await VerifyAppDataMap();Close();return;}
                if(Environment.GetCommandLineArgs().Contains("--full-ui-check")){await VerifyFullUi();Close();return;}
                if(Environment.GetCommandLineArgs().Contains("--duplicates-check")){await VerifyDuplicatesUi();Close();return;}
                if(Environment.GetCommandLineArgs().Contains("--startup-check")){await VerifyStartupUi();Close();return;}
                if(Environment.GetCommandLineArgs().Contains("--modern-check")){await VerifyModernUi();Close();return;}
                if(Environment.GetCommandLineArgs().Contains("--updates-check")){VerifyUpdatesPage();Close();return;}
                if(Environment.GetCommandLineArgs().Contains("--profile-check")){VerifyBenchmarkProfiles();Close();return;}
                if(Environment.GetCommandLineArgs().Contains("--internet-check")){await VerifyInternetTests();Close();return;}
                if(Environment.GetCommandLineArgs().Contains("--capacity-preview")){CaptureCapacityPreview();SetBusy(false);Close();return;}
                if(Environment.GetCommandLineArgs().Contains("--extended-check")){await VerifyExtendedRevision(Directory.CreateDirectory(Path.Combine(".test-data","extended-"+Guid.NewGuid().ToString("N"))).FullName);SetBusy(false);Close();return;}
                if(Environment.GetCommandLineArgs().Contains("--system-layout-check")){
                    SetBusy(false);SelectPage(5);await RefreshSystem();themeChoice.SelectedItem="Dark";hardwareTabs.SelectedIndex=10;Refresh();
                    var before=hardwareNavigation.Bounds;UpdateUptime();PerformLayout();
                    if(before!=hardwareNavigation.Bounds||uptimeLabel.Parent?.Tag as string!=CardRole)throw new InvalidOperationException("Uptime must stay in a card without moving navigation");
                    using(var capture=new Bitmap(Width,Height)){DrawToBitmap(capture,new Rectangle(0,0,Width,Height));capture.Save("artifacts/system-layout-preview.png");}
                    RenderSystem();UpdateUptime();if(uptimeLabel.IsDisposed)throw new InvalidOperationException("Uptime lost during refresh");
                    File.WriteAllText("artifacts/system-layout-success.txt",DateTimeOffset.Now.ToString("o"));Close();return;
                }
                if(Environment.GetCommandLineArgs().Contains("--internet-check")) {
                    using var netLimit=new CancellationTokenSource(TimeSpan.FromMinutes(2));
                    var netResult=await SpeedRunner.Download("https://was1-vin.speedtest.network.ovh.net/files/100Mb.dat",new Progress<string>(),netLimit.Token);
                    File.WriteAllText("artifacts/internet-validation.txt",$"App download completed: {netResult.Bytes} bytes; {netResult.Seconds:F2}s; {netResult.Rate*8:F2} Mbps. Target: OVHcloud Virginia. No upload.");
                }
                var fixture = Path.GetFullPath(Path.Combine(".test-data", "ui-" + Guid.NewGuid().ToString("N")));
                Directory.CreateDirectory(fixture);
                foreach (var name in new[] { "sample-export.tmp", "sample-cache.tmp" })
                {
                    var path = Path.Combine(fixture, name); File.WriteAllText(path, new string('x', name.Contains("export") ? 100 : 9));
                    File.SetCreationTimeUtc(path, DateTime.UtcNow.AddDays(-10)); File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddDays(-10));
                }
                if(Environment.GetCommandLineArgs().Contains("--selection-check")){
                    SetBusy(false);SelectPage(1);themeChoice.SelectedItem="Dark";await ScanLocalDrive(fixture);await renderTask;groupFolders.Checked=true;await renderTask;enableFileSelection.Checked=true;Refresh();
                    using(var capture=new Bitmap(Width,Height)){DrawToBitmap(capture,new Rectangle(0,0,Width,Height));capture.Save("artifacts/selection-checkbox-preview.png");}
                    if(!files.CheckBoxes||!files.ClickFirstGroupCheckbox()||files.CheckedItems.Count!=2)throw new InvalidOperationException("Folder checkbox hit target did not select both visible fixture files.");
                    enableFileSelection.Checked=false;if(files.CheckBoxes||files.CheckedItems.Count!=0||cleanButton.Enabled)throw new InvalidOperationException("Selection off did not clear/disable actions.");
                    enableFileSelection.Checked=true;Refresh();if(!files.CheckBoxes||!files.GroupCheckboxes)throw new InvalidOperationException("Selection did not re-enable.");
                    files.Items[0].Selected=true;
                    foreach(string color in new[]{"Teal","Violet"}){
                        accentChoice.SelectedItem=color;Refresh();
                        if(vmGrid.DefaultCellStyle.SelectionBackColor!=accent||enableFileSelection.Accent!=accent||files.SelectionAccent!=accent)throw new InvalidOperationException("Selection accent did not propagate.");
                        using var themed=new Bitmap(Width,Height);DrawToBitmap(themed,new Rectangle(0,0,Width,Height));themed.Save("artifacts/selection-"+color.ToLowerInvariant()+".png");
                    }
                    if(enableFileSelection.Width<enableFileSelection.GetPreferredSize(Size.Empty).Width)throw new InvalidOperationException("Selection label is clipped.");
                    CaptureCapacityPreview();File.WriteAllText("artifacts/selection-check-success.txt",DateTimeOffset.Now.ToString("o"));SetBusy(false);Close();return;
                }
                if(Environment.GetCommandLineArgs().Contains("--revision-check-only")){
                    SetBusy(false);SelectPage(1);await RefreshSystem();await VerifyNewViews(fixture);
                    if(Environment.GetCommandLineArgs().Contains("--storage-check-f")){
                        var usage=await Task.Run(()=>StorageMeasure.Read(@"F:\",CancellationToken.None));
                        File.WriteAllText("artifacts/storage-complete-check.json",System.Text.Json.JsonSerializer.Serialize(new{storageRoot=@"F:\",usage.Entries,usage.Skipped,usage.Partial,Categories=StorageKinds.Labels.Select((name,i)=>new{name,bytes=usage.Values[i]})}));
                    }
                    File.WriteAllText("artifacts/revision-ui-success.txt",DateTimeOffset.Now.ToString("o"));Close();return;
                }
                var tempRoot=Path.GetPathRoot(Path.GetTempPath())!;
                if(SpeedRunner.DefaultFolder(tempRoot)==tempRoot||Path.GetPathRoot(SpeedRunner.DefaultFolder(tempRoot))!=tempRoot)throw new InvalidOperationException("Writable speed default left selected volume or used protected root.");
                var speedFixture=Path.Combine(fixture,"speed");Directory.CreateDirectory(speedFixture);
                var speedResult=await Task.Run(()=>SpeedRunner.FileTest(speedFixture,8_000_000,new Progress<string>(),CancellationToken.None));
                if(speedResult.Write<=0||speedResult.Read<=0||Directory.GetFiles(speedFixture).Length!=0)throw new InvalidOperationException("Speed test or own-file cleanup failed.");
                using(var speedStop=new CancellationTokenSource()) {
                    speedStop.CancelAfter(1);
                    try {await Task.Run(()=>SpeedRunner.FileTest(speedFixture,256_000_000,new Progress<string>(),speedStop.Token));throw new InvalidOperationException("Speed cancellation failed.");}catch(OperationCanceledException){}
                    if(Directory.GetFiles(speedFixture).Length!=0)throw new InvalidOperationException("Canceled speed test left a file.");
                }
                var benchmarkSamples=new List<DiskMeasurement>();
                await Task.Run(()=>DiskBenchmark.Run(speedFixture,64L<<20,1,1,DiskBenchmark.Workloads,new Progress<string>(),new InlineProgress<DiskMeasurement>(m=>benchmarkSamples.Add(m)),CancellationToken.None));
                if(benchmarkSamples.Count!=8||benchmarkSamples.Any(m=>m.Score.MBps<=0||m.Score.Iops<=0)||Directory.GetFiles(speedFixture).Length!=0)throw new InvalidOperationException($"Disk benchmark validation failed: {benchmarkSamples.Count}/8 samples; {Directory.GetFiles(speedFixture).Length} files remain.");
                using(var benchmarkStop=new CancellationTokenSource()) {
                    var cancelProgress=new Progress<string>(s=>{if(s.Contains("pass 1/1"))benchmarkStop.Cancel();});
                    try {await Task.Run(()=>DiskBenchmark.Run(speedFixture,64L<<20,1,5,[DiskBenchmark.Workloads[0]],cancelProgress,new Progress<DiskMeasurement>(),benchmarkStop.Token));throw new InvalidOperationException("Native benchmark cancellation failed.");}catch(OperationCanceledException){}
                    if(Directory.GetFiles(speedFixture).Length!=0)throw new InvalidOperationException("Native benchmark cancellation left test file.");
                }
                diskMeasurements.AddRange(benchmarkSamples);RenderDiskResults();
                foreach(var unit in new[]{"GB/s","IOPS","µs","MB/s"}){diskUnits.SelectedItem=unit;if(benchmarkCards.Count!=4||benchmarkCards[0].Samples.Length==0)throw new InvalidOperationException("Benchmark unit conversion failed.");}
                File.WriteAllText("artifacts/benchmark-validation.txt","DiskSpd 2.3: all four workload read/write phases passed on a generated 64 MiB file, 1-second measurement + 1-second warmup. Eight measurements parsed; all four unit displays checked; native-process cancellation and own-file removal passed. Real SMB share not tested.");
                var gpuLookup=DeviceIdentity.Lookup(@"PCI\VEN_10DE&DEV_1B80&SUBSYS_85981043");
                if(!gpuLookup.Any(r=>r.Label=="Board vendor (subsystem ID)"&&r.Value.Contains("ASUSTeK")))throw new InvalidOperationException("PCI board lookup failed.");
                if(DeviceIdentity.Lookup(@"HID\VID_046D&PID_C539").Single().Value!="046D:C539")throw new InvalidOperationException("USB ID parsing failed.");
                File.WriteAllText("artifacts/speed-validation.txt","8 MB generated-file write/read passed; test file removed; cancellation and cleanup passed; PCI board and USB identifiers passed. No network share or full internet transfer measured.");
                await RefreshSystem();
                if(systemFacts.Any(f=>f.Section is "Services" or "Startup entries"))throw new InvalidOperationException("Unwanted software inventory present.");
                if(ReadableProperty("NumberOfCores")!="CPU cores" || UsefulValue("System Product Name"))throw new InvalidOperationException("Friendly system labels failed.");
                if (systemFacts.Count == 0) throw new InvalidOperationException("System inventory is empty.");
                var enriched=HardwareView.Prepare(systemFacts);HardwareSpecs.Add(enriched);
                File.WriteAllText("artifacts/hardware-presented.json",System.Text.Json.JsonSerializer.Serialize(enriched));
                if(HardwareView.GpuTitle("NVIDIA GeForce RTX 5080",@"PCI\VEN_10DE&DEV_2C02&SUBSYS_89E01043","generic")!="ASUS GeForce RTX 5080")throw new InvalidOperationException("GPU friendly name failed.");
                var fake=HardwareView.Prepare(new[]{new SystemFact("Mouse","a","ContainerId","test"),new SystemFact("Mouse","a","BusReportedName","Example Keyboard"),new SystemFact("Mouse","a","PNPDeviceID","a"),new SystemFact("Keyboard","b","ContainerId","test"),new SystemFact("Keyboard","b","PNPDeviceID","b")});
                if(fake.Any(f=>f.Section=="Mouse")||fake.Count(f=>f.Section=="Keyboard"&&f.Property=="Name")!=1)throw new InvalidOperationException("Composite keyboard grouping failed.");
                if(StorageMeasure.Category("C:\\",@"C:\Program Files\Steam\steamapps\common\Test\data.bin")!=6||StorageMeasure.Category("C:\\",@"C:\SteamLibrary\steamapps\shadercache\file.bin")!=11||StorageKinds.Personal(@"C:\Users\Test\project\node_modules\lib\index.js")!=9)throw new InvalidOperationException("Storage recognition failed.");
                RenderSystem();if(hardwareTabs.TabPages.Count!=hardwareKinds.Length)throw new InvalidOperationException("Hardware tabs missing.");
                systemSearch.Text="CPU";systemSearchDelay.Stop();RenderSystem();
                if(hardwarePanels[0].Controls.Count==0)throw new InvalidOperationException("Hardware search failed.");
                if(!SystemExport().Contains("CreationClassName"))throw new InvalidOperationException("Full inventory export lost raw properties.");
                systemSearch.Clear();systemSearchDelay.Stop();RenderSystem();
                UpdateLiveMetrics();
                engine = new MultiCleanupEngine([fixture]); await Scan();
                // Exercise menu close-before-click ordering, which direct property changes miss.
                foreach (var choice in new[] { themeChoice, accentChoice })
                {
                    choice.PerformClick();
                    var menu = choice.ActiveMenu!;
                    var option = (ToolStripMenuItem)menu.Items[1];
                    menu.Close(); option.PerformClick();
                    await Task.Delay(50);
                    if (choice.SelectedItem != option.Text) throw new InvalidOperationException("Appearance menu choice failed.");
                }
                files.Items[0].Checked = true;
                nameFilter.Text = "sample-cache"; filterDelay.Stop(); RebuildFiles(false);
                if (files.Items.Count != 1 || files.CheckedItems.Count != 0) throw new InvalidOperationException("Filtering did not clear hidden selections.");
                nameFilter.Clear(); filterDelay.Stop(); RebuildFiles(false);
                minSize.Value = 1; filterDelay.Stop(); RebuildFiles(false);
                if (files.Items.Count != 0) throw new InvalidOperationException("Minimum size filter failed.");
                minSize.Value = 0; filterDelay.Stop(); RebuildFiles(false);
                sortColumn = 0; sortDescending = true; RebuildFiles(true);
                if (files.Items[0].Text != "sample-export.tmp") throw new InvalidOperationException("Descending name sort failed.");
                sortDescending = false; RebuildFiles(true);
                if (files.Groups.Count != 1 || files.Items[0].SubItems[1].Text != ((Candidate)files.Items[0].Tag!).Path) throw new InvalidOperationException("Folder groups or exact paths failed.");
                sortColumn = 2; sortDescending = true; RebuildFiles(true);
                if (((Candidate)files.Items[0].Tag!).Size != 100) throw new InvalidOperationException("Numeric size sort failed.");
                sortColumn = 0; sortDescending = false; RebuildFiles(true);
                foreach (var theme in new[] { "Light", "Dark", "Follow Windows" })
                {
                    themeChoice.SelectedItem = theme;
                    for (var i = 0; i < pages.Count; i++)
                    {
                        SelectPage(i); Refresh();
                        using var bitmap = new Bitmap(Width, Height); DrawToBitmap(bitmap, new Rectangle(0, 0, Width, Height));
                        bitmap.Save($"artifacts/ui-{theme.Replace(" ", "-")}-{i}.png");
                    }
                }
                SelectPage(3);speedMode.SelectedItem="Network folder";
                if(!speedFolder.Visible||!speedTarget.Items.Contains("Browse or enter a network path"))throw new InvalidOperationException("Network folder controls missing.");
                Refresh();using(var networkImage=new Bitmap(Width,Height)){DrawToBitmap(networkImage,new Rectangle(0,0,Width,Height));networkImage.Save("artifacts/ui-network-picker.png");}
                speedMode.SelectedItem="Internet download";Refresh();using(var internetImage=new Bitmap(Width,Height)){DrawToBitmap(internetImage,new Rectangle(0,0,Width,Height));internetImage.Save("artifacts/ui-internet.png");}
                speedMode.SelectedItem="Local disk";
                themeChoice.SelectedItem = "Dark"; accentChoice.SelectedItem = "Teal"; Size = MinimumSize; SelectPage(3); Refresh();
                using(var cardsImage=new Bitmap(Width,Height)){DrawToBitmap(cardsImage,new Rectangle(0,0,Width,Height));cardsImage.Save("artifacts/ui-cards-small.png");}
                SelectPage(1); Refresh();
                LoadAppearance();
                if (themeChoice.Text != "Dark" || accentChoice.Text != "Teal") throw new InvalidOperationException("Appearance did not persist.");
                files.Items[0].Checked = true; UpdateActions();
                if (!cleanButton.Enabled || !selectedSummary.Text.StartsWith("1 selected")) throw new InvalidOperationException("Selection actions did not update.");
                files.Items[0].Checked = false; UpdateActions();
                if (cleanButton.Enabled) throw new InvalidOperationException("Cleanup enabled without a selection.");
                using (var small = new Bitmap(Width, Height)) { DrawToBitmap(small, new Rectangle(0, 0, Width, Height)); small.Save("artifacts/ui-small-dark-teal.png"); }
                themeChoice.SelectedItem = "Light"; accentChoice.SelectedItem = "Blue";
                SelectPage(5); RenderSystem();
                foreach(int tabIndex in new[]{0,1,2,3,5,6,9,10}){hardwareTabs.SelectedIndex=tabIndex;Refresh();using var hardwareImage=new Bitmap(Width,Height);DrawToBitmap(hardwareImage,new Rectangle(0,0,Width,Height));hardwareImage.Save($"artifacts/hardware-tab-{tabIndex}.png");}
                hardwareTabs.SelectedIndex=3; Size=new(1760,1120); Refresh();
                using(var systemImage=new Bitmap(Width,Height)) { DrawToBitmap(systemImage,new Rectangle(0,0,Width,Height)); systemImage.Save("artifacts/ui-system-expanded.png"); }
if(!PanelDimensions("70 x 40 cm",false).Contains("in")||!PanelDimensions("70 x 40 cm",true).Contains("cm"))throw new InvalidOperationException("Display unit conversion failed");
if(!systemFacts.Any(f=>f.Section=="Windows overview"&&f.Property=="Full OS build"))throw new InvalidOperationException("Windows details missing");
UpdateUptime();if(uptimeLabel.AccessibleName!="Live system uptime"||uptimeLabel.Text.Length==0)throw new InvalidOperationException("Uptime missing");
var projectFixture=Path.Combine(fixture,"Development","Example");Directory.CreateDirectory(projectFixture);File.WriteAllText(Path.Combine(projectFixture,"bundle.bin"),"project artifact");
var projectUsage=StorageMeasure.Read(projectFixture,CancellationToken.None);
if(!StorageKinds.ProjectDirectory(projectFixture,["package.json"])||StorageMeasure.Category(@"F:\",@"F:\Development\project\disk.vhdx",null,true)!=9)throw new InvalidOperationException("Project classification failed");
File.Delete(Path.Combine(projectFixture,"bundle.bin"));
                var measured = StorageMeasure.Read(fixture, CancellationToken.None);
                if (measured.Other != 109 || measured.Partial) throw new InvalidOperationException("Storage category fixture measurement failed.");
                if (FormatBytes(2048) != "2.0 KB" || FormatBytes(3L*1024*1024*1024) != "3.0 GB") throw new InvalidOperationException("Readable file sizes failed.");
                var simulated = new RunResult(DateTimeOffset.Now, [new Outcome(scan!.Candidates[0].Path,"Deleted","Simulated result only",9),new Outcome(scan.Candidates[1].Path,"Skipped","Simulated locked file",0)],1000,1009,false);
                RecordCleanup(simulated,2); RebuildFiles(false);
                if(files.Items.Count!=2 || cleanButton.Enabled || files.CheckBoxes) throw new InvalidOperationException("Retained cleanup results failed.");
                Size = new(1760,1120); themeChoice.SelectedItem="Dark"; SelectPage(1); Refresh();
                using(var resultImage=new Bitmap(Width,Height)) { DrawToBitmap(resultImage,new Rectangle(0,0,Width,Height)); resultImage.Save("artifacts/ui-cleanup-results.png"); }
                ClearResults(); if(files.Items.Count!=0 || resultsMode) throw new InvalidOperationException("Clear results failed.");
                if (IsLocalStorage(DriveType.Network) || IsLocalStorage(DriveType.Unknown)) throw new InvalidOperationException("Nonlocal drive included.");
                string[] categoryPaths=["Windows/a.dll","Program Files/App/a.exe","Users/Test/AppData/Local/App/a.bin","Users/Test/Documents/a.txt","Users/Test/AppData/Local/Temp/a.tmp","other.bin"];
                for(int category=0;category<categoryPaths.Length;category++)
                    if(StorageMeasure.Category(fixture,Path.Combine(fixture,categoryPaths[category]))!=new[]{0,1,2,8,4,5}[category])throw new InvalidOperationException("Storage category classification failed.");
                var catalog=DriveCatalog.Read(fixture,CancellationToken.None,null);
                if(catalog.Candidates.Count!=2 || catalog.Limited)throw new InvalidOperationException("Drive catalog failed.");
                await ScanLocalDrive(fixture);
                if(!driveReview || cleanButton.Enabled || files.CheckBoxes)throw new InvalidOperationException("Drive review permitted deletion.");
                enableFileSelection.Checked=true;groupFolders.Checked=true;RebuildFiles(false);await renderTask;
files.ToggleGroup(files.Groups[0]);UpdateActions();
if(files.CheckedItems.Count!=2||!cleanButton.Enabled||!files.GroupCheckboxes)throw new InvalidOperationException("Personal folder checkbox selection failed.");
SelectPage(1);Refresh();using(var selectionImage=new Bitmap(Width,Height)){DrawToBitmap(selectionImage,new Rectangle(0,0,Width,Height));selectionImage.Save("artifacts/ui-folder-selection.png");}
files.ToggleGroup(files.Groups[0]);UpdateActions();if(files.CheckedItems.Count!=0)throw new InvalidOperationException("Group deselection failed.");
enableFileSelection.Checked=false;
string recycleProbe=Path.Combine(fixture,"recycle-probe-"+Guid.NewGuid().ToString("N")+".txt");File.WriteAllText(recycleProbe,"Generated recycle validation fixture");
var recycleCandidate=PersonalReview.Snapshot(recycleProbe);
var recycled=await RecycleShell.OnSta(()=>PersonalReview.Recycle(fixture,[recycleCandidate],[],CancellationToken.None,_=>{},RecycleShell.Move));
if(recycled.Outcomes.Single().Status!="Recycled"||File.Exists(recycleProbe))throw new InvalidOperationException("Generated-file recycle failed: "+recycled.Outcomes.Single().Reason);
dynamic recycleShell=Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application")!)!;
dynamic bin=recycleShell.NameSpace(10);bool foundInBin=false;
foreach(var entry in bin.Items()){if((string)entry.Name==Path.GetFileName(recycleProbe)){foundInBin=true;break;}}
if(!foundInBin)throw new InvalidOperationException("Recycled fixture was not found in Windows Recycle Bin.");
File.WriteAllText("artifacts/recycle-validation.txt","Generated file recycled via Windows shell; confirmed present in Recycle Bin. No user files touched. Group checkbox selects/deselects current visible group; personal recycle button enabled only with opt-in selection.");
                if(files.Columns[1].Width<TextRenderer.MeasureText(catalog.Candidates[0].Path,files.Font).Width)throw new InvalidOperationException("Full-path column not fitted.");
                ClearResults();
                var many=Enumerable.Range(0,1000000).Select(n=>new Candidate(Path.Combine(fixture,$"bulk-{n:D7}.tmp"),n,DateTime.UtcNow.ToFileTimeUtc(),DateTime.UtcNow.ToFileTimeUtc(),0,0,0,0)).ToList();
                scan=new ScanResult(DateTimeOffset.Now,many,[],false,false);driveReview=true;files.CheckBoxes=false;
                int heartbeats=0;using var heartbeat=new System.Windows.Forms.Timer {Interval=20};heartbeat.Tick+=(_,_)=>heartbeats++;heartbeat.Start();
                var performance=Stopwatch.StartNew();RebuildFiles(false);await renderTask;heartbeat.Stop();
                if(files.Items.Count!=500 || filteredRows.Count!=1000000 || heartbeats==0)throw new InvalidOperationException("Million-file paging blocked the UI or lost results.");
                RefreshHistory();UpdateActions();if(!nextFiles.Enabled)throw new InvalidOperationException("Paging disabled after state refresh.");
                nextFiles.PerformClick();if(filePage!=1 || files.Items.Count!=500)throw new InvalidOperationException("Next result page failed.");
                MoveFilePage(1999);if(filePage!=1999 || files.Items.Count!=500)throw new InvalidOperationException("Last-page jump failed.");
                if(!scanLocation.Items.Contains("Downloads") || !scanLocation.Items.Contains("Desktop") || scanDrives.Values.Any(p=>p==Path.GetPathRoot(p)))throw new InvalidOperationException("Focused review locations failed.");
                nameFilter.Text="bulk-0999999.tmp";filterDelay.Stop();RebuildFiles(false);await renderTask;
                if(files.Items.Count!=1)throw new InvalidOperationException("Filtering did not search all million results.");
                File.WriteAllText("artifacts/large-list-validation.txt",$"1,000,000 synthetic records; 500 displayed per page; preparation {performance.Elapsed.TotalSeconds:F2}s; {heartbeats} UI heartbeats; next page and full-dataset search passed. No files created or deleted for this stress check.");
                ClearResults();
                scanLocation.SelectedItem=StreamsMode;await ScanStreams(fixture);await renderTask;
if(!streamReview||cleanButton.Enabled||files.CheckBoxes||chooseScanTarget?.Text!="Choose drive…")throw new InvalidOperationException("Read-only stream mode failed.");
scanLocation.SelectedItem="Downloads";if(chooseScanTarget!.Visible)throw new InvalidOperationException("Downloads showed custom folder picker.");
scanLocation.SelectedItem=TempMode;
await VerifyNewViews(fixture);
if(cleanupSources.AutoScroll||sourceChecks.Any(s=>s.Check.Bottom>cleanupSources.ClientSize.Height))throw new InvalidOperationException("Cleanup source grid clips sources.");
if(Environment.GetCommandLineArgs().Contains("--storage-check")||Environment.GetCommandLineArgs().Contains("--storage-check-f")) {
    string storageRoot=Environment.GetCommandLineArgs().Contains("--storage-check-f")?@"F:\":@"C:\";
    var scanProgress=new Progress<StorageMeasure>(p=>File.WriteAllText("artifacts/storage-live-check.json",System.Text.Json.JsonSerializer.Serialize(new{storageRoot,p.Entries,p.Skipped,Development=p.Values[9],Total=p.Values.Sum()})));
    var realUsage=await Task.Run(()=>StorageMeasure.Read(storageRoot,CancellationToken.None,scanProgress));
    File.WriteAllText("artifacts/storage-complete-check.json",System.Text.Json.JsonSerializer.Serialize(new{storageRoot,realUsage.Entries,realUsage.Skipped,realUsage.Partial,Categories=StorageKinds.Labels.Select((name,i)=>new{name,bytes=realUsage.Values[i]})}));
}
                using(var demoAudit=store.StartJournal()) {
 LocalStore.Append(demoAudit,new{Summary="Scan UI audit fixture (simulated)",Kind="Temporary files",Location=fixture});
 LocalStore.Append(demoAudit,new Outcome(Path.Combine(fixture,"audit-deleted.tmp"),"Pending","Simulated request",8192));
 LocalStore.Append(demoAudit,new Outcome(Path.Combine(fixture,"audit-deleted.tmp"),"Deleted","Simulated outcome; no actual file deletion",8192));
 LocalStore.Append(demoAudit,new Outcome(Path.Combine(fixture,"audit-recycled.txt"),"Recycled","Simulated outcome",16384));
 LocalStore.Append(demoAudit,new Outcome(Path.Combine(fixture,"audit-unknown.tmp"),"Pending","Simulated incomplete request",1024));
}
RefreshHistory();SelectPage(4);
if(navigation[4].Text!="History"||navigation[5].Text!="System"||historyTabs.TabPages.Count!=5)throw new InvalidOperationException("History navigation/tabs failed");
historyFind.Text="audit-";
if(removedHistoryGrid.Rows.Count<2||otherHistoryGrid.Rows.Count<1)throw new InvalidOperationException("History outcome tabs/search failed");
for(int tab=0;tab<4;tab++){historyTabs.SelectedIndex=tab;Refresh();using var historyImage=new Bitmap(Width,Height);DrawToBitmap(historyImage,new Rectangle(0,0,Width,Height));historyImage.Save($"artifacts/history-tab-{tab}.png");}
historyFind.Clear();
                File.WriteAllText("artifacts/ui-smoke.txt", "Opened all pages under Light, Dark and Follow Windows; scanned two generated fixture files; rendered minimum-size Dark/Teal view. No real temporary files scanned; no cleanup executed.");
                Close();
            };
            timer.Start();
        };
    }
    Button Button(string text, Action action) => Button(text, () => { action(); return Task.CompletedTask; });
    Button Button(string text, Func<Task> action)
    {
        var b = new AppearanceButton { Text = text, Font = Font, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new(8, 5, 8, 5) };
        b.Click += async (_, _) => { if(emergencyStopping&&(b.Tag as string==StartRole||b.Text.StartsWith("Scan ")||b.Text.StartsWith("Refresh ")))return;try { await action(); } catch (Exception e) { MessageBox.Show(this, "Operation stopped. Review History before trying again.\n\n" + e.Message); } };
        mutationButtons.Add(b); StyleButton(b); return b;
    }
    void SetBusy(bool value) { busy = value; foreach(var stop in scanCancelButtons)stop.Enabled=value; foreach(var browser in browserChecks)browser.Check.Enabled=!value; foreach(var source in sourceChecks)source.Check.AutoCheck=!value&&source.Allowed; foreach (var b in mutationButtons) b.Enabled = !value; files.Enabled = !value; UpdateActions(); UpdateDuplicateActions(); }
    async Task Scan()
    {
        if(scanLocation.Text==RestoreMode){await ScanRestore();return;}
        if(scanLocation.Text==UpdateMode){await ScanUpdateStorage();return;}
        if(scanLocation.Text==VmMode){await ScanVirtualMachines();return;}
        if(scanLocation.Text==LeftoverMode){await ScanAppLeftovers();return;}
        if(scanLocation.Text=="Choose folder…"){ChooseReviewFolder();if(scanLocation.Text!="Chosen folder")return;}
        if(scanLocation.Text==StreamsMode){if(streamRoot is null)await ChooseStreamDrive();if(streamRoot is not null)await ScanStreams(streamRoot);return;}
        if (scanDrives.TryGetValue(scanLocation.Text,out var driveRoot)) { await ScanLocalDrive(driveRoot); return; }
        if(scanLocation.Text!=TempMode&&scanLocation.Text!=BrowserMode){MessageBox.Show(this,"This folder is not available. Choose another folder.");return;}
        if(scanLocation.Text==BrowserMode?!ConfigureBrowserEngine():!ConfigureCleanupEngine())return;
        streamReview=false;driveReview=false;
        enableFileSelection.Checked=true;
        if (engine is null) { MessageBox.Show(this, "The checked temporary-file sources are unavailable."); return; }
        fileColumnsSized = false; resultsMode = false; cleanupOutcomes.Clear(); files.CheckBoxes = true; SetBusy(true); cancel = new(); files.Items.Clear(); scan = null; status.Text = "Scanning temporary files…";
        try
        {
            var snapshot = exclusions.ToArray();
            var progress = new Progress<int>(n => status.Text = $"Scanning… {n:N0} entries inspected. No files are being deleted.");
            TimeSpan? wait = maxWait.Checked ? TimeSpan.FromSeconds((double)waitSeconds.Value) : null;
            scan = await Task.Run(() => engine.Scan(snapshot, cancel.Token, progress, maxDuration: wait));
            scanSummary.Text = 
                (scan.Cancelled ? "Scan cancelled — partial preview. " : scan.Limited ? "Max wait reached — partial preview. " : "Scan complete. ") + "Select files to review and clean.";
            ShowScanMetrics();
            details.Text = "Allowed location: " + engine.Root + "\r\nRule: creation and last write at least seven days ago; no links, protected files, or multiple hard links.\r\nAge is not a guarantee that a file is unused.\r\n" + string.Join("\r\n", scan.Skipped.Select(s => s.Reason + " | " + s.Path));
            using var journal = store.StartJournal();
            LocalStore.Append(journal, new { Kind=scanLocation.Text==BrowserMode?"Browser caches":"Temporary files",Location=engine.Root,Summary = $"Scan {scan.At:g}: {scan.Candidates.Count} eligible; {scan.Skipped.Count} skipped; cancelled={scan.Cancelled}; limited={scan.Limited}." });
            status.Text = "Review the list. Files are not selected automatically.";
        }
        finally { cancel.Dispose(); cancel = null; SetBusy(false); RebuildFiles(false); RefreshHistory(); }
    }
    async Task Clean()
    {
        if (smoke) return; // UI smoke mode is never allowed to delete, even under automation.
        if(scanLocation.Text==BrowserMode&&!BrowsersClosed())return;
        if(PersonalSelection){await RecycleSelected();return;}
        if (engine is null || scan is null || resultsMode || driveReview) return;
        var selected = files.CheckedItems.Cast<ListViewItem>().Select(i => (Candidate)i.Tag!).ToArray();
        if (selected.Length == 0) { MessageBox.Show(this, "Select the files you want to clean first."); return; }
        if (MessageBox.Show(this, $"Permanently delete {selected.Length:N0} selected temporary files ({FormatBytes(selected.Sum(c => c.Size))} logical size)?\n\nThese files will not go to the Recycle Bin. This app cannot undo deletion. Close other applications first. Files that changed, are locked, or are excluded will be skipped.", "Confirm selected cleanup", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.OK) return;
        if(scanLocation.Text==BrowserMode&&!BrowsersClosed())return;
        resultsMode = true; files.CheckBoxes = false; SetBusy(true); cancel = new(); status.Text = "Cleaning selected files…";
        try
        {
            using var journal = store.StartJournal();
            LocalStore.Append(journal, new { Summary = $"Cleanup started {DateTimeOffset.Now:g}; {selected.Length} files requested." });
            var progress = new Progress<int>(n => status.Text = $"Processed {n:N0} of {selected.Length:N0} files. Cancel stops remaining work.");
            var result = await Task.Run(() => engine.Clean(selected, exclusions.ToArray(), cancel.Token, o => LocalStore.Append(journal, o), progress));
            RecordCleanup(result, selected.Length);
            var deleted = result.Outcomes.Count(o => o.Status == "Deleted"); var bytes = result.Outcomes.Sum(o => o.Bytes);
            string delta = result.FreeBefore.HasValue && result.FreeAfter.HasValue ? FormatBytes(result.FreeAfter.Value - result.FreeBefore.Value) : "Unknown";
            var summary = $"Cleanup {result.At:g}: {deleted} deleted; {result.Outcomes.Count - deleted} skipped; {selected.Length - result.Outcomes.Count} not processed.\r\nDeleted logical size: {FormatBytes(bytes)}. Observed free-space change: {delta}. Cancelled: {result.Cancelled}.";
            LocalStore.Append(journal, new { Summary = summary });
            scanSummary.Text = $"{(result.Cancelled ? "Cleanup cancelled" : "Cleanup finished")} · Observed free-space change: {delta}. Results remain below; scan again before further cleanup."; details.Text = string.Join("\r\n", result.Outcomes.Select(o => o.Status + ": " + o.Reason + " | " + o.Path));
            status.Text = "Finished. Results are saved locally. Scan again for a fresh preview.";
        }
        catch { scanSummary.Text = "Cleanup interrupted. Outcomes may be incomplete; review History before rescanning."; throw; }
        finally { cancel.Dispose(); cancel = null; SetBusy(false); RebuildFiles(false); RefreshHistory(); RefreshOverview(); }
    }
    void SaveExclusions()
    {
        exclusions = exclusions.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        store.SaveExclusions(exclusions); RefreshExclusions(); files.Items.Clear(); scan = null;
        scanSummary.Text = "Exclusions changed. Scan again to refresh the preview."; UpdateActions();
    }
    void RefreshExclusions() { exclusionsList.Items.Clear(); exclusionsList.Items.AddRange(exclusions.Cast<object>().ToArray()); exclusionsEmpty.Visible = exclusions.Count == 0; UpdateActions(); }
    void RefreshHistory() { history.Items.Clear(); foreach (var p in store.History()) history.Items.Add(new HistoryItem(p, ActivityLabel(p))); historyEmpty.Visible = history.Items.Count == 0; if (history.Items.Count > 0) history.SelectedIndex = 0; latestActivity.Text = history.Items.Count > 0 ? history.Items[0]!.ToString() : "No activity yet. Start with a temporary-file scan."; PreviewHistory(); RefreshHistoryDetails(); UpdateActions(); }
    sealed record HistoryItem(string Path, string Label) { public override string ToString() => Label; }
    void Report()
    {
        if (history.SelectedItem is not HistoryItem item) { MessageBox.Show(this, "Select an activity record first."); return; }
        using var dialog = new Form { Text = "Support report — preview before sharing", Width = 850, Height = 650, StartPosition = FormStartPosition.CenterParent };
        var text = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false, Text = LocalStore.ReadReport(item.Path, false) };
        var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 55 };
        var paths = new AppearanceCheckBox { Text = "Include file paths (may reveal personal information)", AutoSize = true, Margin = new(8, 15, 8, 0) };
        paths.CheckedChanged += (_, _) => text.Text = LocalStore.ReadReport(item.Path, paths.Checked);
        var save = new AppearanceButton { Text = "Export text…", AutoSize = true }; save.Click += (_, _) => { using var d = new SaveFileDialog { Filter = "Text report|*.txt", FileName = "Maintenance-report.txt" }; if (d.ShowDialog(dialog) == DialogResult.OK) { try { File.WriteAllText(d.FileName, text.Text); } catch (Exception e) { MessageBox.Show(dialog, "Could not export: " + e.Message); } } };
        bar.Controls.Add(paths); bar.Controls.Add(save); dialog.Controls.Add(text); dialog.Controls.Add(bar); dialog.ShowDialog(this);
    }
    void RefreshOverview()
    {
        var cpu = Registry.GetValue(@"HKEY_LOCAL_MACHINE\HARDWARE\DESCRIPTION\System\CentralProcessor\0", "ProcessorNameString", "Unknown")?.ToString()?.Trim() ?? "Unknown";
        string memory = GetPhysicallyInstalledSystemMemory(out var kb) ? FormatBytes(checked((long)kb * 1024)) : "Unknown";
        string disk; try { var d = new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory)!); disk = $"{FormatBytes(d.AvailableFreeSpace)} free of {FormatBytes(d.TotalSize)}"; } catch { disk = "Unknown"; }
        osValue.Text = "Windows 11"; ramValue.Text = memory;
        try { var drive = new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory)!); diskValue.Text = FormatBytes(drive.AvailableFreeSpace); } catch { diskValue.Text = "Unknown"; }
        machineDetail.Text = $"{cpu} · Build {Environment.OSVersion.Version.Build}\nSystem drive: {disk} · Checked {DateTime.Now:t}";
    }
    [DllImport("kernel32.dll")] static extern bool GetPhysicallyInstalledSystemMemory(out ulong kb);
    internal static string FormatBytes(long n)
    {
        string[] units=["B","KB","MB","GB","TB","PB"]; double value=n; int unit=0;
        while(Math.Abs(value)>=1024 && unit<units.Length-1) { value/=1024; unit++; }
        return unit==0 ? $"{n:N0} B" : $"{value:N1} {units[unit]}";
    }
}































