using Maintenance.Core;
namespace Maintenance.App;
sealed partial class MainWindow
{
    async Task VerifyDuplicatesUi()
    {
        SetBusy(false); SelectPage(8);
        string root = Directory.CreateDirectory(Path.GetFullPath(Path.Combine(".test-data", "duplicate-ui-" + Guid.NewGuid().ToString("N")))).FullName;
        string originals = Directory.CreateDirectory(Path.Combine(root, "Originals")).FullName;
        string backup = Directory.CreateDirectory(Path.Combine(root, "Backup copies")).FullName;
        for (int i = 0; i < 105; i++) {
            var data = Enumerable.Repeat((byte)i, 64_000 + i * 10).ToArray();
            File.WriteAllBytes(Path.Combine(originals, $"Project report {i + 1:000}.pdf"), data);
            File.WriteAllBytes(Path.Combine(backup, $"Project report {i + 1:000} - copy.pdf"), data);
        }
        AddDuplicateRoot(originals); AddDuplicateRoot(backup); await ScanDuplicates();
        if (duplicateScan?.Groups.Count != 105 || duplicateGroups.Rows.Count != 50 || duplicateSelected.Count != 0) throw new Exception("Duplicate grouping/paging/no-auto-selection failed");
        if (duplicateGroup is null || duplicateKeeper is null) throw new Exception("Duplicate first group not selected");
        ToggleDuplicate(duplicateKeeper); if (duplicateSelected.Count != 0) throw new Exception("Keeper selectable");
        ToggleDuplicate(duplicateGroup.Files.First(c => c != duplicateKeeper)); if (duplicateSelected.Count != 1 || !duplicateRecycle.Enabled) throw new Exception("Explicit duplicate selection failed");
        duplicatePage = 2; RenderDuplicateGroups(); if (duplicateGroups.Rows.Count != 5 || duplicateNext.Enabled) throw new Exception("Last page unreachable");
        duplicateFind.Text = "report 003"; if (duplicateGroups.Rows.Count != 1 || duplicateSelected.Count != 0) throw new Exception("Duplicate search / stale-selection reset failed");
        duplicateFind.Clear(); duplicateOrder.SelectedItem = "Name A–Z";
        foreach (string theme in new[] { "Dark", "Light" }) {
            themeChoice.SelectedItem = theme; accentChoice.SelectedItem = "Teal"; await Task.Delay(150); Refresh();
            using var image = new Bitmap(Width, Height); DrawToBitmap(image, new Rectangle(0, 0, Width, Height)); image.Save($"artifacts/duplicates-{theme.ToLowerInvariant()}.png");
        }
        Size = new Size(1280, 900); themeChoice.SelectedItem = "Dark"; await Task.Delay(150); Refresh();
        using (var image = new Bitmap(Width, Height)) { DrawToBitmap(image, new Rectangle(0, 0, Width, Height)); image.Save("artifacts/duplicates-compact.png"); }
        if (duplicateRecycle.Bottom > duplicateRecycle.Parent!.ClientSize.Height || navigation[5].Bottom > navigation[5].Parent!.ClientSize.Height) throw new Exception("Duplicate actions or sidebar clipped");
        await VerifyDuplicateRecycleFixture(root);
        if (!HistoryCatalog.Read(store.History()).Scans.Any(s => s.Summary.Contains("exact duplicates"))) throw new Exception("Duplicate scan not recorded in history");
        var actualScan = duplicateScan;
        var sample = actualScan!.Groups[0];
        duplicateScan = actualScan with { Groups = [new DuplicateGroup(sample.Hash, Enumerable.Range(0, 405).Select(i => sample.Files[0] with { Path = Path.Combine(root, $"paging-fixture-{i}.bin") }).ToArray())] };
        RenderDuplicateGroups(); if (duplicateFiles.Rows.Count != 200 || !duplicateFileNext.Enabled) throw new Exception("Copy paging failed");
        duplicateFilePage = 2; RenderDuplicateFiles(); if (duplicateFiles.Rows.Count != 5 || duplicateFileNext.Enabled) throw new Exception("Last copy page inaccessible");
        duplicateScan = actualScan; RenderDuplicateGroups();
        File.WriteAllText("artifacts/duplicates-check-success.txt", DateTimeOffset.Now.ToString("o") + " · 105 exact groups, 3 accessible pages, search, explicit selection, protected keeper, light/dark/compact layout and persisted history. Generated fixture recycled and restored through Windows; no personal files modified.");
    }
    async Task VerifyDuplicateRecycleFixture(string parent)
    {
        string folder = Directory.CreateDirectory(Path.Combine(parent, "recycle-fixture")).FullName;
        string keep = Path.Combine(folder, "keep.txt"), remove = Path.Combine(folder, "duplicate-recovery-" + Guid.NewGuid().ToString("N") + ".txt");
        string payload = "Maintenance duplicate recycle/restore fixture only.";
        File.WriteAllText(keep, payload); File.WriteAllText(remove, payload);
        var group = DuplicateCatalog.Scan([folder], [], CancellationToken.None).Groups.Single();
        using var journal = store.StartJournal(); LocalStore.Append(journal, new { Kind = "Duplicate cleanup", Summary = "Owned test fixture recycling and restoration" });
        var result = await RecycleShell.OnSta(() => DuplicateCatalog.Recycle(group, group.Files.Single(c => c.Path == keep), [group.Files.Single(c => c.Path == remove)], [], CancellationToken.None, o => LocalStore.Append(journal, o), RecycleShell.Move));
        if (result.Outcomes.Single().Status != "Recycled" || !File.Exists(keep) || File.Exists(remove)) throw new Exception("Windows fixture recycling failed: " + result.Outcomes.Single().Reason);
        bool found = await RecycleShell.OnSta(() => {
            dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application")!)!;
            try {
                dynamic bin = shell.NameSpace(10);
                foreach (dynamic item in bin.Items()) {
                    try {
                        string from = Convert.ToString(item.ExtendedProperty("System.Recycle.DeletedFrom")) ?? "";
                        string name = Convert.ToString(item.Name) ?? "";
                        if (!from.Equals(folder, StringComparison.OrdinalIgnoreCase) || !name.Equals(Path.GetFileName(remove), StringComparison.OrdinalIgnoreCase)) continue;
                        string recycledPath = Convert.ToString(item.Path) ?? "";
                        if (File.ReadAllText(recycledPath) != payload) throw new Exception("Recycled payload changed");
                        item.InvokeVerb("undelete"); return true;
                    } finally { System.Runtime.InteropServices.Marshal.ReleaseComObject(item); }
                }
                return false;
            } finally { System.Runtime.InteropServices.Marshal.ReleaseComObject(shell); }
        });
        if (!found) throw new Exception("Owned fixture was not found in Recycle Bin; restoration not verified");
        for (int i = 0; i < 40 && !File.Exists(remove); i++) await Task.Delay(100);
        if (!File.Exists(remove) || File.ReadAllText(remove) != payload || File.ReadAllText(keep) != payload) throw new Exception("Owned fixture restore failed");
        LocalStore.Append(journal, new { Summary = "Owned test fixture restored from Recycle Bin; payload and keeper verified" });
    }}

