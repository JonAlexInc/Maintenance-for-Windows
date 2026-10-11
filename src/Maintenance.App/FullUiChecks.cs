namespace Maintenance.App;
sealed partial class MainWindow
{
    async Task VerifyFullUi()
    {
        SetBusy(false); PopulateDrives(); await RefreshSystem(); await ReadStartup();
        var violations = new HashSet<string>();
        IEnumerable<Control> Desc(Control c) => c.Controls.Cast<Control>().SelectMany(x => new[] { x }.Concat(Desc(x)));
        void Capture(string name) {
            foreach(var grid in Desc(this).OfType<ResultGrid>().Where(g=>g.Visible))grid.FitResults();
            PerformLayout(); Refresh();
            using var capture = new Bitmap(Width, Height); DrawToBitmap(capture, new Rectangle(0,0,Width,Height)); capture.Save("artifacts/polish-" + name + ".png");
            foreach (var b in Desc(this).OfType<Button>().Where(b => b.Visible && b.Parent is FlowLayoutPanel flow && !flow.AutoScroll))
                if (b.Right > b.Parent!.ClientSize.Width + 2 || b.Bottom > b.Parent.ClientSize.Height + 2) violations.Add(name + " · clipped action: " + b.Text);
        }
        foreach (var size in new[] { new Size(1760,1120),new Size(1280,900) }) {
            Size = size;
            foreach (var theme in new[] { "Dark","Light" }) {
                themeChoice.SelectedItem = theme;
                for(int page=0;page<pages.Count;page++) { SelectPage(page); await Task.Delay(35); Capture($"{size.Width}-{theme}-{page}"); }
            }
        }
        Size = new Size(1760,1120); themeChoice.SelectedItem="Dark";
        SelectPage(5); for(int i=0;i<hardwareTabs.TabPages.Count;i++) { hardwareTabs.SelectedIndex=i; await Task.Delay(25); Capture("hardware-"+i); }
        SelectPage(4); for(int i=0;i<historyTabs.TabPages.Count;i++){historyTabs.SelectedIndex=i;Capture("history-"+i);}
        SelectPage(7);for(int i=0;i<startupTabs.TabPages.Count;i++){startupTabs.SelectedIndex=i;Capture("startup-"+i);}
        SelectPage(1);foreach(string mode in new[]{TempMode,BrowserMode,LeftoverMode,UpdateMode,RestoreMode,"Downloads","Desktop",VmMode,StreamsMode}) { scanLocation.SelectedItem=mode; await Task.Delay(30); Capture("cleanup-"+scanLocation.Items.IndexOf(mode)); }
        scanLocation.SelectedItem=LeftoverMode;for(int i=0;i<leftoverTabs.TabPages.Count;i++){leftoverTabs.SelectedIndex=i;Capture("apps-"+i);}
        SelectPage(3);foreach(string mode in new[]{"Local disk","Network folder","Internet speed"}){speedMode.SelectedItem=mode;Capture("speed-"+speedMode.Items.IndexOf(mode));}
        var coverageRoot = Directory.CreateDirectory(Path.GetFullPath(Path.Combine(".test-data", "coverage-" + Guid.NewGuid().ToString("N")))).FullName;
        File.WriteAllText(Path.Combine(coverageRoot,"plain.txt"), "fixture");
        var measured = StorageMeasure.Read(coverageRoot, CancellationToken.None);
        var missing = StorageMeasure.Read(Path.Combine(coverageRoot,"missing"), CancellationToken.None);
        if (measured.Values.Sum() != 7 || measured.Skipped != 0 || missing.Skipped != 1 || missing.Reasons.Values.Sum() != missing.Skipped || missing.Examples.Count != 1) throw new Exception("Storage coverage accounting failed");
        SelectPage(8); SetBusy(true); SetBusy(false); if(duplicateRecycle.Enabled)throw new Exception("Empty duplicate view enables recycling");
        foreach(int page in new[]{1,3}) { SelectPage(page); var viewport = pages[page].Controls.OfType<Panel>().First(p => p.AutoScroll); viewport.AutoScrollPosition = new(0,2000); Capture("scrolled-"+page); viewport.AutoScrollPosition=Point.Empty; }
        ShowSettings();
        File.WriteAllLines("artifacts/full-ui-layout-review.txt",violations);
        File.WriteAllText("artifacts/full-ui-check-success.txt",DateTimeOffset.Now.ToString("o")+$" · All {pages.Count} pages, hardware/history/startup tabs, cleanup modes, speed modes and Settings captured. Light/dark at 1760x1120 and 1280x900. {violations.Count} layout items to review. Read-only inventory; no cleanup, benchmark or startup changes.");
    }
}



