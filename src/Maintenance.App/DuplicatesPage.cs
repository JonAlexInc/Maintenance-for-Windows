using Maintenance.Core;
using System.Text;
namespace Maintenance.App;

sealed partial class MainWindow
{
    readonly List<string> duplicateRoots = [];
    readonly AppearanceChoice duplicateRootChoice = new() { Width = 520, Height = 38 };
    readonly TextBox duplicateFind = new() { Width = 290, PlaceholderText = "Find a name or full path" };
    readonly AppearanceChoice duplicateOrder = new() { Width = 210, Height = 38 };
    readonly DataGridView duplicateGroups = HistoryGrid(), duplicateFiles = HistoryGrid();
    readonly Label duplicateSummary = TextLabel("Add folders to compare. Nothing is selected for removal automatically.", 11, true);
    readonly Label duplicateSelection = TextLabel("Choose a group to review its copies.", 9, true);
    readonly Label duplicatePages = TextLabel("No results yet", 9);
    readonly TextBox duplicateDetail = new() { Dock = DockStyle.Bottom, Height = 48, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical };
    readonly HashSet<string> duplicateSelected = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, string> duplicateOutcomes = new(StringComparer.OrdinalIgnoreCase);
    readonly Label duplicateEmpty = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Padding = new(24) };
    readonly List<Button> duplicateReviewActions = [];
    readonly Label duplicateRightEmpty = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Padding = new(24), Text = "Review matching copies here.\n\nChoose a group after scanning.\nAt least one copy always stays in place." };
    FlowLayoutPanel duplicateReviewBar = null!;
    DuplicateScan? duplicateScan;
    DuplicateGroup? duplicateGroup;
    Candidate? duplicateKeeper;
    int duplicatePage, duplicateFilePage, duplicateMatchCount;
    FlowLayoutPanel duplicateFilePager = null!;
    Label duplicateFilePosition = null!;
    Button duplicateFilePrevious = null!, duplicateFileNext = null!;
    bool renderingDuplicates, duplicateWorking;
    Button duplicateRecycle = null!, duplicatePrevious = null!, duplicateNext = null!;
    AppearanceButton duplicateStop = null!;
    Button duplicateExportButton = null!, duplicateDetailsButton = null!;

    void BuildDuplicates()
    {
        var page = Page("Exact duplicates", "Subfolders included · Compare all added locations · Choose a copy to keep before recycling."); page.Controls[0].Height = 80; var body = Body(page);
        var sources = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, MinimumSize=new(0,58), WrapContents = true, Padding = new(0, 0, 0, 8) };
        sources.Controls.Add(duplicateRootChoice);
        sources.Controls.Add(Button("Add folder / drive…", () => { using var picker = new FolderBrowserDialog { Description = "Choose a folder or NTFS drive to compare", UseDescriptionForTitle = true }; if (picker.ShowDialog(this) == DialogResult.OK) AddDuplicateRoot(picker.SelectedPath); }));
        sources.Controls.Add(Button("Downloads", () => AddDuplicateRoot(DownloadsFolder())));
        sources.Controls.Add(Button("Desktop", () => AddDuplicateRoot(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory))));
        sources.Controls.Add(Button("Remove location", () => { duplicateRoots.Remove(duplicateRootChoice.Text); RefreshDuplicateRoots(); }));
        sources.SizeChanged += (_, _) => duplicateRootChoice.Width = Math.Clamp(sources.ClientSize.Width - 660, 250, 640);
        RefreshDuplicateRoots();
        var actions = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true, Padding = new(0, 4, 0, 8) };
        actions.Controls.Add(StartSearch(ScanDuplicates));
        duplicateStop = new AppearanceButton { Text = "Cancel", Width = 100, Height = 39, Enabled = false }; duplicateStop.Click += (_, _) => { if (duplicateWorking) cancel?.Cancel(); }; actions.Controls.Add(duplicateStop);
        actions.Controls.Add(duplicateExportButton = Button("Export matches…", ExportDuplicates)); actions.Controls.Add(duplicateDetailsButton = Button("Scan details…", ShowDuplicateIssues));
        actions.Controls.Add(Button("Clear results", () => { duplicateScan = null; duplicateOutcomes.Clear(); RenderDuplicateGroups(); duplicateSummary.Text = "Results cleared. Add locations and scan again."; status.Text = "Ready to find duplicates. Subfolders are included."; }));
        duplicateSummary.Dock = DockStyle.Top; duplicateSummary.Height = 74; duplicateSummary.Font = new("Segoe UI", 9, FontStyle.Bold); duplicateSummary.Padding = new(8); duplicateSummary.Tag = "notice";
        var filters = ResultFilters(duplicateFind,duplicateOrder,Button("Clear filter",()=>duplicateFind.Clear()));
        duplicateOrder.Items.AddRange(["Most extra space", "Most copies", "Largest files", "Name A–Z"]); duplicateOrder.SelectedItem = "Most extra space";
        duplicateFind.TextChanged += (_, _) => { duplicatePage = 0; RenderDuplicateGroups(); };
        duplicateOrder.SelectedIndexChanged += (_, _) => { duplicatePage = 0; RenderDuplicateGroups(); };
        var panes = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Padding = new(0, 8, 0, 4) };
        duplicateResultSurface=panes;
        panes.ColumnStyles.Add(new(SizeType.Percent, 34)); panes.ColumnStyles.Add(new(SizeType.Percent, 66));
        var left = Card(); left.Padding = new(12); left.Dock = DockStyle.Fill;
        var right = Card(); right.Padding = new(12); right.Dock = DockStyle.Fill; right.Margin = Padding.Empty;
        var groupTitle = TextLabel("MATCHING GROUPS", 9, true); groupTitle.Dock = DockStyle.Top;
        foreach (var (name, width) in new[] { ("File group", 220), ("Copies", 65), ("Extra size", 100) }) duplicateGroups.Columns.Add(new DataGridViewTextBoxColumn { Name = name, HeaderText = name, Width = width, SortMode = DataGridViewColumnSortMode.NotSortable });
        duplicateGroups.Columns[0].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        duplicateGroups.CellFormatting += (_, e) => { if (e.ColumnIndex == 2 && e.Value is long bytes) { e.Value = FormatBytes(bytes); e.FormattingApplied = true; } };
        duplicateGroups.SelectionChanged += (_, _) => { if (!renderingDuplicates) ShowDuplicateGroup(duplicateGroups.CurrentRow?.Tag as DuplicateGroup); };
        var pager = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, WrapContents = true, Padding = new(0, 8, 0, 0) }; duplicatePrevious = Button("Previous", () => { duplicatePage--; RenderDuplicateGroups(); }); duplicateNext = Button("Next", () => { duplicatePage++; RenderDuplicateGroups(); });
        duplicatePages.Width = 160; pager.Controls.Add(duplicatePrevious); pager.Controls.Add(duplicateNext); pager.Controls.Add(duplicatePages);
        left.Controls.Add(duplicateGroups); left.Controls.Add(duplicateEmpty); duplicateEmpty.BringToFront(); left.Controls.Add(groupTitle); left.Controls.Add(pager);
        duplicateSelection.Dock = DockStyle.Top; duplicateSelection.Height = 54;
        foreach (var (name, width) in new[] { ("Choice", 105), ("File and location", 500), ("Size", 100) }) duplicateFiles.Columns.Add(new DataGridViewTextBoxColumn { Name = name, HeaderText = name, Width = width, SortMode = DataGridViewColumnSortMode.Automatic });
        duplicateFiles.Columns[1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill; duplicateFiles.Columns[1].DefaultCellStyle.WrapMode = DataGridViewTriState.True; duplicateFiles.RowTemplate.Height = 76; duplicateFiles.ScrollBars = ScrollBars.Vertical;
        duplicateFiles.CellFormatting += (_, e) => {
            if (e.ColumnIndex == 2 && e.Value is long bytes) { e.Value = FormatBytes(bytes); e.FormattingApplied = true; }
            if (e.ColumnIndex == 3 && e.Value is DateTime at) { e.Value = at.ToString("g"); e.FormattingApplied = true; }
            if (e.RowIndex >= 0 && duplicateFiles.Rows[e.RowIndex].Tag is Candidate c && (c == duplicateKeeper || duplicateSelected.Contains(c.Path))) { e.CellStyle!.ForeColor = c == duplicateKeeper ? Color.MediumTurquoise : accent; }
        };
        duplicateFiles.CellClick += (_, e) => { if (e.RowIndex >= 0 && e.ColumnIndex == 0 && duplicateFiles.Rows[e.RowIndex].Tag is Candidate c) ToggleDuplicate(c); };
        duplicateFiles.SelectionChanged += (_, _) => { if (duplicateFiles.CurrentRow?.Tag is Candidate c) duplicateDetail.Text = c.Path + "\r\n" + (duplicateOutcomes.TryGetValue(c.Path, out var outcome) ? outcome : c == duplicateKeeper ? "Kept copy · protected from selection" : "Exact contents verified at scan time. Select explicitly to recycle; contents will be rechecked."); };
        var review = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, WrapContents = true, Padding = new(0, 8, 0, 0) };
        review.Controls.Add(Button("Keep this copy", () => { if (duplicateWorking || duplicateFiles.CurrentRow?.Tag is not Candidate c || duplicateOutcomes.ContainsKey(c.Path)) return; duplicateKeeper = c; duplicateSelected.Remove(c.Path); RenderDuplicateFiles(); }));
        review.Controls.Add(Button("Select / unselect", () => { if (duplicateFiles.CurrentRow?.Tag is Candidate c) ToggleDuplicate(c); }));
        review.Controls.Add(Button("Select shown copies", () => { if (duplicateWorking || duplicateGroup is null) return; foreach (var c in duplicateGroup.Files.Skip(duplicateFilePage * 200).Take(200).Where(c => c != duplicateKeeper && !duplicateOutcomes.ContainsKey(c.Path) && !DuplicateCatalog.ReviewOnly(c.Path))) duplicateSelected.Add(c.Path); RenderDuplicateFiles(); }));
        review.Controls.Add(Button("Select none", () => { duplicateSelected.Clear(); RenderDuplicateFiles(); }));
        duplicateRecycle = Primary("Recycle selected…", RecycleDuplicates); review.Controls.Add(duplicateRecycle);
        duplicateFilePager = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 48 };
        duplicateFilePrevious = Button("Previous copies", () => { duplicateFilePage--; RenderDuplicateFiles(); }); duplicateFileNext = Button("Next copies", () => { duplicateFilePage++; RenderDuplicateFiles(); }); duplicateFilePosition = TextLabel("", 8); duplicateFilePosition.Width = 150;
        duplicateFilePager.Controls.Add(duplicateFilePrevious); duplicateFilePager.Controls.Add(duplicateFileNext); duplicateFilePager.Controls.Add(duplicateFilePosition);
        duplicateReviewBar = review; duplicateReviewActions.AddRange(review.Controls.OfType<Button>().Where(b => b != duplicateRecycle));
        right.Controls.Add(duplicateFiles); right.Controls.Add(duplicateRightEmpty); duplicateRightEmpty.BringToFront(); right.Controls.Add(duplicateFilePager); right.Controls.Add(duplicatesVisual);right.Controls.Add(duplicateSelection); right.Controls.Add(duplicateDetail); right.Controls.Add(review);
        panes.Controls.Add(left, 0, 0); panes.Controls.Add(right, 1, 0);
        var coverage = TextLabel("NTFS · Subfolders included · Cloud / USB copies: review only · Cloud-only files, links, protected files and extra streams are omitted. Extra size is logical; recycling frees no space until the Bin is emptied.", 8);
        coverage.Dock = DockStyle.Bottom; coverage.Height = 42;
        body.Controls.Add(panes); body.Controls.Add(filters); body.Controls.Add(duplicateSummary); body.Controls.Add(actions); body.Controls.Add(sources); body.Controls.Add(coverage);
        RenderDuplicateGroups();
    }
    void AddDuplicateRoot(string path)
    {
        if (busy) return;
        path = DuplicateCatalog.LocalPath(path);
        if (DuplicateCatalog.Protected(path)) throw new IOException("Choose personal folders; system and application folders are excluded from duplicate cleanup.");
        if (!duplicateRoots.Contains(path, StringComparer.OrdinalIgnoreCase)) duplicateRoots.Add(path);
        RefreshDuplicateRoots(); duplicateRootChoice.SelectedItem = path;
    }
    void RefreshDuplicateRoots()
    {
        duplicateRootChoice.Items.Clear(); duplicateRootChoice.Items.AddRange(duplicateRoots.Count == 0 ? ["Add folders to compare…"] : duplicateRoots.ToArray());
        duplicateRootChoice.SelectedItem = duplicateRootChoice.Items[0];
        duplicateRootChoice.AccessibleName = $"{duplicateRoots.Count} scan locations. Every listed location is scanned together.";
    }
    async Task ScanDuplicates()
    {
        if (busy) return;
        if (duplicateRoots.Count == 0) { duplicateSummary.Text = "Add a folder or drive first. Add multiple locations to compare files across them."; return; }
        var roots = duplicateRoots.ToArray(); var excluded = exclusions.ToArray();
        duplicateWorking = true; SetBusy(true); cancel = new(); duplicateStop.Enabled = true;
        duplicateScan = null; duplicateOutcomes.Clear(); RenderDuplicateGroups();
        duplicateSummary.Text = $"Comparing {roots.Length} location(s) · Scanning never deletes files.";
        var progress = new Progress<string>(s => { if (duplicateWorking) { status.Text = s; duplicateSummary.Text = s; } });
        try {
            duplicateScan = await Task.Run(() => DuplicateCatalog.Scan(roots, excluded, cancel.Token, progress));
            duplicatePage = 0;
            duplicateSummary.Text = $"{duplicateScan.Groups.Count:N0} matching groups · {FormatBytes(duplicateScan.Groups.Sum(g => g.ExtraBytes))} in extra copies\n{duplicateScan.FilesExamined:N0} files · {duplicateScan.FoldersExamined:N0} folders · {duplicateScan.Skipped:N0} omitted · {(duplicateScan.Partial ? "Partial scan — cancelled or 250,000-entry limit reached. Narrow your folders for full coverage." : "Scan complete · Nothing selected for removal.")}";
            using var journal = store.StartJournal(); LocalStore.Append(journal, new { Kind = "Exact duplicates", Location = string.Join("; ", roots), Summary = "Scan exact duplicates: " + duplicateSummary.Text.Replace("\n", " · ") });
            status.Text = "Duplicate scan finished. Choose a group, keep a copy, and review the rest.";
        } catch (Exception e) { duplicateSummary.Text = "Scan stopped: " + e.Message; status.Text = duplicateSummary.Text; throw; } finally { duplicateWorking = false; cancel.Dispose(); cancel = null; SetBusy(false); duplicateStop.Enabled = false; RenderDuplicateGroups(); RefreshHistory(); }
    }
    void RenderDuplicateGroups()
    {
        if (duplicatePrevious is null) return;
        renderingDuplicates = true; duplicateGroups.Rows.Clear();
        IEnumerable<DuplicateGroup> query = duplicateScan?.Groups ?? [];
        if (!string.IsNullOrWhiteSpace(duplicateFind.Text)) query = query.Where(g => g.Files.Any(c => c.Path.Contains(duplicateFind.Text.Trim(), StringComparison.OrdinalIgnoreCase)));
        query = duplicateOrder.Text switch { "Most copies" => query.OrderByDescending(g => g.Files.Count), "Largest files" => query.OrderByDescending(g => g.Size), "Name A–Z" => query.OrderBy(g => Path.GetFileName(g.Files[0].Path), StringComparer.OrdinalIgnoreCase), _ => query.OrderByDescending(g => g.ExtraBytes) };
        var matches = query.ToArray(); duplicateMatchCount = matches.Length; duplicateEmpty.Visible = matches.Length == 0; duplicateGroups.Visible = matches.Length != 0; duplicateEmpty.ForeColor = muted; duplicateEmpty.BackColor = surface;
        duplicateEmpty.Text = duplicateWorking ? "Comparing files…\n\nProgress is shown in the status bar. You can cancel safely." : duplicateScan is null ? "Ready to compare\n\nAdd folders, then choose Find duplicates.\nSubfolders are included." : duplicateScan.Groups.Count > 0 ? "No groups match your search.\n\nClear the search to see all verified copies." : $"No exact duplicate groups found.\n\n{duplicateScan.FilesExamined:N0} files in {duplicateScan.FoldersExamined:N0} folders · {duplicateScan.Skipped:N0} omitted\nOpen Scan details to see what was left out."; duplicatePage = Math.Clamp(duplicatePage, 0, Math.Max(0, (matches.Length - 1) / 50));
        foreach (var group in matches.Skip(duplicatePage * 50).Take(50)) duplicateGroups.Rows[duplicateGroups.Rows.Add(Path.GetFileName(group.Files[0].Path), group.Files.Count, group.ExtraBytes)].Tag = group;
        duplicatePages.Text = matches.Length == 0 ? "No groups" : $"{duplicatePage + 1} / {(matches.Length + 49) / 50} · {matches.Length:N0} groups";
        duplicatePrevious.Enabled = !busy && duplicatePage > 0; duplicateNext.Enabled = !busy && (duplicatePage + 1) * 50 < matches.Length;
        renderingDuplicates = false; ShowDuplicateGroup(duplicateGroups.CurrentRow?.Tag as DuplicateGroup);
    }
    void ShowDuplicateGroup(DuplicateGroup? group)
    {
        duplicateGroup = group; duplicateFilePage = 0; duplicateSelected.Clear(); duplicateKeeper = group?.Files.FirstOrDefault(c => !duplicateOutcomes.ContainsKey(c.Path) && !DuplicateCatalog.ReviewOnly(c.Path)) ?? group?.Files.FirstOrDefault(c => !duplicateOutcomes.ContainsKey(c.Path)); RenderDuplicateFiles();
    }
    void ToggleDuplicate(Candidate c)
    {
        if (busy || c == duplicateKeeper || duplicateOutcomes.ContainsKey(c.Path) || DuplicateCatalog.ReviewOnly(c.Path)) return;
        if (!duplicateSelected.Remove(c.Path)) duplicateSelected.Add(c.Path); RenderDuplicateFiles();
    }
    void RenderDuplicateFiles()
    {
        var highlighted = (duplicateFiles.CurrentRow?.Tag as Candidate)?.Path;
        duplicateFiles.Rows.Clear(); duplicateDetail.Clear();
        var allCopies = duplicateGroup?.Files ?? []; duplicateFilePage = Math.Clamp(duplicateFilePage, 0, Math.Max(0, (allCopies.Count - 1) / 200));
        duplicateFilePager.Visible = allCopies.Count > 200; duplicateFilePrevious.Enabled = !busy && duplicateFilePage > 0; duplicateFileNext.Enabled = !busy && (duplicateFilePage + 1) * 200 < allCopies.Count; duplicateFilePosition.Text = $"{duplicateFilePage + 1} / {Math.Max(1, (allCopies.Count + 199) / 200)} pages";
        foreach (var c in allCopies.Skip(duplicateFilePage * 200).Take(200)) {
            string choice = c == duplicateKeeper ? "● Keep" : duplicateOutcomes.ContainsKey(c.Path) ? "Result" : DuplicateCatalog.ReviewOnly(c.Path) ? "Review only" : duplicateSelected.Contains(c.Path) ? "● Selected" : "Select";
            int row = duplicateFiles.Rows.Add(choice, Path.GetFileName(c.Path) + "\n" + c.Path, c.Size); duplicateFiles.Rows[row].Tag = c; duplicateFiles.Rows[row].Cells[1].ToolTipText = c.Path;
            if (c.Path == highlighted) duplicateFiles.CurrentCell = duplicateFiles.Rows[row].Cells[0];
        }
        ShowDuplicateVisual();
        duplicateSelection.Text = duplicateGroup is null ? "Choose a group to review its copies." : $"{duplicateGroup.Files.Count:N0} exact copies · {FormatBytes(duplicateGroup.Size)} each\n{duplicateSelected.Count:N0} selected · Keep: {Path.GetFileName(duplicateKeeper?.Path ?? "")}";
        if (duplicateFiles.CurrentRow?.Tag is Candidate current) duplicateDetail.Text = DateTime.FromFileTimeUtc(current.Written).ToLocalTime().ToString("g") + " · " + duplicateOutcomes.GetValueOrDefault(current.Path, current == duplicateKeeper ? "Kept copy · protected from selection" : "Verified at scan time · select explicitly to recycle");
        UpdateDuplicateActions();
    }
    void UpdateDuplicateActions()
    {
        if (duplicateRecycle is null || duplicateReviewBar is null) return;
        duplicateExportButton.Enabled = duplicateDetailsButton.Enabled = !busy && duplicateScan is not null;
        duplicatePrevious.Enabled = !busy && duplicatePage > 0; duplicateNext.Enabled = !busy && (duplicatePage + 1) * 50 < duplicateMatchCount;
        duplicateReviewBar.Visible = duplicateGroup is not null; duplicateFiles.Visible = duplicateGroup is not null; duplicateDetail.Visible = duplicateGroup is not null; duplicateRightEmpty.Visible = duplicateGroup is null;
        foreach (var action in duplicateReviewActions) action.Enabled = !busy && duplicateGroup is not null;
        duplicateRecycle.Enabled = !busy && duplicateSelected.Count > 0 && duplicateKeeper is not null && !DuplicateCatalog.ReviewOnly(duplicateKeeper.Path);
    }
    async Task RecycleDuplicates()
    {
        if (smoke || busy || duplicateGroup is null || duplicateKeeper is null || duplicateSelected.Count == 0) return;
        var group = duplicateGroup; var keeper = duplicateKeeper; var selected = group.Files.Where(c => duplicateSelected.Contains(c.Path)).ToArray();
        if (MessageBox.Show(this, $"Recycle {selected.Length:N0} selected copies ({FormatBytes(selected.Sum(c => c.Size))}) from this group?\n\nKeep:\n{keeper.Path}\n\nMatching content does not mean an application no longer needs a file at its original location. Restore through the Recycle Bin if needed. Space is not freed until the Bin is emptied.", "Review duplicate copies", MessageBoxButtons.OKCancel, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.OK) return;
        duplicateWorking = true; SetBusy(true); cancel = new(); duplicateStop.Enabled = true;
        try {
            using var journal = store.StartJournal(); LocalStore.Append(journal, new { Kind = "Duplicate cleanup", Summary = $"Recycle {selected.Length} selected duplicate copies; one copy retained", KeptPath = keeper.Path });
            var excluded = exclusions.ToArray(); status.Text = "Rechecking duplicate contents before recycling…";
            var progress = new Progress<string>(s => { if (duplicateWorking) status.Text = s; });
            var result = await RecycleShell.OnSta(() => DuplicateCatalog.Recycle(group, keeper, selected, excluded, cancel.Token, o => LocalStore.Append(journal, o), RecycleShell.Move, progress));
            foreach (var outcome in result.Outcomes) duplicateOutcomes[outcome.Path] = outcome.Status + " · " + outcome.Reason;
            int recycled = result.Outcomes.Count(o => o.Status == "Recycled");
            duplicateSummary.Text = $"{recycled:N0} copies recycled · {selected.Length - recycled:N0} skipped / remaining\nResults retained. Your kept copy remains in place. Full outcomes are in History.";
            LocalStore.Append(journal, new { Summary = duplicateSummary.Text }); status.Text = duplicateSummary.Text;
        } finally { duplicateSelected.Clear(); duplicateWorking = false; cancel.Dispose(); cancel = null; SetBusy(false); duplicateStop.Enabled = false; RenderDuplicateFiles(); RefreshHistory(); }
    }
    void ShowDuplicateIssues()
    {
        if (duplicateScan is null) return;
        using var dialog = new Form { Text = "Duplicate scan coverage", Width = 1050, Height = 650, StartPosition = FormStartPosition.CenterParent };
        var text = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false, Text = $"{duplicateScan.Skipped:N0} omitted entries. First {duplicateScan.Issues.Count:N0} reasons shown. Protected folders are counted once, not per descendant.\r\n\r\n" + string.Join("\r\n", duplicateScan.Issues.Select(i => i.Path + " — " + i.Reason)) };
        dialog.Controls.Add(text); PaintTheme(dialog, canvas); dialog.ShowDialog(this);
    }
    void ExportDuplicates()
    {
        if (duplicateScan is null) return;
        using var dialog = new SaveFileDialog { Filter = "CSV file|*.csv", FileName = "Maintenance-duplicates.csv" }; if (dialog.ShowDialog(this) != DialogResult.OK) return;
        static string Q(string s) => "\"" + ((s.Length > 0 && "=+-@\t\r\n".Contains(s[0])) ? "'" + s : s).Replace("\"", "\"\"") + "\"";
        var lines = new List<string> { "Group,SHA256,Full path,Bytes,Modified UTC,Result,Partial scan" }; int index = 0;
        foreach (var group in duplicateScan.Groups) { index++; foreach (var c in group.Files) lines.Add(string.Join(",", new[] { index.ToString(), group.Hash, c.Path, c.Size.ToString(), DateTime.FromFileTimeUtc(c.Written).ToString("o"), duplicateOutcomes.GetValueOrDefault(c.Path, "Verified at scan time"), duplicateScan.Partial.ToString() }.Select(Q))); }
        File.WriteAllLines(dialog.FileName, lines, new UTF8Encoding(true)); status.Text = "Duplicate report exported with full file paths.";
    }
}












