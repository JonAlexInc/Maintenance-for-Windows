using System.Globalization;
using System.Text.Json;
using Maintenance.Core;
using Microsoft.Win32;
using System.Runtime.InteropServices;

namespace Maintenance.App;

sealed partial class MainWindow
{
    readonly List<Button> navigation = [];
    readonly List<Panel> cards = [];
    readonly Label selectedSummary = new() { AutoSize = true };
    readonly Label emptyFiles = new() { AutoSize = false, TextAlign = ContentAlignment.MiddleCenter, Dock = DockStyle.Fill };
    readonly Panel fileArea = new() { Dock = DockStyle.Fill };
    readonly Label osValue = new(), diskValue = new(), ramValue = new(), machineDetail = new(), latestActivity = new();
    readonly TextBox historyPreview = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, BorderStyle = BorderStyle.None };
    readonly AppearanceChoice themeChoice = new() { Width = 168, Height = 36, AccessibleName = "Appearance theme" };
    readonly AppearanceChoice accentChoice = new() { Width = 168, Height = 36, AccessibleName = "Accent color" };
    Button cleanButton = null!, stopButton = null!, selectAllButton = null!, clearButton = null!, excludeButton = null!;
    Button exportButton = null!, removeExclusionButton = null!;
    ActivityPulse activity = null!;
    Label historyEmpty = null!, exclusionsEmpty = null!;
    Panel sidebar = null!, progressRow = null!;
    int activePage;
    bool loadingAppearance;
    bool dark;
    Color canvas, surface, ink, muted, border, accent;
    const string MutedRole = "muted", PrimaryRole = "primary", CardRole = "card";

    void BuildUi()
    {
        // The sidebar and content retain standard keyboard-accessible Windows controls.
        sidebar = new Panel { Dock = DockStyle.Left, Width = 214, Padding = new(18, 22, 18, 16), Tag = "sidebar" };
        content.Padding = new(24, 20, 24, 8);
        status.Height = 42; status.Padding = new(24, 10, 0, 0); status.Font = new("Segoe UI", 8);
        Controls.Add(content); Controls.Add(liveStatus); Controls.Add(sidebar);StartStatusStrip();
        var brand = TextLabel("Maintenance", 11, true); brand.Dock = DockStyle.Top; brand.Height = 37;
        var platform = TextLabel("for Windows", 9); platform.Dock = DockStyle.Top; platform.Height = 44;
        var nav = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 432, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        string[] names = ["Overview", "Cleanup", "Exclusions", "Speed tests", "History", "System", "Updates", "Startup", "Duplicates"];
        for (int i = 0; i < names.Length; i++)
        {
            var index = i;
            var button = new AppearanceButton { Text = names[i], Width = 178, Height = 42, Margin = new(0, 0, 0, 6), TextAlign = ContentAlignment.MiddleLeft, Padding = new(12, 0, 0, 0), FlatStyle = FlatStyle.Flat, AccessibleName = names[i] + " page" };
            button.Click += (_, _) => SelectPage(index); navigation.Add(button); nav.Controls.Add(button);
        }
        nav.Controls.SetChildIndex(navigation[6],4);
        nav.Controls.SetChildIndex(navigation[7],4);
        nav.Controls.SetChildIndex(navigation[8],2);
        var appearance = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 265, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        appearance.Controls.Add(TextLabel("APPEARANCE", 8, true));
        themeChoice.Items.AddRange(["Light", "Dark", "Follow Windows"]); accentChoice.Items.AddRange(["Blue", "Teal", "Violet"]);
        themeChoice.Margin = new(0, 10, 0, 12); accentChoice.Margin = new(0, 4, 0, 12);
        appearance.Controls.Add(themeChoice); appearance.Controls.Add(TextLabel("Accent color", 8)); appearance.Controls.Add(accentChoice);
        appearance.Controls.Add(TextLabel("Personal license", 8));appearance.Controls.Add(Button("Settings…",ShowSettings));
        sidebar.Controls.Add(nav); sidebar.Controls.Add(platform); sidebar.Controls.Add(brand); sidebar.Controls.Add(appearance);

        BuildDashboard(); BuildCleanup(); BuildExclusions(); BuildSpeedTests(); BuildHistory(); BuildSystem();BuildUpdates();BuildStartup();BuildDuplicates();
        files.ItemChecked += (_, _) => { if (!rebuildingFiles && IsHandleCreated) BeginInvoke(UpdateActions); };
        files.SelectedIndexChanged += (_, _) => UpdateActions();
        files.ShowItemToolTips = true; files.BorderStyle = BorderStyle.None;
        files.Resize += (_, _) => ResizeFileColumns();
        history.SelectedIndexChanged += (_, _) => { PreviewHistory(); UpdateActions(); };
        exclusionsList.SelectedIndexChanged += (_, _) => UpdateActions();
        files.OwnerDraw = true;
        files.DrawColumnHeader += (_, e) =>
        {
            using var brush = new SolidBrush(surface); e.Graphics.FillRectangle(brush, e.Bounds);
            var bounds = e.Bounds; bounds.Inflate(-8, 0);
            TextRenderer.DrawText(e.Graphics, e.Header?.Text, files.Font, bounds, ink, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        };
        files.DrawItem += (_, e) => e.DrawDefault = false;
        files.DrawSubItem += DrawThemedListRow;
        LoadAppearance();
        themeChoice.SelectedIndexChanged += (_, _) => ChangeAppearance();
        accentChoice.SelectedIndexChanged += (_, _) => ChangeAppearance();
        SystemEvents.UserPreferenceChanged += WindowsAppearanceChanged;
        FormClosed += (_, _) => SystemEvents.UserPreferenceChanged -= WindowsAppearanceChanged;
        SelectPage(0);
    }

    Panel Page(string title, string description)
    {
        var p = new Panel { Dock = DockStyle.Fill, Visible = false };
        var heading = new Panel { Dock = DockStyle.Top, Height = 92 };
        var label = TextLabel(title, 20, true); label.Dock = DockStyle.Top; label.Height = 47;
        var sub = TextLabel(description, 9); sub.Dock = DockStyle.Fill;
        heading.Controls.Add(sub); heading.Controls.Add(label); p.Controls.Add(heading);
        content.Controls.Add(p); pages.Add(p); return p;
    }
    Panel Body(Panel page)
    {
        int pageIndex = pages.IndexOf(page);
        if (pageIndex is 1 or 3) {
            // Keep scan/setup controls reachable on short displays. Scroll the page as a
            // whole instead of allowing top/bottom docks to consume the results viewport.
            int minimum = pageIndex == 1 ? 1120 : 1040;
            var viewport = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            var scrollBody = new Panel { Dock = DockStyle.Top, Height = minimum };
            viewport.Controls.Add(scrollBody); page.Controls.Add(viewport); viewport.BringToFront();
            viewport.SizeChanged += (_, _) => scrollBody.Height = Math.Max(minimum, viewport.ClientSize.Height);
            return scrollBody;
        }
        var body = new Panel { Dock = DockStyle.Fill }; page.Controls.Add(body); body.BringToFront(); return body;
    }
    static Label TextLabel(string text, float size = 9, bool strong = false) => new()
    {
        Text = text, Font = new("Segoe UI", size, strong ? FontStyle.Bold : FontStyle.Regular), AutoSize = false,
        Height = 28, Width = 170, Tag = strong ? "text" : MutedRole, AutoEllipsis = true
    };
    Panel Card()
    {
        var panel = new Panel { Padding = new(20), Tag = CardRole, Margin = new(0, 0, 12, 14) };
        cards.Add(panel); return panel;
    }
    static FlowLayoutPanel Bar(int height = 58) => new() { Dock = DockStyle.Bottom, Height = height, WrapContents = true, Padding = new(0, 8, 0, 0) };
    Button Primary(string text, Func<Task> action)
    {
        var b = Button(text, action); b.Tag = PrimaryRole; b.MinimumSize = new(192, 42); return b;
    }
    void BuildOverview()
    {
        var body = Body(Page("Your computer", "A clear starting point for everyday maintenance."));
        body.AutoScroll = true;
        var layout = new TableLayoutPanel { Dock = DockStyle.Top, Height = 650, ColumnCount = 1, RowCount = 5 };
        body.Resize += (_, _) => layout.Height = Math.Max(650, body.ClientSize.Height);
        layout.RowStyles.Add(new(SizeType.Absolute, 120)); layout.RowStyles.Add(new(SizeType.Absolute, 62));
        layout.RowStyles.Add(new(SizeType.Absolute, 186)); layout.RowStyles.Add(new(SizeType.Percent, 100)); layout.RowStyles.Add(new(SizeType.Absolute, 44));
        var stats = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, Margin = Padding.Empty };
        for (int i = 0; i < 3; i++) stats.ColumnStyles.Add(new(SizeType.Percent, 33.333f));
        AddStat(stats, 0, "OPERATING SYSTEM", osValue); AddStat(stats, 1, "SYSTEM DRIVE · FREE", diskValue); AddStat(stats, 2, "INSTALLED MEMORY", ramValue);
        layout.Controls.Add(stats, 0, 0);
        machineDetail.Dock = DockStyle.Fill; machineDetail.Tag = MutedRole; machineDetail.Font = new("Segoe UI", 8); machineDetail.AutoEllipsis = true; machineDetail.Padding = new(0, 3, 0, 0); layout.Controls.Add(machineDetail, 0, 1);
        var hero = Card(); hero.Dock = DockStyle.Fill; hero.Margin = new(0, 0, 0, 14);
        var heroTitle = TextLabel("Make room. Stay in control.", 14, true); heroTitle.Dock = DockStyle.Top; heroTitle.Height = 40;
        var heroDescription = TextLabel("Review older temporary files, then choose what to remove.", 9); heroDescription.Dock = DockStyle.Fill;
        var heroAction = Bar(66); heroAction.Controls.Add(Primary("Scan temporary files", async () => { SelectPage(1); await Scan(); }));
        hero.Controls.Add(heroDescription); hero.Controls.Add(heroTitle); hero.Controls.Add(heroAction); layout.Controls.Add(hero, 0, 2);
        var last = Card(); last.Dock = DockStyle.Fill; last.Margin = new(0, 0, 0, 12);
        var lastTitle = TextLabel("Recent activity", 11, true); lastTitle.Dock = DockStyle.Top; lastTitle.Height = 34;
        latestActivity.Dock = DockStyle.Fill; latestActivity.Font = new("Segoe UI", 9); latestActivity.AutoEllipsis = true;
        var lastActions = Bar(66); lastActions.Controls.Add(Button("View history", () => SelectPage(4)));
        last.Controls.Add(latestActivity); last.Controls.Add(lastTitle); last.Controls.Add(lastActions); layout.Controls.Add(last, 0, 3);
        var coverage = TextLabel("Preview coverage: storage and temporary files. No malware scan or overall health rating.", 8); coverage.Dock = DockStyle.Fill; layout.Controls.Add(coverage, 0, 4);
        body.Controls.Add(layout);
    }
    void AddStat(TableLayoutPanel row, int index, string title, Label value)
    {
        var box = Card(); box.Dock = DockStyle.Fill; if (index == 2) box.Margin = new(0, 0, 0, 14);
        var caption = TextLabel(title, 8); caption.Dock = DockStyle.Top; caption.Height = 29;
        value.Dock = DockStyle.Fill; value.Font = new("Segoe UI", 15, FontStyle.Bold); value.AutoEllipsis = true;
        box.Controls.Add(value); box.Controls.Add(caption); row.Controls.Add(box, index, 0);
    }
    void BuildCleanup()
    {
        var body = Body(Page("Cleanup", "Scan → review → clean. You choose every file that gets removed."));
        scanSummary.Height = 56; scanSummary.Padding = new(16, 10, 12, 0); scanSummary.Tag = "notice";
        scanSummary.Text = "Start with a scan of your temporary files.\nOnly files created and modified at least seven days ago are eligible.";
        foreach (var heading in fileHeadings) files.Columns.Add(heading, 180);
        emptyFiles.Text = "No files to review yet\n\nRun a scan to see eligible temporary files."; emptyFiles.Tag = MutedRole;
        fileArea.Controls.Add(files); fileArea.Controls.Add(emptyFiles); emptyFiles.BringToFront();
        details.Height = 130; details.Font = new("Consolas", 8); details.BorderStyle = BorderStyle.FixedSingle;
        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, MinimumSize = new(0,54), Padding = new(0, 4, 0, 4), WrapContents = true };
        var searchActions=WorkflowRow();searchActions.Controls.Add(StartSearch(Scan));stopButton=ScanCancel();searchActions.Controls.Add(stopButton);searchActions.Controls.Add(Button("Clear results",ClearResults));
        searchActions.Controls.Add(maxWait);searchActions.Controls.Add(waitSeconds);searchActions.Controls.Add(new Label {Text="seconds",AutoSize=true,Padding=new(0,10,0,0)});
        selectAllButton = Button("Select visible", () => { rebuildingFiles = true; files.BeginUpdate(); foreach (ListViewItem i in files.Items) i.Checked = true; files.EndUpdate(); rebuildingFiles = false; UpdateActions(); }); toolbar.Controls.Add(selectAllButton);viewStreamButton=Button("View stream…",ShowStreamInfo);toolbar.Controls.Add(viewStreamButton);
        clearButton = Button("Select none", () => { rebuildingFiles = true; files.BeginUpdate(); foreach (ListViewItem i in files.Items) i.Checked = false; files.EndUpdate(); rebuildingFiles = false; UpdateActions(); }); toolbar.Controls.Add(clearButton);
        excludeButton = Button("Exclude highlighted", () => { foreach (ListViewItem i in files.SelectedItems) exclusions.Add(((Candidate)i.Tag!).Path); SaveExclusions(); }); toolbar.Controls.Add(excludeButton);
        toolbar.Controls.Add(filterToggle);toolbar.Controls.Add(groupFolders);toolbar.Controls.Add(enableFileSelection);
        var expert = new AppearanceCheckBox { Text = "Expert details", AutoSize = true, AccessibleName = "Show expert details" };
        expert.CheckedChanged += (_, _) => details.Visible = expert.Checked; toolbar.Controls.Add(expert);
        var footer = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = 112, ColumnCount = 2, RowCount = 2, Padding = new(0, 12, 0, 0) };
        footer.ColumnStyles.Add(new(SizeType.Percent, 100)); footer.ColumnStyles.Add(new(SizeType.Absolute, 216));
        footer.RowStyles.Add(new(SizeType.Absolute, 46)); footer.RowStyles.Add(new(SizeType.Percent, 100));
        selectedSummary.Dock = DockStyle.Fill; selectedSummary.TextAlign = ContentAlignment.MiddleLeft; selectedSummary.Font = new("Segoe UI", 10, FontStyle.Bold); footer.Controls.Add(selectedSummary, 0, 0);
        cleanButton = Primary("Clean selected…", Clean); cleanButton.Dock = DockStyle.Fill; footer.Controls.Add(cleanButton, 1, 0);
        var permanent = cleanupNotice; permanent.Dock = DockStyle.Fill; permanent.Padding = new(0, 8, 0, 0); footer.Controls.Add(permanent, 0, 1); footer.SetColumnSpan(permanent, 2);
        progressRow = new Panel { Dock = DockStyle.Bottom, Height = 58 };
        activity = new ActivityPulse { Dock = DockStyle.Fill, Visible = false, Margin = new(0, 8, 0, 8) };
        
        progressRow.Padding = new(0, 6, 0, 6); progressRow.Controls.Add(activity);
        body.Controls.Add(fileArea); body.Controls.Add(details); body.Controls.Add(filterSummary); body.Controls.Add(BuildFilters()); body.Controls.Add(toolbar); body.Controls.Add(scanSummary); body.Controls.Add(BuildCleanupMetrics()); body.Controls.Add(BuildCleanupSources());body.Controls.Add(BuildBrowserSources());body.Controls.Add(searchActions);var scanLocationRow=BuildScanLocation();body.Controls.Add(scanLocationRow); body.Controls.Add(progressRow); body.Controls.Add(BuildResultPager()); body.Controls.Add(footer);BuildRestorePanel(body,scanLocationRow);
    }
    void BuildExclusions()
    {
        var body = Body(Page("Exclusions", "Files and folders you want Maintenance to leave alone."));
        exclusionsList.BorderStyle = BorderStyle.None; exclusionsList.HorizontalScrollbar = true; exclusionsList.IntegralHeight = false;
        exclusionsEmpty = TextLabel("No exclusions yet. Add a file or folder to keep it out of future cleanup.", 9); exclusionsEmpty.Dock = DockStyle.Top; exclusionsEmpty.Height = 56;
        var actions = Bar(66);
        actions.Controls.Add(Button("Add folder…", () => { using var d = new FolderBrowserDialog(); if (d.ShowDialog(this) == DialogResult.OK) { exclusions.Add(d.SelectedPath); SaveExclusions(); } }));
        actions.Controls.Add(Button("Add file…", () => { using var d = new OpenFileDialog(); if (d.ShowDialog(this) == DialogResult.OK) { exclusions.Add(d.FileName); SaveExclusions(); } }));
        removeExclusionButton = Button("Remove selected", () => { if (exclusionsList.SelectedItem is string p) { exclusions.Remove(p); SaveExclusions(); } }); actions.Controls.Add(removeExclusionButton);
        body.Controls.Add(exclusionsList); body.Controls.Add(exclusionsEmpty); body.Controls.Add(actions);
    }
    void BuildHistory()
    {
        var body = Body(Page("History", "Scans, deleted files and recycle records. Stored only on this computer."));
        history.BorderStyle = BorderStyle.None; history.IntegralHeight = false; history.HorizontalScrollbar = true; history.ItemHeight = 28;
        history.Dock = DockStyle.Fill;
        historyEmpty = TextLabel("No activity yet. Your first scan will appear here.", 9); historyEmpty.Dock = DockStyle.Top; historyEmpty.Height = 40;
        var reportCard = Card(); reportCard.Dock = DockStyle.Fill;
        var caption = TextLabel("Report preview · identifying paths omitted", 9, true); caption.Dock = DockStyle.Top; caption.Height = 38;
        historyPreview.Font = new("Segoe UI", 9); reportCard.Controls.Add(historyPreview); reportCard.Controls.Add(caption);
        var actions = Bar(66); exportButton = Button("Preview / export…", Report); actions.Controls.Add(exportButton); actions.Controls.Add(Button("Refresh", RefreshHistory));
        var split=new SplitContainer{Dock=DockStyle.Fill,Orientation=Orientation.Vertical,SplitterWidth=8,Size=new(1200,650),SplitterDistance=490,Panel1MinSize=280,Panel2MinSize=320};
        split.Panel1.Controls.Add(history);split.Panel1.Controls.Add(historyEmpty);split.Panel2.Controls.Add(reportCard);
        BuildHistoryTabs(body,split,actions);
    }
    void SelectPage(int index)
    {
        activePage = index;
        for (int i = 0; i < pages.Count; i++) pages[i].Visible = i == index;
        pages[index].BringToFront(); ApplyNavigation(); ResizeFileColumns();
    }
    void ResizeFileColumns()
    {
        if (files.Columns.Count == 8 && !fileColumnsSized) FitFileColumns();
    }
    void UpdateActions()
    {
        if (cleanButton is null) return;
        previousFiles.Enabled=!busy&&!pendingFilter&&scan is not null&&filePage>0;
        nextFiles.Enabled=!busy&&!pendingFilter&&scan is not null&&(filePage+1)*FilePageSize<filteredRows.Count;
        enableFileSelection.Visible=!streamReview;enableFileSelection.Enabled=!busy&&!resultsMode&&!pendingFilter;
        files.GroupCheckboxes=enableFileSelection.Checked&&!streamReview&&!resultsMode&&!busy&&groupFolders.Checked;
        if(!busy)files.CheckBoxes=!streamReview&&!resultsMode&&enableFileSelection.Checked;
        cleanupNotice.Text=streamReview?"Read-only stream search. No files or metadata will be changed.":driveReview?"Personal files go to the Recycle Bin. Folder checkboxes select only visible files on this page.":"Cleanup is permanent. Selected temporary files do not go to the Recycle Bin.";
        cleanButton.Text=driveReview?"Recycle selected…":"Clean selected…";
        if(viewStreamButton!=null){viewStreamButton.Visible=streamReview;viewStreamButton.Enabled=!busy&&streamReview&&files.SelectedItems.Count>0;}
        int count = files.CheckedItems.Count;
        long bytes = files.CheckedItems.Cast<ListViewItem>().Sum(i => ((Candidate)i.Tag!).Size);
        selectedSummary.Text = streamReview ? "Stream review · No files or streams will be changed" : driveReview&&!PersonalSelection ? "Review only · Enable file & folder checkboxes to select files" : resultsMode ? "Results retained · Scan again to clean more files" : $"{count:N0} selected · {FormatBytes(bytes)}";
        cleanButton.Enabled = !busy && enableFileSelection.Checked && (!driveReview||PersonalSelection) && !streamReview && !resultsMode && !pendingFilter && count > 0 && scan is not null;
        selectAllButton.Enabled = !busy && enableFileSelection.Checked && (!driveReview||PersonalSelection) && !streamReview && !resultsMode && files.Items.Count > 0;
        clearButton.Enabled = !busy && (!driveReview||PersonalSelection) && !streamReview && !resultsMode && count > 0;
        excludeButton.Enabled = !busy && files.SelectedItems.Count > 0;
        exportButton.Enabled = !busy && history.SelectedItem is HistoryItem;
        removeExclusionButton.Enabled = !busy && exclusionsList.SelectedIndex >= 0;
        scanLocation.Enabled = !busy; maxWait.Enabled = !busy; waitSeconds.Enabled = !busy && maxWait.Checked; stopButton.Enabled = busy; activity.Visible = busy; progressRow.Visible = busy&&scanLocation.Text!=RestoreMode;
        vmDrive.Enabled=vmRaw.Enabled=!busy;UpdateRestoreSelection();
        emptyFiles.Visible = files.Items.Count == 0;
        emptyFiles.Text = busy ? "Working…\n\nProgress appears below. You can cancel remaining work." : scan is null ? "Nothing waiting for review\n\nScan files to create a fresh cleanup preview." : "No eligible files found\n\nSee expert details for skipped files and scan coverage.";
        if (emptyFiles.Visible) emptyFiles.BringToFront(); else files.BringToFront();
    }
    void PreviewHistory()
    {
        try { historyPreview.Text = history.SelectedItem is HistoryItem h ? LocalStore.ReadReport(h.Path, false) : "Choose an activity to preview its report."; }
        catch { historyPreview.Text = "This activity record could not be read."; }
    }
    string ActivityLabel(string path)
    {
        var name = Path.GetFileName(path); string when = name;
        if (name.Length >= 18 && DateTime.TryParseExact(name[..18], "yyyyMMdd'T'HHmmssfff", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var at)) when = at.ToLocalTime().ToString("MMM d, yyyy · h:mm tt");
        try
        {
            using var reader = File.OpenText(path); using var d = JsonDocument.Parse(reader.ReadLine() ?? "{}");
            var first = d.RootElement.GetProperty("Summary").GetString() ?? "";
            if (d.RootElement.TryGetProperty("Kind", out var kind) && !string.IsNullOrWhiteSpace(kind.GetString())) {
                if(kind.GetString()=="Speed benchmark"){
                    string Value(string name)=>d.RootElement.TryGetProperty(name,out var value)?value.GetString()??"":"";
                    return when+"   —   "+string.Join(" · ",new[]{Value("Mode"),Value("Target"),Value("Direction"),Value("Outcome")}.Where(v=>v.Length>0&&v!="—"));
                }
                return when + "   —   " + kind.GetString();
            }
            return when + (first.StartsWith("Recycle") ? "   —   Recycle personal files" : first.StartsWith("Read-only NTFS") ? "   —   Alternate data streams" : first.StartsWith("Scan storage") ? "   —   Storage analysis" : first.StartsWith("Scan local drive") ? "   —   Personal-folder scan" : first.StartsWith("Cleanup") ? "   —   Cleanup" : first.StartsWith("Scan") ? "   —   Temporary-file scan" : "   —   Activity");
        }
        catch { return when + "   —   Record unavailable"; }
    }
    sealed record Appearance(string Theme = "Light", string Accent = "Blue");
    void LoadAppearance()
    {
        loadingAppearance = true; var setting = new Appearance();
        try { var path = Path.Combine(store.Folder, "appearance.json"); if (File.Exists(path)) setting = JsonSerializer.Deserialize<Appearance>(File.ReadAllText(path)) ?? setting; } catch { }
        themeChoice.SelectedItem = themeChoice.Items.Contains(setting.Theme) ? setting.Theme : "Light";
        accentChoice.SelectedItem = accentChoice.Items.Contains(setting.Accent) ? setting.Accent : "Blue";
        loadingAppearance = false; ApplyTheme();
    }
    void ChangeAppearance()
    {
        if (loadingAppearance) return; ApplyTheme();
        try
        {
            var path = Path.Combine(store.Folder, "appearance.json");
            File.WriteAllText(path + ".new", JsonSerializer.Serialize(new Appearance(themeChoice.Text, accentChoice.Text)));
            File.Move(path + ".new", path, true);
        }
        catch { status.Text = "Appearance changed for this session, but could not be saved."; }
    }
    void WindowsAppearanceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (!IsDisposed && IsHandleCreated) BeginInvoke(() => { if (themeChoice.Text == "Follow Windows") ApplyTheme(); });
    }
    void ApplyTheme()
    {
        dark = themeChoice.Text == "Dark";
        if (themeChoice.Text == "Follow Windows")
            dark = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 1) is int value && value == 0;
        canvas = dark ? Color.FromArgb(24, 28, 35) : Color.FromArgb(245, 247, 250);
        surface = dark ? Color.FromArgb(34, 40, 49) : Color.White;
        ink = dark ? Color.FromArgb(235, 239, 245) : Color.FromArgb(28, 39, 53);
        muted = dark ? Color.FromArgb(169, 182, 199) : Color.FromArgb(85, 101, 120);
        border = dark ? Color.FromArgb(61, 72, 86) : Color.FromArgb(217, 225, 235);
        accent = accentChoice.Text switch { "Teal" => Color.FromArgb(0, 110, 104), "Violet" => Color.FromArgb(103, 64, 180), _ => Color.FromArgb(29, 88, 180) };
        PaintTheme(this, canvas);if(focusedResults!=null)PaintTheme(focusedResults,canvas); ApplyNavigation();
        foreach (var panel in cards) panel.Invalidate();
        if (IsHandleCreated)
        {
            int enabled = dark ? 1 : 0; DwmSetWindowAttribute(Handle, 20, ref enabled, sizeof(int));
            SetWindowTheme(files.Handle, dark ? "DarkMode_Explorer" : "Explorer", null);
        }
    }
    void PaintTheme(Control control, Color parent)
    {
        var role = control.Tag as string;
        Color back = role is CardRole or "sidebar" ? surface : role == "notice" ? (dark ? Color.FromArgb(39, 53, 70) : Color.FromArgb(231, 240, 253)) : parent;
        control.BackColor = back; control.ForeColor = control.Tag is Color semantic ? semantic : role == MutedRole ? muted : ink;
        if(control is Panel&&role==CardRole){control.SizeChanged-=RoundCard;control.SizeChanged+=RoundCard;RoundCard(control,EventArgs.Empty);}
        if(control is ScrollableControl or TextBox or ListView or ListBox or ScrollBar){control.HandleCreated-=ThemeNativeSurface;control.HandleCreated+=ThemeNativeSurface;if(control.IsHandleCreated)ThemeNativeSurface(control,EventArgs.Empty);}
        if(control is SearchFrame frame){frame.BackColor=surface;frame.Line=border;frame.Invalidate();back=surface;}
        if(control is TextBox textBox)textBox.BorderStyle=textBox.ReadOnly||textBox.Parent is SearchFrame?BorderStyle.None:BorderStyle.FixedSingle;
        if(control is NumericUpDown number){number.BorderStyle=BorderStyle.None;number.BackColor=surface;number.ForeColor=ink;}
        if(control is ListBox list)list.BorderStyle=BorderStyle.None;
        if(control is AppearanceCheckBox check){check.DisabledInk=muted;check.Accent=accent;check.Invalidate();}
        if(control is FileListView fileList){fileList.SelectionAccent=accent;fileList.Invalidate();}
        if(control is ActivityStrip strip){strip.Accent=accent;strip.Invalidate();}
        if(control is ActivityPulse pulse){pulse.Accent=accent;pulse.Invalidate();}
        if(control is SystemDriveBadge badge){badge.Accent=accent;badge.Invalidate();}
        if(control is BenchmarkCard benchmark){benchmark.SelectionAccent=accent;benchmark.Invalidate();}
        if(control is ListView resultList){ListResultSizing.Attach(resultList);ResultLocations.Attach(resultList,()=>resultList.SelectedItems.Count==0?null:ResultLocations.Resolve(resultList.SelectedItems[0].Tag,resultList.SelectedItems[0].SubItems.Cast<ListViewItem.ListViewSubItem>().Select(s=>s.Text)),point=>{var item=resultList.GetItemAt(point.X,point.Y);if(item!=null){resultList.SelectedItems.Clear();item.Selected=true;item.Focused=true;}});}
        if(control is ListBox listBox)ThemeListBox(listBox);
        if(control is LinkLabel link){link.LinkColor=dark?ControlPaint.Light(accent,.35f):accent;link.ActiveLinkColor=accent;link.VisitedLinkColor=link.LinkColor;}
        if(control is TabControl tabs&&tabs.DrawMode==TabDrawMode.Normal){tabs.DrawMode=TabDrawMode.OwnerDrawFixed;tabs.Padding=new(16,8);tabs.DrawItem+=(_,e)=>{bool selected=e.Index==tabs.SelectedIndex;ThemeShapes.Fill(e.Graphics,Rectangle.Inflate(e.Bounds,-2,-2),selected?accent:surface,8);TextRenderer.DrawText(e.Graphics,tabs.TabPages[e.Index].Text,tabs.Font,e.Bounds,selected?Color.White:ink,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPrefix);};}
        if(control.ContextMenuStrip is ContextMenuStrip menu){menu.Renderer=new AccentMenuRenderer(accent,surface,ink);menu.BackColor=surface;menu.ForeColor=ink;}
        if (control is DataGridView grid)
        {
            grid.Paint -= PaintGridCorner; grid.Paint += PaintGridCorner;
            grid.BackgroundColor = surface; grid.EnableHeadersVisualStyles = false;grid.BorderStyle=BorderStyle.None;grid.CellBorderStyle=DataGridViewCellBorderStyle.SingleHorizontal;grid.ColumnHeadersBorderStyle=DataGridViewHeaderBorderStyle.None;
            grid.DefaultCellStyle.BackColor = surface; grid.DefaultCellStyle.ForeColor = ink;
            grid.ColumnHeadersDefaultCellStyle.BackColor = surface; grid.ColumnHeadersDefaultCellStyle.ForeColor = ink;
            grid.DefaultCellStyle.SelectionBackColor=accent;grid.DefaultCellStyle.SelectionForeColor=Color.White;
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor=surface;grid.ColumnHeadersDefaultCellStyle.SelectionForeColor=ink;
            grid.RowHeadersDefaultCellStyle.SelectionBackColor=accent;grid.RowHeadersDefaultCellStyle.SelectionForeColor=Color.White;
            grid.GridColor = border;
        }
        if (control is Button button) { StyleButton(button); return; }
        if (control is TextBox or ListView or ListBox or ComboBox) { control.BackColor = surface; control.ForeColor = ink; }
        foreach (Control child in control.Controls) PaintTheme(child, back);
    }
    static void PaintGridCorner(object? sender, PaintEventArgs e)
    {
        if (sender is not DataGridView grid) return;
        var vertical = grid.Controls.OfType<VScrollBar>().FirstOrDefault(s => s.Visible);
        var horizontal = grid.Controls.OfType<HScrollBar>().FirstOrDefault(s => s.Visible);
        if (vertical is null || horizontal is null) return;
        using var brush = new SolidBrush(grid.BackgroundColor);
        e.Graphics.FillRectangle(brush, new Rectangle(vertical.Left, horizontal.Top, vertical.Width, horizontal.Height));
    }
    static void RoundCard(object? sender,EventArgs e){if(sender is not Control c||c.Width<2||c.Height<2)return;using var path=ThemeShapes.Rounded(c.ClientRectangle,10);var previous=c.Region;c.Region=new Region(path);previous?.Dispose();}
    void ThemeNativeSurface(object? sender,EventArgs e){if(sender is Control c&&c.IsHandleCreated)SetWindowTheme(c.Handle,dark?"DarkMode_Explorer":"Explorer",null);}
    void StyleButton(Button b)
    {
        if (b is AppearanceButton styled) { styled.DisabledInk = muted; styled.DisabledSurface = surface; }
        if(b is AppearanceChoice choice)choice.SelectionAccent=accent;
        b.FlatStyle = FlatStyle.Flat; b.FlatAppearance.BorderSize = 1; b.FlatAppearance.BorderColor = border;
        b.Margin = new(0, 0, 8, 0); b.Cursor = Cursors.Hand;
        bool primary = b.Tag as string == PrimaryRole;
        bool start=b.Tag as string==StartRole || b is not AppearanceChoice && (b.Text.StartsWith("Scan ")||b.Text.StartsWith("Find duplicates")||b.Text is "Run benchmark" or "Start test" or "Read usage history" or "Map app data" or "Refresh inventory");
        bool stop=b.Tag as string==CancelRole||b.Text is "Cancel" or "Stop storage scan";
        Color action=stop?Color.FromArgb(184,53,66):start?Color.FromArgb(22,133,83):accent;
        bool colored=primary||start||stop;
        b.BackColor = colored ? action : surface; b.ForeColor = colored ? Color.White : ink;
        b.FlatAppearance.MouseOverBackColor = colored ? ControlPaint.Light(action, .1f) : (dark ? Color.FromArgb(48, 57, 70) : Color.FromArgb(232, 238, 245));
        if(b.Parent is FlowLayoutPanel && !navigation.Contains(b)){b.MinimumSize=new(Math.Max(104,b.MinimumSize.Width),42);b.Padding=new(12,6,12,6);}
    }
    void ApplyNavigation()
    {
        for (int i = 0; i < navigation.Count; i++)
        {
            var b = navigation[i]; b.FlatAppearance.BorderSize = 0;
            b.BackColor = i == activePage ? accent : surface; b.ForeColor = i == activePage ? Color.White : muted;
            b.Font = new("Segoe UI", 9, i == activePage ? FontStyle.Bold : FontStyle.Regular);
        }
    }
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr window, int attr, ref int value, int size);
    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)] static extern int SetWindowTheme(IntPtr window, string theme, string? subId);
}


















